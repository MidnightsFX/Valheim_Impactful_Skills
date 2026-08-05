using ImpactfulSkills.common;
using System.Collections.Generic;
using UnityEngine;

namespace ImpactfulSkills.modules.Multiplant {

    /// Many of the classes in this file are roughly based on
    /// https://github.com/AdvizeGH/Advize_ValheimMods/tree/main/Advize_PlantEasily
    /// These are modified, but original design and implementation is credited to Advize
    /// This project uses the GNU 3.0 License also and all references to this implementation must do the same

    /// <summary>
    /// Manages the pool of extra ghost GameObjects and positions them each frame using the
    /// directions computed by PlantGridState.
    ///
    /// Index 0 = root ghost (Player.m_placementGhost).
    /// Index 1..N = ExtraGhosts[0..N-1].
    ///
    /// The block is CENTERED on the cursor:
    ///   BasePosition + RowDirection * (row - (Rows-1)/2) + ColumnDirection * (col - (Columns-1)/2)
    /// Centering is what makes the grid stable: negating either axis maps the block onto itself, so
    /// the sign the snap system picks for an axis no longer matters. The old corner-anchored layout
    /// made that sign load-bearing, and a single occupied neighboring cell could mirror the whole
    /// block from one frame to the next.
    /// </summary>
    internal static class PlantGhostController {
        internal static readonly List<GameObject> ExtraGhosts = new List<GameObject>();
        // Per-ghost validity — index 0 = root ghost, 1+ = extra ghosts
        internal static readonly List<bool> GhostValid = new List<bool>();

        private static readonly int _ghostLayer = LayerMask.NameToLayer("ghost");
        private static string _lastPlantName = "";
        private static bool _preservePool;

        // Pool sizing. Deliberately independent of the AOE toggle so switching it off and on again
        // does not destroy and rebuild every ghost.
        private static int MaxActiveGhosts => PlantGrid.MaxToPlantAtOnce() - 1;
        private static int TotalCells => 1 + MaxActiveGhosts;

        /// <summary>
        /// How many cells the grid actually lays out this frame. With the AOE toggle off only one
        /// plant is placed, so the layout must collapse to 1x1 — otherwise the snap would size its
        /// search and offsets for the full block and position that single plant as if it were a
        /// corner of one. Low-skill players already have MaxToPlantAtOnce() == 1 and take the same path.
        /// </summary>
        internal static int LayoutCells => PlantGrid.MultiplantDisabled ? 1 : TotalCells;

        // Target a square layout, capped by the configured max columns
        internal static int Columns {
            get {
                int ideal = Mathf.CeilToInt(Mathf.Sqrt(LayoutCells));
                return Mathf.Clamp(ideal, 1, ValConfig.FarmingMultiplantColumnCount.Value);
            }
        }
        internal static int Rows => Mathf.CeilToInt(LayoutCells / (float)Columns);

        // One resolved grid cell for this frame.
        private struct PlantCell {
            internal Vector3 pos;
            internal float yaw;
            internal bool valid;
        }

        private static readonly List<PlantCell> _cells = new List<PlantCell>();
        private static readonly List<Vector2> _offsets = new List<Vector2>();
        // Per-cell decorative yaw, so mass-planted crops do not all face the same way.
        private static float[] _cellYaw = new float[0];

        /// <summary>
        /// The block's cell offsets in CELL UNITS relative to BasePosition, as (row, col).
        /// A component is a half-integer when that axis is centered across an even number of cells
        /// (the default 12-plant grid is 4 columns wide, giving -1.5, -0.5, +0.5, +1.5).
        ///
        /// SnapSystem uses this too: it has to align a CELL to the lattice rather than the grid
        /// center, because centering an even-width axis would otherwise leave every cell sitting
        /// half a cell off the existing pattern and a second batch could never line up.
        /// </summary>
        internal static void GetCellOffsets(List<Vector2> into) {
            into.Clear();
            int cols = Columns;
            int total = LayoutCells;
            // Offset by the FULL rows x columns extent, so a partially filled last row does not pull
            // the block off the cursor.
            float rowMid = ValConfig.FarmingMultiPlantCenterRows.Value ? (Rows - 1) / 2f : 0f;
            float colMid = ValConfig.FarmingMultiPlantCenterColumns.Value ? (cols - 1) / 2f : 0f;
            for (int i = 0; i < total; i++) {
                into.Add(new Vector2(i / cols - rowMid, i % cols - colMid));
            }
        }

