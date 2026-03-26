using ImpactfulSkills.common;
using System.Collections.Generic;
using UnityEngine;

namespace ImpactfulSkills.modules.Multiplant {
    /// <summary>
    /// Manages the pool of extra ghost GameObjects and positions them each frame using the
    /// directions computed by PlantGridState. Closely mirrors PlantEasily's GhostGrid.
    ///
    /// Index 0 = root ghost (Player.m_placementGhost).
    /// Index 1..N = ExtraGhosts[0..N-1].
    ///
    /// Ghost positions: BasePosition + RowDirection * row + ColumnDirection * col
    ///   where  row = index / Columns,  col = index % Columns
    /// </summary>
    internal static class PlantGhostController {
        internal static readonly List<GameObject> ExtraGhosts = new List<GameObject>();
        // Per-ghost validity — index 0 = root ghost, 1+ = extra ghosts
        internal static readonly List<bool> GhostValid = new List<bool>();

        private static readonly int _ghostLayer = LayerMask.NameToLayer("ghost");
        private static string _lastPlantName = "";
        private static bool _preservePool;

        private static int MaxActiveGhosts => PlantGrid.MaxToPlantAtOnce() - 1;
        private static int TotalCells => 1 + MaxActiveGhosts;
        // Target a square layout, capped by the configured max columns
        private static int Columns {
            get {
                int ideal = Mathf.CeilToInt(Mathf.Sqrt(TotalCells));
                return Mathf.Clamp(ideal, 1, ValConfig.FarmingMultiplantColumnCount.Value);
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
            PlantGrid.GridPlantingActive = true;
        }

        internal static void DestroyPool() {
            foreach (GameObject g in ExtraGhosts) {
                if (g != null) UnityEngine.Object.Destroy(g);
            }
            ExtraGhosts.Clear();
            GhostValid.Clear();
            PlantGrid.GridPlantingActive = false;
            SnapSystem.ResetSnap();
            PlantGridState.ResetSavedOrientation();
        }

        // ── Per-frame update ───────────────────────────────────────────────────

        /// <summary>Called every frame from UpdatePlacementGhost after PlantGridState.Update().</summary>
        internal static void Update() {
            UpdateVisibility();

            if (PlantGridState.PlacementGhost == null) return;

            int cols = Columns;

            // Root ghost is always updated
            UpdateGhost(0, 0, 0);

            for (int i = 1; i < TotalCells; i++) {
                if (i - 1 >= ExtraGhosts.Count) break;
                UpdateGhost(i / cols, i % cols, i);
            }
        }

        // ── Internal pool management ───────────────────────────────────────────

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
            bool active = PlantGridState.PlacementGhost != null && PlantGridState.PlacementGhost.activeSelf;
            for (int i = 0; i < ExtraGhosts.Count; i++) {
                bool shouldBeActive = active && i < MaxActiveGhosts;
                if (ExtraGhosts[i].activeSelf != shouldBeActive) {
                    ExtraGhosts[i].SetActive(shouldBeActive);
                }
            }
        }

        private static void UpdateGhost(int row, int col, int index) {
            GameObject ghost = GetGhost(index);
            if (ghost == null) return;

            // Row 0, col 0 is always the root ghost at BasePosition;
            // other positions extend along the pre-computed direction vectors
            Vector3 pos = index == 0 ? PlantGridState.BasePosition :
                PlantGridState.BasePosition
                + PlantGridState.RowDirection * row
                + PlantGridState.ColumnDirection * col;

            Heightmap.GetHeight(pos, out float height);
            pos.y = height;

            ghost.transform.position = pos;
            // Root ghost (index 0) rotation is managed by Valheim and left untouched.
            if (index > 0) {
                ghost.transform.rotation = Quaternion.identity;
            }

            bool isValid = IsValidPosition(pos);
            ghost.GetComponent<Piece>()?.SetInvalidPlacementHeightlight(!isValid);

            if (index < GhostValid.Count) {
                GhostValid[index] = isValid;
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

            return Physics.OverlapSphere(pos, PlantGridState.Plant.m_growRadius, Plant.m_spaceMask).Length == 0;
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
