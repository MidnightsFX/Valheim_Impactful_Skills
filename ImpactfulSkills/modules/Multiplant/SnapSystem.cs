using ImpactfulSkills.common;
using System.Collections.Generic;
using UnityEngine;

namespace ImpactfulSkills.modules.Multiplant {

    /// Many of the classes in this file are roughly based on
    /// https://github.com/AdvizeGH/Advize_ValheimMods/tree/main/Advize_PlantEasily
    /// These are modified, but original design and implementation is credited to Advize
    /// This project uses the GNU 3.0 License also and all references to this implementation must do the same


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
    /// Snaps toward the nearest plant, aligning the grid direction to cardinal axes.
    /// </summary>
    internal static class SnapSystem {
        // Includes "Default" so placed plants (which use Default layer in Valheim) are found
        private static readonly int _scanMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid");
        private const int MaxPrimaries = 8;

        /// <summary>
        /// Try to snap. On success sets PlantGridState.BasePosition, RowDirection, ColumnDirection
        /// and returns true.
        ///
        /// "Grid" style (default) detects the lattice implied by the surrounding plants and snaps the
        /// ghost to the nearest free cell of that lattice — so it never lands diagonally/off-phase when
        /// multiple plants are nearby. "Legacy" keeps the original nearest-plant behavior.
        /// </summary>
        internal static bool FindSnapPoints(string plantName, float pieceSpacing) {
            if (ValConfig.FarmingSnapStyle?.Value == "Legacy") {
                return TryFreeSnap(plantName, pieceSpacing);
            }
            return TryGridSnap(plantName, pieceSpacing);
        }

        // ── Free snap ──────────────────────────────────────────────────────────

        private static bool TryFreeSnap(string plantName, float pieceSpacing) {
            List<Transform> primaries = ScanForPlants(PlantGridState.BasePosition, ValConfig.PlantingSnapDistance.Value, plantName);
            if (primaries.Count == 0) return false;
            Transform nearest = SortByDistance(primaries, PlantGridState.BasePosition);
            ComputeFreeDirections(nearest.position, pieceSpacing);

            List<SnapPoint> snapPoints = new List<SnapPoint>();
            if (!GenerateCandidates(snapPoints, nearest.position)) return false;

            SnapPoint snap = FindNearestEuclidean(snapPoints);
            CommitSnap(snap);
            PlantGridState.RowDirection = ChooseDirection(snap.pos, PlantGridState.RowDirection);
            PlantGridState.ColumnDirection = ChooseDirection(snap.pos, PlantGridState.ColumnDirection);
            return true;
        }