        // ── Lifecycle ──────────────────────────────────────────────────────────

        /// <summary>Called from SetupPlacementGhost to manage pool lifecycle before BuildGrid.</summary>
        internal static void Prepare(GameObject rootGhost) {
            if (rootGhost == null) {
                DestroyPool();
                return;
            }

            DetectPlantChange(rootGhost);
            if (ShouldPreservePool()) { return; }
            DestroyPool();
        }

        /// <summary>Grow pool to required size and initialize ghost states.</summary>
        internal static void BuildGrid(GameObject rootGhost) {
            GrowPoolIfNeeded(rootGhost);
            InitializeGhosts(rootGhost);
            DeactivateExcess();
            // SetupPlacementGhost runs again after each placement, so this re-rolls the facings for
            // every batch rather than stamping out the same pattern repeatedly.
            RandomizeCellYaw();
            PlantGrid.GridPlantingActive = true;
        }

        internal static void DestroyPool() {
            foreach (GameObject g in ExtraGhosts) {
                if (g != null) UnityEngine.Object.Destroy(g);
            }
            ExtraGhosts.Clear();
            GhostValid.Clear();
            _cells.Clear();
            PlantGrid.GridPlantingActive = false;
            PlantGridState.ResetSavedOrientation();
        }

        // ── Per-frame update ───────────────────────────────────────────────────

        /// <summary>Called every frame from UpdatePlacementGhost after PlantGridState.Update().</summary>
        internal static void Update() {
            UpdateVisibility();

            if (PlantGridState.PlacementGhost == null) return;

            BuildCells();
            AssignGhosts();
        }

