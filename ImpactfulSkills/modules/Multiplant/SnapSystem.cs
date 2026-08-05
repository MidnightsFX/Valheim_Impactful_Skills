using ImpactfulSkills.common;
using System.Collections.Generic;
using UnityEngine;

namespace ImpactfulSkills.modules.Multiplant {

    /// Many of the classes in this file are roughly based on
    /// https://github.com/AdvizeGH/Advize_ValheimMods/tree/main/Advize_PlantEasily
    /// These are modified, but original design and implementation is credited to Advize
    /// This project uses the GNU 3.0 License also and all references to this implementation must do the same
    ///
    /// The Grid-style rotation snap below (mutually-closest pair + 90 degree fold) is adapted from
    /// https://github.com/blaxxun-boop/Farming/blob/master/Farming/MassPlant.cs
    /// which itself derives from https://github.com/Xeio/MassFarming (MIT).


    internal class SnapPoint {
        internal Vector3 pos;
        internal Vector3 rowDir;
        internal Vector3 colDir;
        internal Vector3 origin;

        internal SnapPoint() {
        }

        internal SnapPoint(Vector3 pos, Vector3 rowDir, Vector3 colDir, Vector3 origin) {
            this.pos = pos;
            this.rowDir = rowDir;
            this.colDir = colDir;
            this.origin = origin;
        }
    }

    /// <summary>
    /// Finds snap points near the placement ghost and, when found, commits the snapped position
    /// and aligned grid directions onto PlantGridState.
    ///
    /// All comparisons here are horizontal (XZ) only. Plants on a terraced or sloped field sit at
    /// different heights, and folding Y into the distances made snapping engage at different
    /// horizontal ranges on a hill than on flat ground.
    /// </summary>
    internal static class SnapSystem {
        // Same layer set the ghosts validate against, and the same one Valheim's own
        // Plant.HaveGrowSpace uses. Shared so the two can never drift apart.
        private static int ScanMask => PlantDefinitions.plantSpaceMask;

        // Sanity bound on how many nearby plants we keep after sorting.
        private const int MaxPrimaries = 16;

        // How far out the 1x1 layout may look for a free cell, in cells.
        private const int FreeCellSearch = 2;

        // Reused per-frame scratch so a scan that runs every frame does not churn the GC.
        private static readonly List<Transform> _nearbyPlants = new List<Transform>();
        private static readonly HashSet<Transform> _scanSeen = new HashSet<Transform>();
        private static readonly Collider[] _scanBuffer = new Collider[512];
        private static readonly List<Vector2> _snapOffsets = new List<Vector2>();

        /// <summary>
        /// Try to snap. On success sets PlantGridState.BasePosition, RowDirection, ColumnDirection
        /// and returns true.
        ///
        /// "Grid" style (default) detects the lattice implied by the surrounding plants and aligns the
        /// whole grid to it. "Legacy" keeps the original nearest-plant behavior.
        /// </summary>
        internal static bool FindSnapPoints(string plantName, float pieceSpacing) {
            if (ValConfig.FarmingSnapStyle?.Value == "Legacy") {
                return TryFreeSnap(plantName, pieceSpacing);
            }
            return TryGridSnap(plantName, pieceSpacing);
        }

        // ── Free snap (Legacy) ─────────────────────────────────────────────────

        private static bool TryFreeSnap(string plantName, float pieceSpacing) {
            if (ScanForPlants(PlantGridState.BasePosition, ValConfig.PlantingSnapDistance.Value, plantName) == 0) return false;
            Transform nearest = SortByDistance(_nearbyPlants, PlantGridState.BasePosition);
            ComputeFreeDirections(nearest.position, pieceSpacing);

            List<SnapPoint> snapPoints = new List<SnapPoint>();
            if (!GenerateCandidates(snapPoints, nearest.position)) return false;

            CommitSnap(FindNearestEuclidean(snapPoints));
            return true;
        }

