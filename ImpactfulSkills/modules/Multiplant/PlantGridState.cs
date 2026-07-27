using ImpactfulSkills.common;
using UnityEngine;

namespace ImpactfulSkills.modules.Multiplant {

    /// Many of the classes in this file are roughly based on
    /// https://github.com/AdvizeGH/Advize_ValheimMods/tree/main/Advize_PlantEasily
    /// These are modified, but original design and implementation is credited to Advize
    /// This project uses the GNU 3.0 License also and all references to this implementation must do the same

    /// <summary>
    /// Central per-frame state for ghost grid placement. Updated once per frame in
    /// UpdatePlacementGhost before GhostGrid positions are recalculated.
    /// RowDirection and ColumnDirection are world-space step vectors that already
    /// include the spacing magnitude — ghosts are placed at
    ///   BasePosition + RowDirection * row + ColumnDirection * col
    /// </summary>
    internal static class PlantGridState {
        // Root ghost reference
        internal static GameObject PlacementGhost;
        internal static Plant Plant;

        // Updated every frame from the placement ghost transform
        internal static Vector3 BasePosition;
        internal static Quaternion BaseRotation;
        // Rotation quantized to 90° increments, used by snap alignment
        internal static Quaternion FixedRotation;

        // World-space step vectors (magnitude == spacing). Set by SnapSystem or default directions.
        internal static Vector3 RowDirection;
        internal static Vector3 ColumnDirection;

        // Whether AltPlace is currently held (disables snapping)
        internal static bool AltPlacement;

        // Saved orientation — persisted across placements to keep grid aligned
        internal static Quaternion? SavedBaseRotation;
        internal static Vector3 SavedRowDirection;
        internal static Vector3 SavedColumnDirection;

        internal static void SetReferences(GameObject rootGhost) {
            PlacementGhost = rootGhost;
            Plant = rootGhost.GetComponent<Plant>();
        }

        internal static void Clear() {
            PlacementGhost = null;
            Plant = null;
        }

        // Diagnostics — only used when EnableDebugMode is on
        private static bool _snapEngaged;
        private static string _dbgSig;

        /// <summary>Run once per frame in UpdatePlacementGhost before ghost positions are updated.</summary>
        internal static void Update() {
            if (PlacementGhost == null) return;

            AltPlacement = ZInput.GetButton("AltPlace");
            BasePosition = PlacementGhost.transform.position;
            BaseRotation = PlacementGhost.transform.rotation;
            Vector3 euler = BaseRotation.eulerAngles;
            euler.y = Mathf.Round(euler.y / 90f) * 90f;
            FixedRotation = Quaternion.Euler(euler);
            UpdateDirectionsAndSnap();
            DebugTrace();
        }

        private static void UpdateDirectionsAndSnap() {
            float spacing = PlantGrid.Spacing;
            string plantName = Utils.GetPrefabName(PlacementGhost);
            _snapEngaged = false;

            // Default: Row = ghost forward, Column = ghost right — follow the actual ghost rotation freely.
            RowDirection = BaseRotation * Vector3.forward;
            ColumnDirection = BaseRotation * Vector3.right;

            if (ValConfig.FarmingMultiPlantSnapToExisting.Value && !AltPlacement) {
                // SnapSystem will set RowDirection, ColumnDirection, and BasePosition if a snap is found
                if (SnapSystem.FindSnapPoints(plantName, spacing)) { _snapEngaged = true; return; }
            }

            // No snap. If the player has rotated the ghost since the orientation was saved, they are
            // intentionally re-aiming — drop the saved orientation so the new facing takes over.
            if (OrientationRotatedAway()) { ResetSavedOrientation(); }

            // Reuse the orientation saved at the last placement so rows stay aligned across placements
            // (the ghost's yaw is randomized by Valheim after each plant). Skip while AltPlace is held
            // (free placement) or when the feature is disabled.
            bool useSaved = ValConfig.FarmingMultiPlantPersistOrientation.Value
                            && !AltPlacement
                            && SavedRowDirection != Vector3.zero;
            if (useSaved) {
                RowDirection = SavedRowDirection * spacing;
                ColumnDirection = SavedColumnDirection * spacing;
            } else {
                RowDirection *= spacing;
                ColumnDirection *= spacing;
            }
        }

        /// <summary>Capture the current grid orientation so subsequent placements can reuse it.</summary>
        internal static void SaveOrientation() {
            if (RowDirection.sqrMagnitude < 1e-4f || ColumnDirection.sqrMagnitude < 1e-4f) { return; }
            SavedRowDirection = RowDirection.normalized;
            SavedColumnDirection = ColumnDirection.normalized;
            SavedBaseRotation = BaseRotation;
        }

        /// <summary>
        /// True once the player has rotated the ghost more than half a rotate step (≈11°, one scroll
        /// notch of Valheim's 22.5° step) away from the orientation that was saved — i.e. an intentional
        /// re-aim rather than the post-placement random-rotation jitter (which we cancel out).
        /// </summary>
        internal static bool OrientationRotatedAway() {
            return SavedBaseRotation.HasValue && Quaternion.Angle(BaseRotation, SavedBaseRotation.Value) > 11f;
        }

        internal static void ResetSavedOrientation() {
            SavedBaseRotation = null;
            SavedRowDirection = Vector3.zero;
            SavedColumnDirection = Vector3.zero;
        }

        // Horizontal heading of a step vector, in degrees (0 = +Z / north).
        internal static float HeadingOf(Vector3 v) {
            v.y = 0;
            return v.sqrMagnitude < 1e-6f ? 0f : Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
        }

        // Logs the orientation decision only when it changes, so a short repro produces a readable trace.
        private static void DebugTrace() {
            if (!ValConfig.EnableDebugMode.Value) { return; }
            string saved = SavedRowDirection == Vector3.zero ? "none" : $"{HeadingOf(SavedRowDirection):F0}";
            string sig = $"snap={_snapEngaged} alt={AltPlacement} ghostYaw={BaseRotation.eulerAngles.y:F0} " +
                         $"row={HeadingOf(RowDirection):F0} saved={saved} rotAway={OrientationRotatedAway()}";
            if (sig == _dbgSig) { return; }
            _dbgSig = sig;
            Logger.LogDebug($"[Multiplant/orient] {sig}");
        }
    }
}
