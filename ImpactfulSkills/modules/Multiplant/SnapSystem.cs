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

        private static bool _hasLastSnap;
        private static Quaternion _lastGhostRotation;

        internal static void ResetSnap() {
            _hasLastSnap = false;
        }

        /// <summary>
        /// Returns true if the ghost has been rotated by at least half a scroll step (11.25°)
        /// since the last snap was committed — meaning the player has intentionally changed direction.
        /// </summary>
        internal static bool HasRotationChangedSinceSnap() {
            if (!_hasLastSnap) return false;
            return Quaternion.Angle(PlantGridState.FixedRotation, _lastGhostRotation) > 11f;
        }

        /// <summary>
        /// Try to snap. On success sets PlantGridState.BasePosition, RowDirection, ColumnDirection
        /// and returns true.
        /// </summary>
        internal static bool FindSnapPoints(string plantName, float pieceSpacing) {
            return TryFreeSnap(plantName, pieceSpacing);
        }

        // ── Free snap ──────────────────────────────────────────────────────────

        private static bool TryFreeSnap(string plantName, float pieceSpacing) {
            List<Transform> primaries = ScanForPlants(PlantGridState.BasePosition, ValConfig.PlantingSnapDistance.Value, plantName);
            if (primaries.Count == 0) return false;
            SortByDistanceSqr(primaries, PlantGridState.BasePosition);

            Transform nearest = primaries[0];
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
            _hasLastSnap = true;
            _lastGhostRotation = PlantGridState.PlacementGhost.transform.rotation;

            if (PlantGridState.SavedBaseRotation == null)
                PlantGridState.SavedBaseRotation = _lastGhostRotation;

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

        private static void SortByDistanceSqr(List<Transform> list, Vector3 origin) {
            list.Sort((a, b) =>
                (a.position - origin).sqrMagnitude.CompareTo((b.position - origin).sqrMagnitude));
        }
    }
}