        private static void ComputeFreeDirections(Vector3 target, float pieceSpacing) {
            Vector3 dir = Flatten(PlantGridState.BasePosition - target);

            if (dir.sqrMagnitude < 0.001f) {
                dir = Vector3.forward;
            } else {
                dir.Normalize();
                if (!PlantGridState.AltPlacement) {
                    float angle = Vector3.SignedAngle(Vector3.forward, dir, Vector3.up);
                    dir = Quaternion.Euler(0, Mathf.Round(angle / 90f) * 90f, 0) * Vector3.forward;
                }
            }

            PlantGridState.RowDirection = PlantGridState.FixedRotation * dir * pieceSpacing;
            PlantGridState.ColumnDirection = Vector3.Cross(Vector3.up, PlantGridState.RowDirection);
        }

        // ── Grid snap ──────────────────────────────────────────────────────────

        /// <summary>
        /// Snap the grid onto the lattice implied by the surrounding plants, without ever changing the
        /// player's heading or moving the block away from the cursor.
        ///
        /// Rotation is the player's alone. The axes below come straight from the ghost, which Valheim
        /// already quantizes to 22.5 degree steps, and nothing here rotates them. Position snapping
        /// then works along those axes, so the grid aligns to nearby plants while keeping the heading
        /// that was chosen — cells tile perfectly when that heading matches the patch, which is the
        /// player's call to make.
        /// </summary>
        private static bool TryGridSnap(string plantName, float heldSpacing) {
            if (heldSpacing <= 0.001f) return false;

            Vector3 cursor = PlantGridState.BasePosition;
            int found = ScanForPlants(cursor, ScanRadius(heldSpacing), plantName);
            if (found == 0) return false;

            // ── Axes: the ghost's own heading, untouched ───────────────────────
            Vector3 axisRow = Flatten(PlantGridState.RowDirection);
            if (axisRow.sqrMagnitude < 1e-6f) { axisRow = Vector3.forward; }
            axisRow.Normalize();
            Vector3 axisCol = Vector3.Cross(Vector3.up, axisRow);

            // ── Spacing ────────────────────────────────────────────────────────
            Transform anchor = _nearbyPlants[0];
            Vector3 anchorPos = anchor.position;
            float anchorSpacing = SpacingForAnchor(anchor, heldSpacing);

            // Adopt the existing patch's spacing only when it is at least as generous as the held plant
            // needs. A tighter patch would crowd our own cells against each other, and IsValidPosition
            // cannot catch that: the ghosts sit on the ghost layer and do not see one another. Report
            // no snap in that case and let the caller place freely — there is nothing left to align,
            // since rotation is the player's and we never touch it.
            if (anchorSpacing < heldSpacing - 0.001f) { return false; }

            float step = anchorSpacing;
            PlantGridState.RowDirection = axisRow * step;
            PlantGridState.ColumnDirection = axisCol * step;

            // ── Translation ────────────────────────────────────────────────────
            // Align a CELL to the lattice rather than the grid's center. When an axis is centered
            // across an even number of cells its offsets are half-integers (the default 4-wide grid
            // gives -1.5, -0.5, +0.5, +1.5), so snapping the center would leave every cell sitting
            // exactly half a cell off the existing pattern and a second batch could never line up.
            // Every offset differs from the reference by a whole number of cells, so putting the
            // reference cell on a lattice point puts the entire block on it.
            PlantGhostController.GetCellOffsets(_snapOffsets);
            if (_snapOffsets.Count == 0) { return false; }
            Vector2 refOffset = _snapOffsets[0];
            Vector3 refToBase = (axisRow * refOffset.x + axisCol * refOffset.y) * step;

            // Round onto the lattice cell nearest the CURSOR. The anchor supplies only the lattice's
            // origin and phase, never its own position as a target — so the block lands within half a
            // cell of where the player points and never centers itself on the anchor plant.
            Vector3 delta = Flatten(cursor + refToBase - anchorPos);
            int nc = Mathf.RoundToInt(Vector3.Dot(delta, axisRow) / step);
            int mc = Mathf.RoundToInt(Vector3.Dot(delta, axisCol) / step);

            // A single plant may hop to the nearest free cell; a whole block never moves. Cells of a
            // block that do not fit simply report invalid and are skipped, which is what lets a
            // partial batch plant at all.
            if (_snapOffsets.Count == 1) {
                FindFreeCell(anchorPos, axisRow, axisCol, step, refToBase, cursor, ref nc, ref mc);
            }

            Vector3 snapped = anchorPos + (axisRow * nc + axisCol * mc) * step - refToBase;
            // Keep the cursor's height; every cell resamples the heightmap for itself.
            snapped.y = cursor.y;
            CommitSnap(new SnapPoint(snapped, PlantGridState.RowDirection, PlantGridState.ColumnDirection, anchorPos));
            return true;
        }