        /// <summary>
        /// Resolve every cell position and its validity for this frame, then decide which cell the
        /// root ghost occupies.
        /// </summary>
        private static void BuildCells() {
            _cells.Clear();
            GetCellOffsets(_offsets);
            EnsureYawCapacity(_offsets.Count);

            for (int i = 0; i < _offsets.Count; i++) {
                Vector3 pos = PlantGridState.BasePosition
                    + PlantGridState.RowDirection * _offsets[i].x
                    + PlantGridState.ColumnDirection * _offsets[i].y;

                Heightmap.GetHeight(pos, out float height);
                pos.y = height;

                _cells.Add(new PlantCell { pos = pos, yaw = _cellYaw[i], valid = IsValidPosition(pos) });
            }

            // Index 0 is Player.m_placementGhost and Valheim refuses to place when it is invalid, so a
            // blocked center cell would otherwise make the entire grid unplaceable. Swap the valid cell
            // nearest the center into index 0. The yaw travels with the cell so nothing visibly spins.
            int rootIdx = 0;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _cells.Count; i++) {
                if (!_cells[i].valid) continue;
                float sqr = (_cells[i].pos - PlantGridState.BasePosition).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; rootIdx = i; }
            }
            if (rootIdx != 0) {
                PlantCell swap = _cells[0];
                _cells[0] = _cells[rootIdx];
                _cells[rootIdx] = swap;
            }
        }

        private static void AssignGhosts() {
            GhostValid.Clear();

            for (int i = 0; i < _cells.Count; i++) {
                PlantCell cell = _cells[i];
                GhostValid.Add(cell.valid);

                GameObject ghost = GetGhost(i);
                if (ghost == null) continue;

                ghost.transform.position = cell.pos;
                // Vanilla rewrites the root ghost's transform at the top of every UpdatePlacementGhost,
                // so writing rotation here cannot compound frame to frame — and PlacePiece reads the
                // ghost's rotation, which is what keeps the preview honest about what gets planted.
                ghost.transform.rotation = Quaternion.Euler(0, cell.yaw, 0) * PlantGridState.BaseRotation;
                ghost.GetComponent<Piece>()?.SetInvalidPlacementHeightlight(!cell.valid);
            }

            // We moved the root ghost after Valheim already computed its placement status, so re-assert
            // it from the cell we actually put it on.
            if (_cells.Count > 0 && Player.m_localPlayer != null) {
                Player.m_localPlayer.m_placementStatus = _cells[0].valid
                    ? Player.PlacementStatus.Valid
                    : Player.PlacementStatus.Invalid;
            }
        }

        // ── Internal pool management ───────────────────────────────────────────

        private static void EnsureYawCapacity(int count) {
            if (_cellYaw.Length >= count) return;
            float[] grown = new float[count];
            _cellYaw.CopyTo(grown, 0);
            bool random = ValConfig.FarmingMultiPlantRandomRotation.Value;
            for (int i = _cellYaw.Length; i < count; i++) {
                grown[i] = random ? Random.Range(0f, 360f) : 0f;
            }
            _cellYaw = grown;
        }

        private static void RandomizeCellYaw() {
            EnsureYawCapacity(TotalCells);
            bool random = ValConfig.FarmingMultiPlantRandomRotation.Value;
            for (int i = 0; i < _cellYaw.Length; i++) {
                _cellYaw[i] = random ? Random.Range(0f, 360f) : 0f;
            }
        }

        private static void GrowPoolIfNeeded(GameObject rootGhost) {
            string rootName = rootGhost.name;
            while (ExtraGhosts.Count < MaxActiveGhosts) {
                ZNetView.m_forceDisableInit = true;
                GameObject clone = UnityEngine.Object.Instantiate(rootGhost);
                ZNetView.m_forceDisableInit = false;
                clone.name = rootName;
                // All child objects on ghost layer so they don't affect collision/validity checks
                foreach (Transform t in clone.GetComponentsInChildren<Transform>())
                    t.gameObject.layer = _ghostLayer;
                ExtraGhosts.Add(clone);
            }
        }

        private static void InitializeGhosts(GameObject rootGhost) {
            GhostValid.Clear();
            GhostValid.Add(true); // index 0 = root

            Transform rootT = rootGhost.transform;
            for (int i = 0; i < MaxActiveGhosts && i < ExtraGhosts.Count; i++) {
                GameObject g = ExtraGhosts[i];
                g.SetActive(true);
                g.transform.position = rootT.position;
                g.transform.localScale = rootT.localScale;
                GhostValid.Add(true);
            }
        }

        private static void DeactivateExcess() {
            for (int i = MaxActiveGhosts; i < ExtraGhosts.Count; i++) {
                ExtraGhosts[i].SetActive(false);
            }
        }

        private static void UpdateVisibility() {
            // With the AOE toggle off only the root ghost is shown; the pool itself is left intact.
            bool active = PlantGridState.PlacementGhost != null
                          && PlantGridState.PlacementGhost.activeSelf
                          && !PlantGrid.MultiplantDisabled;
            for (int i = 0; i < ExtraGhosts.Count; i++) {
                bool shouldBeActive = active && i < MaxActiveGhosts;
                if (ExtraGhosts[i].activeSelf != shouldBeActive) {
                    ExtraGhosts[i].SetActive(shouldBeActive);
                }
            }
        }

        private static GameObject GetGhost(int index) {
            if (index == 0) return PlantGridState.PlacementGhost;
            int ei = index - 1;
            return ei < ExtraGhosts.Count ? ExtraGhosts[ei] : null;
        }

        internal static bool IsValidPosition(Vector3 pos) {
            Heightmap heightmap = Heightmap.FindHeightmap(pos);
            if (heightmap == null || PlantGridState.Plant == null) { return false; }
            if (PlantGridState.Plant.m_needCultivatedGround && !heightmap.IsCultivated(pos)) { return false; }

            return Physics.OverlapSphere(pos, PlantGridState.Plant.m_growRadius, PlantDefinitions.plantSpaceMask).Length == 0;
        }

        private static void DetectPlantChange(GameObject rootGhost) {
            string name = rootGhost.name;
            if (name == _lastPlantName) {
                _preservePool = true;
                return;
            }
            _lastPlantName = name;
            _preservePool = false;
        }

        private static bool ShouldPreservePool() {
            if (!_preservePool || !PlantGrid.GridPlantingActive) return false;
            _preservePool = false;
            return true;
        }
    }
}
