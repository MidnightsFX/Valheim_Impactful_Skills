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
        }

        private static void UpdateDirectionsAndSnap() {
            float spacing = PlantGrid.Spacing;
            string plantName = Utils.GetPrefabName(PlacementGhost);

            // Default: derive from FixedRotation so grid direction tracks scroll input
            // and naturally persists between placements (Valheim's m_placeRotation is preserved).
            // Row = ghost forward, Column = ghost right — follow the actual ghost rotation freely
            RowDirection = BaseRotation * Vector3.forward;
            ColumnDirection = BaseRotation * Vector3.right;

            if (ValConfig.FarmingMultiPlantSnapToExisting.Value && !AltPlacement) {
                // SnapSystem will set RowDirection, ColumnDirection, and BasePosition if a snap is found
                if (SnapSystem.FindSnapPoints(plantName, spacing)) { return; }
            }

            // If the player has rotated since the last snap, the saved orientation is stale.
            if (SnapSystem.HasRotationChangedSinceSnap()) { ResetSavedOrientation(); }

            // No snap — use the saved snapped orientation if available so the grid keeps
            // its alignment after placement (newly placed plants block snap candidates).
            // Fall back to ghost rotation only when no orientation has been established yet.
            if (SavedRowDirection != Vector3.zero) {
                RowDirection = SavedRowDirection * spacing;
                ColumnDirection = SavedColumnDirection * spacing;
            } else {
                RowDirection *= spacing;
                ColumnDirection *= spacing;
            }
        }

        internal static void ResetSavedOrientation() {
            SavedBaseRotation = null;
            SavedRowDirection = Vector3.zero;
            SavedColumnDirection = Vector3.zero;
        }
    }
}