        /// <summary>
        /// For a 1x1 layout only: if the rounded cell is not plantable, take the nearest one that is.
        /// The rounded cell is tested first so the common case costs a single check.
        ///
        /// Uses PlantGhostController.IsValidPosition so the search agrees exactly with what the ghost
        /// highlights and what PlacePiece will accept — cultivated ground included. Scoring purely on
        /// plant occupancy was what let the old slide search wander onto unfarmed dirt.
        /// </summary>
        private static void FindFreeCell(Vector3 anchorPos, Vector3 axisRow, Vector3 axisCol, float step,
                                         Vector3 refToBase, Vector3 cursor, ref int nc, ref int mc) {
            Vector3 CellPos(int n, int m) {
                Vector3 p = anchorPos + (axisRow * n + axisCol * m) * step - refToBase;
                Heightmap.GetHeight(p, out float h);
                p.y = h;
                return p;
            }

            if (PlantGhostController.IsValidPosition(CellPos(nc, mc))) { return; }

            int bestN = nc, bestM = mc;
            float bestSqr = float.MaxValue;
            bool found = false;
            for (int dn = -FreeCellSearch; dn <= FreeCellSearch; dn++) {
                for (int dm = -FreeCellSearch; dm <= FreeCellSearch; dm++) {
                    if (dn == 0 && dm == 0) continue;
                    Vector3 pos = CellPos(nc + dn, mc + dm);
                    if (!PlantGhostController.IsValidPosition(pos)) continue;
                    float sqr = FlatSqrDistance(pos, cursor);
                    if (sqr < bestSqr) { bestSqr = sqr; bestN = nc + dn; bestM = mc + dm; found = true; }
                }
            }
            // Nothing free in range: stay on the rounded cell so the ghost shows red rather than jumping.
            if (found) { nc = bestN; mc = bestM; }
        }

        // How far out to look for plants. Covers the block itself plus a margin for lattice detection,
        // and the free-cell search when the layout is a single plant. Scales with the plant's own
        // spacing so a large-radius plant is not stuck with a search area smaller than one of its cells.
        private static float ScanRadius(float spacing) {
            float halfExtent = Mathf.Max(PlantGhostController.Rows, PlantGhostController.Columns) * 0.5f;
            float search = PlantGhostController.LayoutCells == 1 ? FreeCellSearch : 1f;
            return spacing * (halfExtent + search) + Mathf.Max(0f, ValConfig.PlantingSnapDistance.Value);
        }

        // Spacing an anchor's own patch was built with. Mirrors PlantGrid.Spacing so a same-species anchor
        // yields heldSpacing, while a different species (EnableSnappingToOtherPlants) uses its own radius.
        private static float SpacingForAnchor(Transform anchorRoot, float fallback) {
            Plant p = anchorRoot.GetComponentInChildren<Plant>();
            if (p == null) return fallback;
            float spacing = p.m_growRadius * ValConfig.FarmingMultiPlantDistanceBufferModifier.Value
                            + ValConfig.FarmingMultiPlantBufferSpace.Value;
            return spacing <= 0.001f ? fallback : spacing;
        }

        // ── Candidate generation (Legacy only) ─────────────────────────────────