        private static void ComputeFreeDirections(Vector3 target, float pieceSpacing) {
            Vector3 dir = PlantGridState.BasePosition - target;
            dir.y = 0;

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

        // Holds one candidate anchor's lattice (its own spacing/axes) and the cell the cursor projects to.
        private class GridAnchor {
            internal Vector3 pos;      // anchor world position (lattice origin)
            internal Vector3 axisRow;  // unit, horizontal
            internal Vector3 axisCol;  // unit, horizontal, perpendicular to axisRow
            internal float spacing;    // anchor's own grid spacing
            internal int cRow, cCol;   // integer cell the cursor projects to on this lattice
            internal Vector3 idealPos; // world position of that cell (before occupancy resolution)
        }

        // How many cells out to search for a free cell when the projected cell is occupied.
        private const int CellSearchRadius = 3;

        /// <summary>
        /// Detect the lattice implied by the surrounding plants and snap the ghost onto the nearest
        /// free cell of it. Unlike the Legacy path (which only offsets ±one cell from the single nearest
        /// plant), this projects the cursor onto a plant's grid, so the result is always on-grid — never
        /// diagonal/off-phase relative to that plant — and can jump over occupied cells.
        /// </summary>
        private static bool TryGridSnap(string plantName, float heldSpacing) {
            Vector3 cursor = PlantGridState.BasePosition;
            List<Transform> primaries = ScanForPlants(cursor, ValConfig.PlantingSnapDistance.Value, plantName);
            if (primaries.Count == 0) return false;

            // Pick the anchor whose lattice puts a plant closest to the cursor. This is what fixes the
            // multi-plant diagonal: two out-of-phase patches no longer flip-flop by raw distance — we
            // choose the phase that actually aligns nearest to where the player is pointing.
            GridAnchor best = null;
            float bestSqr = float.MaxValue;
            foreach (Transform t in primaries) {
                float spacing = SpacingForAnchor(t, heldSpacing);
                if (spacing <= 0.001f) continue;

                Vector3 anchorPos = t.position;
                Vector3 axisRow = AxisRowFor(anchorPos, spacing, primaries);
                Vector3 axisCol = Vector3.Cross(Vector3.up, axisRow);

                Vector3 delta = cursor - anchorPos;
                delta.y = 0;
                int cRow = Mathf.RoundToInt(Vector3.Dot(delta, axisRow) / spacing);
                int cCol = Mathf.RoundToInt(Vector3.Dot(delta, axisCol) / spacing);
                Vector3 idealPos = anchorPos + axisRow * (spacing * cRow) + axisCol * (spacing * cCol);

                float sqr = (idealPos - cursor).sqrMagnitude;
                if (sqr < bestSqr) {
                    bestSqr = sqr;
                    best = new GridAnchor {
                        pos = anchorPos, axisRow = axisRow, axisCol = axisCol,
                        spacing = spacing, cRow = cRow, cCol = cCol, idealPos = idealPos,
                    };
                }
            }
            if (best == null) return false;

            if (!TryFindFreeCell(best, cursor, out Vector3 snapped)) return false;

            // Extra ghosts extend at the HELD plant's spacing (keeps the planted block internally healthy);
            // only the corner is aligned to the anchor's lattice.
            PlantGridState.RowDirection = best.axisRow * heldSpacing;
            PlantGridState.ColumnDirection = best.axisCol * heldSpacing;
            CommitSnap(new SnapPoint(snapped, PlantGridState.RowDirection, PlantGridState.ColumnDirection, best.pos));
            PlantGridState.RowDirection = ChooseDirection(snapped, PlantGridState.RowDirection);
            PlantGridState.ColumnDirection = ChooseDirection(snapped, PlantGridState.ColumnDirection);
            return true;
        }

        // Spacing an anchor's own patch was built with. Mirrors PlantGrid.Spacing so a same-species anchor
        // yields heldSpacing, while a different species (EnableSnappingToOtherPlants) uses its own radius.
        private static float SpacingForAnchor(Transform anchorRoot, float fallback) {
            Plant p = anchorRoot.GetComponentInChildren<Plant>();
            if (p == null) return fallback;
            return p.m_growRadius * ValConfig.FarmingMultiPlantDistanceBufferModifier.Value
                   + ValConfig.FarmingMultiPlantBufferSpace.Value;
        }

        // Orientation of the anchor's lattice: the direction to its nearest axis-adjacent neighbour
        // (distance ≈ one spacing) reveals a real grid axis, including rotated patches. Diagonal
        // neighbours sit at ~1.41× spacing and are excluded by the tolerance window; isolated anchors
        // fall back to the ghost's quantized (world-cardinal) rotation.
        private static Vector3 AxisRowFor(Vector3 anchorPos, float spacing, List<Transform> primaries) {
            Vector3 bestDir = Vector3.zero;
            float bestDist = float.MaxValue;
            float lo = spacing * 0.6f, hi = spacing * 1.4f;
            foreach (Transform t in primaries) {
                Vector3 d = t.position - anchorPos;
                d.y = 0;
                float dist = d.magnitude;
                if (dist < 0.001f || dist < lo || dist > hi) continue;
                if (dist < bestDist) { bestDist = dist; bestDir = d / dist; }
            }
            if (bestDir == Vector3.zero) {
                bestDir = PlantGridState.FixedRotation * Vector3.forward;
                bestDir.y = 0;
            }
            if (bestDir.sqrMagnitude < 1e-4f) return Vector3.forward;
            bestDir.Normalize();
            return bestDir;
        }

        // Nearest free cell to the cursor, searched outward from the projected cell. Returns false if
        // every cell in range is occupied (caller then leaves the ghost unsnapped).
        private static bool TryFindFreeCell(GridAnchor a, Vector3 cursor, out Vector3 result) {
            result = Vector3.zero;
            float bestSqr = float.MaxValue;
            bool found = false;
            for (int dr = -CellSearchRadius; dr <= CellSearchRadius; dr++) {
                for (int dc = -CellSearchRadius; dc <= CellSearchRadius; dc++) {
                    Vector3 pos = a.pos
                        + a.axisRow * (a.spacing * (a.cRow + dr))
                        + a.axisCol * (a.spacing * (a.cCol + dc));
                    if (PositionHasCollisions(pos)) continue;
                    float sqr = (pos - cursor).sqrMagnitude;
                    if (sqr < bestSqr) { bestSqr = sqr; result = pos; found = true; }
                }
            }
            return found;
        }

        // ── Candidate generation ───────────────────────────────────────────────

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
                if (PositionHasCollisions(pos)) continue;

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
            float bestSqr = (best.pos - PlantGridState.BasePosition).sqrMagnitude;
            for (int i = 1; i < snaps.Count; i++) {
                float d = (snaps[i].pos - PlantGridState.BasePosition).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = snaps[i]; }
            }
            return best;
        }

        // ── Physics ────────────────────────────────────────────────────────────

        // Point-occupancy check: is this position blocked by any existing piece/plant?
        private static bool PositionHasCollisions(Vector3 pos) =>
            Physics.CheckCapsule(pos, pos + Vector3.up * 0.1f, Mathf.Epsilon, _scanMask);

        // Pick direction or its opposite — whichever has free space
        private static Vector3 ChooseDirection(Vector3 origin, Vector3 direction) {
            if (!PositionHasCollisions(origin + direction)) return direction;
            if (!PositionHasCollisions(origin - direction)) return -direction;
            return direction;
        }

        // ── Scanning ───────────────────────────────────────────────────────────

        private static List<Transform> ScanForPlants(Vector3 origin, float radius, string plantName) {
            Collider[] hits = Physics.OverlapSphere(origin, radius, _scanMask);
            List<Transform> results = new List<Transform>();
            HashSet<Transform> seen = new HashSet<Transform>();

            foreach (Collider c in hits) {
                if (c.gameObject.layer == PlantDefinitions.GhostLayer) continue;
                if (c.GetComponent<Plant>() == null) continue;
                if (!ValConfig.EnableSnappingToOtherPlants.Value && Utils.GetPrefabName(c.gameObject) != plantName) continue;

                Transform root = c.transform.root;
                if (seen.Add(root)) {
                    results.Add(root);
                    if (results.Count >= MaxPrimaries) break;
                }
            }
            return results;
        }

        private static Transform SortByDistance(List<Transform> list, Vector3 origin) {
            Transform best = list[0];
            float current_distance = 9999f;
            foreach (Transform t in list) {
                float distance = t.localPosition.DistanceTo(origin);
                if (distance < current_distance) {
                    best = t;
                    current_distance = distance;
                }
            }
            return best;
        }
    }
}
