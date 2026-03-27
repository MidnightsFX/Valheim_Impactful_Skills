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
        internal bool isCardinal;

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
    /// and aligned grid directions onto PlantGridState. Closely mirrors PlantEasily's SnapSystem.
    ///
    /// Grid snap:  detects an existing grid from neighbour pairs, generates ±row/±col candidates.
    /// Free snap:  falls back to aligning toward nearby plants (quantized to 22.5° steps).
    /// </summary>
    internal static class SnapSystem {
        // Includes "Default" so placed plants (which use Default layer in Valheim) are found
        private static readonly int _scanMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid");
        private const int MaxPrimaries = 8;

        private static SnapPoint _lastSnap;
        private static bool _hasLastSnap;
        private static Quaternion _lastGhostRotation;

        internal static void ResetSnap() {
            _lastSnap = null;
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
            if (TryGridSnap(plantName, pieceSpacing)) return true;
            if (TryFreeSnap(plantName, pieceSpacing)) return true;
            return false;
        }

        // ── Grid snap ──────────────────────────────────────────────────────────

        private static bool TryGridSnap(string plantName, float pieceSpacing) {
            List<Transform> primaries = ScanForPlants(PlantGridState.BasePosition, ValConfig.PlantingSnapDistance.Value, plantName);
            SortByDistanceSqr(primaries, PlantGridState.BasePosition);

            List<SnapPoint> snapPoints = new List<SnapPoint>();

            foreach (Transform primary in primaries) {
                List<Transform> neighbours = ScanNeighbours(primary, pieceSpacing, plantName);
                bool found = false;

                foreach (Transform neighbour in neighbours) {
                    if (!TryDetectGrid(primary.position, neighbour.position, pieceSpacing))
                        continue;

                    if (GenerateCandidates(snapPoints, primary.position, gridDetected: true)) {
                        found = true;
                        break;
                    }
                }

                if (found) break;
            }

            if (snapPoints.Count == 0) return false;

            ApplyRotationHysteresis();
            SnapPoint nearest = ApplyPositionHysteresis(FindNearestManhattan(snapPoints));
            CommitSnap(nearest);
            ApplyGridOrientation(nearest);
            return true;
        }

        private static bool TryDetectGrid(Vector3 primary, Vector3 neighbour, float pieceSpacing) {
            Vector3 delta = neighbour - primary;
            delta.y = 0;
            if (delta.sqrMagnitude < 0.000001f) return false;

            Vector3 dir = PlantGridState.FixedRotation * delta.normalized;
            PlantGridState.RowDirection = dir * pieceSpacing;
            PlantGridState.ColumnDirection = Vector3.Cross(Vector3.up, dir).normalized * pieceSpacing;
            return true;
        }

        // ── Free snap ──────────────────────────────────────────────────────────

        private static bool TryFreeSnap(string plantName, float pieceSpacing) {
            List<Transform> primaries = ScanForPlants(PlantGridState.BasePosition, ValConfig.PlantingSnapDistance.Value, plantName);
            SortByDistanceSqr(primaries, PlantGridState.BasePosition);

            List<SnapPoint> snapPoints = new List<SnapPoint>();

            foreach (Transform primary in primaries) {
                ComputeFreeDirections(primary.position, pieceSpacing);
                if (GenerateCandidates(snapPoints, primary.position, gridDetected: false)) break;
            }

            if (snapPoints.Count == 0) return false;

            SnapPoint nearest = FindNearestEuclidean(snapPoints);
            CommitSnap(nearest);
            PlantGridState.RowDirection = ChooseDirection(nearest.pos, PlantGridState.RowDirection);
            PlantGridState.ColumnDirection = ChooseDirection(nearest.pos, PlantGridState.ColumnDirection);
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
                    dir = Quaternion.Euler(0, Mathf.Round(angle / 22.5f) * 22.5f, 0) * Vector3.forward;
                }
            }

            PlantGridState.RowDirection = PlantGridState.FixedRotation * dir * pieceSpacing;
            PlantGridState.ColumnDirection = Vector3.Cross(Vector3.up, PlantGridState.RowDirection);
        }

        // ── Candidate generation ───────────────────────────────────────────────

        private static bool GenerateCandidates(List<SnapPoint> snapPoints, Vector3 fromPos, bool gridDetected) {
            Vector3 row = PlantGridState.RowDirection;
            Vector3 col = PlantGridState.ColumnDirection;

            Vector3[] positions = gridDetected
                ? new Vector3[] {
                    fromPos + row,         fromPos - row,
                    fromPos + col,         fromPos - col,
                    fromPos + row + col,   fromPos + row - col,
                    fromPos - row + col,   fromPos - row - col,
                }
                : new Vector3[] {
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

        // ── Commit & orientation ───────────────────────────────────────────────

        private static void CommitSnap(SnapPoint snap) {
            _lastSnap = snap;
            _hasLastSnap = true;
            _lastGhostRotation = PlantGridState.PlacementGhost.transform.rotation;

            if (PlantGridState.SavedBaseRotation == null)
                PlantGridState.SavedBaseRotation = _lastGhostRotation;

            PlantGridState.BasePosition = PlantGridState.PlacementGhost.transform.position = snap.pos;
        }

        private static void ApplyGridOrientation(SnapPoint snap) {
            Vector3 row = ChooseDirection(snap.pos, snap.rowDir);
            Vector3 col = ChooseDirection(snap.pos, snap.colDir);

            if (PlantGridState.SavedRowDirection != Vector3.zero) {
                float dot = Mathf.Abs(Vector3.Dot(PlantGridState.SavedRowDirection, row.normalized));
                bool aligned = dot > 0.95f || dot < 0.05f;

                if (aligned) {
                    float spacing = row.magnitude;
                    row = ChooseDirection(snap.pos, PlantGridState.SavedRowDirection * spacing);
                    col = ChooseDirection(snap.pos, PlantGridState.SavedColumnDirection * spacing);
                } else {
                    PlantGridState.ResetSavedOrientation();
                    PlantGridState.SavedRowDirection = row.normalized;
                    PlantGridState.SavedColumnDirection = col.normalized;
                }
            } else {
                PlantGridState.SavedRowDirection = row.normalized;
                PlantGridState.SavedColumnDirection = col.normalized;
            }

            PlantGridState.RowDirection = row;
            PlantGridState.ColumnDirection = col;
        }

        // ── Hysteresis ─────────────────────────────────────────────────────────

        private static void ApplyRotationHysteresis() {
            const float threshold = 1f;
            if (_hasLastSnap && Quaternion.Angle(PlantGridState.FixedRotation, _lastGhostRotation) > threshold)
                _hasLastSnap = false;

            if (PlantGridState.SavedBaseRotation is Quaternion saved &&
                Quaternion.Angle(saved, _lastGhostRotation) > threshold)
                PlantGridState.ResetSavedOrientation();
        }

        private static SnapPoint ApplyPositionHysteresis(SnapPoint nearest) {
            const float sqrThreshold = 0.05f * 0.05f;
            if (_hasLastSnap && _lastSnap != null && _lastSnap.origin == nearest.origin) {
                float lastDist = ManhattanDist(_lastSnap.pos, _lastSnap.rowDir, _lastSnap.colDir);
                float newDist = ManhattanDist(nearest.pos, nearest.rowDir, nearest.colDir);
                if (newDist > lastDist - sqrThreshold)
                    return _lastSnap;
            }
            return nearest;
        }

        // ── Nearest selection ──────────────────────────────────────────────────

        private static SnapPoint FindNearestManhattan(List<SnapPoint> snaps) {
            SnapPoint best = snaps[0];
            float bestDist = ManhattanDist(best.pos, best.rowDir, best.colDir);
            for (int i = 1; i < snaps.Count; i++) {
                float d = ManhattanDist(snaps[i].pos, snaps[i].rowDir, snaps[i].colDir);
                if (d < bestDist) { bestDist = d; best = snaps[i]; }
            }
            return best;
        }

        private static SnapPoint FindNearestEuclidean(List<SnapPoint> snaps) {
            SnapPoint best = snaps[0];
            float bestSqr = (best.pos - PlantGridState.BasePosition).sqrMagnitude;
            for (int i = 1; i < snaps.Count; i++) {
                float d = (snaps[i].pos - PlantGridState.BasePosition).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = snaps[i]; }
            }
            return best;
        }

        private static float ManhattanDist(Vector3 snapPos, Vector3 rowDir, Vector3 colDir) {
            Vector3 delta = snapPos - PlantGridState.BasePosition;
            float r = rowDir.sqrMagnitude > 0.001f ? Mathf.Abs(Vector3.Dot(delta, rowDir.normalized)) : 0;
            float c = colDir.sqrMagnitude > 0.001f ? Mathf.Abs(Vector3.Dot(delta, colDir.normalized)) : 0;
            return r + c;
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

        private static List<Transform> ScanNeighbours(Transform primary, float expectedSpacing, string plantName) {
            float tolerance = Mathf.Max(expectedSpacing * 0.01f, 0.005f);
            float maxDist = expectedSpacing + tolerance;
            float minSqr = (expectedSpacing - tolerance) * (expectedSpacing - tolerance);
            float maxSqr = maxDist * maxDist;

            Collider[] hits = Physics.OverlapSphere(primary.position, maxDist, _scanMask);
            List<Transform> results = new List<Transform>();
            HashSet<Transform> seen = new HashSet<Transform>();

            foreach (Collider c in hits) {
                Transform root = c.transform.root;
                if (root == primary) continue;
                if (c.gameObject.layer == PlantDefinitions.GhostLayer) continue;
                if (!ValConfig.EnableSnappingToOtherPlants.Value && Utils.GetPrefabName(c.gameObject) != plantName) continue;

                float distSqr = (root.position - primary.position).sqrMagnitude;
                if (distSqr >= minSqr && distSqr <= maxSqr && seen.Add(root))
                    results.Add(root);
            }
            return results;
        }

        private static void SortByDistanceSqr(List<Transform> list, Vector3 origin) {
            list.Sort((a, b) =>
                (a.position - origin).sqrMagnitude.CompareTo((b.position - origin).sqrMagnitude));
        }
    }
}