        private static bool GenerateCandidates(List<SnapPoint> snapPoints, Vector3 fromPos) {
            Vector3 row = PlantGridState.RowDirection;
            Vector3 col = PlantGridState.ColumnDirection;

            Vector3[] positions = new Vector3[] {
                fromPos + row,  fromPos - row,
                fromPos + col,  fromPos - col,
            };

            float spacing = row.magnitude;
            bool hasCardinal = false;
            List<(Vector3 pos, bool isCardinal)> valid = new List<(Vector3, bool)>();

            foreach (Vector3 pos in positions) {
                if (!PlantGhostController.IsValidPosition(pos)) continue;

                Vector3 dir = pos - fromPos;
                bool isCardinal =
                    (Mathf.Abs(Vector3.Dot(dir, row.normalized)) < spacing * 0.25f) ||
                    (Mathf.Abs(Vector3.Dot(dir, col.normalized)) < spacing * 0.25f);

                valid.Add((pos, isCardinal));
                if (isCardinal) hasCardinal = true;
            }

            if (valid.Count == 0) return false;

            bool preferCardinal = ValConfig.FarmingSnapPreferCardinal?.Value ?? true;
            foreach (var (pos, isCardinal) in valid) {
                if (!preferCardinal || !hasCardinal || isCardinal)
                    snapPoints.Add(new SnapPoint(pos, row, col, fromPos));
            }

            return snapPoints.Count > 0;
        }

        // ── Commit ─────────────────────────────────────────────────────────────

        private static void CommitSnap(SnapPoint snap) {
            PlantGridState.BasePosition = PlantGridState.PlacementGhost.transform.position = snap.pos;
        }

        // ── Nearest selection ──────────────────────────────────────────────────

        private static SnapPoint FindNearestEuclidean(List<SnapPoint> snaps) {
            SnapPoint best = snaps[0];
            float bestDist = DistanceXZ(best.pos, PlantGridState.BasePosition);
            for (int i = 1; i < snaps.Count; i++) {
                float d = DistanceXZ(snaps[i].pos, PlantGridState.BasePosition);
                if (d < bestDist) { bestDist = d; best = snaps[i]; }
            }
            return best;
        }

        // ── Geometry helpers ───────────────────────────────────────────────────

        private static Vector3 Flatten(Vector3 v) {
            v.y = 0f;
            return v;
        }

        private static float DistanceXZ(Vector3 a, Vector3 b) {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static float FlatSqrDistance(Vector3 a, Vector3 b) {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        // ── Scanning ───────────────────────────────────────────────────────────

        /// <summary>
        /// Fill _nearbyPlants with every plant root within <paramref name="radius"/>, sorted by
        /// horizontal distance. Returns the count.
        ///
        /// Deliberately uncapped: the occupancy set is built from the whole list, and truncating it
        /// would report cells outside the kept set as free and place plants on top of them. Only the
        /// O(n^2) pair search reads a capped prefix (MaxPrimaries).
        /// </summary>
        private static int ScanForPlants(Vector3 origin, float radius, string plantName) {
            _nearbyPlants.Clear();
            _scanSeen.Clear();

            int hits = Physics.OverlapSphereNonAlloc(origin, radius, _scanBuffer, ScanMask);
            for (int i = 0; i < hits; i++) {
                Collider c = _scanBuffer[i];
                if (c == null) continue;
                if (c.gameObject.layer == PlantDefinitions.GhostLayer) continue;
                if (c.GetComponent<Plant>() == null) continue;
                if (!ValConfig.EnableSnappingToOtherPlants.Value && Utils.GetPrefabName(c.gameObject) != plantName) continue;

                Transform root = c.transform.root;
                // OverlapSphere is a 3D test; re-check horizontally so a plant further down a slope
                // is not dropped despite being within range on the ground plane.
                if (DistanceXZ(root.position, origin) > radius) continue;
                if (_scanSeen.Add(root)) { _nearbyPlants.Add(root); }
            }

            // Sort so the anchor and the pair search are stable. OverlapSphere does not guarantee a
            // stable order, and picking anchors out of an unordered list made the grid jitter.
            _nearbyPlants.Sort((a, b) => {
                int cmp = FlatSqrDistance(a.position, origin).CompareTo(FlatSqrDistance(b.position, origin));
                if (cmp != 0) return cmp;
                cmp = a.position.x.CompareTo(b.position.x);
                return cmp != 0 ? cmp : a.position.z.CompareTo(b.position.z);
            });
            return _nearbyPlants.Count;
        }

        private static Transform SortByDistance(List<Transform> list, Vector3 origin) {
            Transform best = list[0];
            float current_distance = float.MaxValue;
            foreach (Transform t in list) {
                float distance = DistanceXZ(t.position, origin);
                if (distance < current_distance) {
                    best = t;
                    current_distance = distance;
                }
            }
            return best;
        }
    }
}
