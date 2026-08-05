using HarmonyLib;
using ImpactfulSkills.common;
using ImpactfulSkills.compatibility;
using System.Collections.Generic;
using UnityEngine;

namespace ImpactfulSkills.modules.Multiplant {

    /// Many of the classes in this file are roughly based on
    /// https://github.com/AdvizeGH/Advize_ValheimMods/tree/main/Advize_PlantEasily
    /// These are modified, but original design and implementation is credited to Advize
    /// This project uses the GNU 3.0 License also and all references to this implementation must do the same

    internal static class PlantGrid {
        internal static bool GridPlantingActive = false;
        internal static bool MultiplantDisabled = false;
        internal static float Spacing = 0;

        // Evaluated on demand: PreferOtherPlantGrid is server-synced, so caching this at type-init
        // meant a mid-session change never took effect.
        internal static bool UseOtherPlantGridSystem => IsOtherPlantGridSystemAvailable();


        public static bool IsOtherPlantGridSystemAvailable() {
            if (ValConfig.PreferOtherPlantGrid.Value && Modcheck.OtherFarmingGridModPresent()) {
                return true;
            }
            return false;
        }

        internal static int MaxToPlantAtOnce() {
            int maxToPlant = Mathf.RoundToInt(ValConfig.FarmingMultiplantMaxPlantedAtOnce.Value * Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Farming));
            if (maxToPlant <= 1) {
                return 1;
            }
            return maxToPlant;
        }

        internal static bool HoldingCultivator() {
            if (Player.m_localPlayer == null || Player.m_localPlayer.GetRightItem() == null) {
                return false;
            }
            return Player.m_localPlayer.GetRightItem().m_shared.m_name == "$item_cultivator";
        }

        internal static bool IsPlantable(GameObject go) {
            return go.GetComponent<Plant>() != null;
        }

        internal static void PlantGhostsWithCosts(Player player, GameObject primaryPlantablePrefab) {
            if (!GridPlantingActive || PlantGhostController.ExtraGhosts.Count == 0) {
                return;
            }
            Logger.LogDebug("Placing plants in grid");

            int plantsPlaced = 0;
            string plantName = Utils.GetPrefabName(primaryPlantablePrefab);
            Plantable plantDef = PlantDefinitions.PlantableDefinitions[plantName];

            int maxByResources = 100;
            if (plantDef.Seeds.Count > 0) {
                foreach (Piece.Requirement req in plantDef.Seeds) {
                    int available = player.m_inventory.CountItems(req.m_resItem.m_itemData.m_shared.m_name);
                    int canMake = available / req.m_amount;
                    if (canMake < maxByResources)
                        maxByResources = canMake;
                }
            }
            Logger.LogDebug($"Resources support planting up to {maxByResources}");

            float staminaPerPlant = 10f * (ValConfig.PlantingCostStaminaReduction.Value * player.GetSkillFactor(Skills.SkillType.Farming) - 1f);
            float staminaCost = 0;

            // ExtraGhosts[0..N-1] correspond to GhostValid[1..N]
            for (int i = 0; i < PlantGhostController.ExtraGhosts.Count; i++) {
                GameObject ghost = PlantGhostController.ExtraGhosts[i];
                if (!ghost.activeSelf) continue;

                int validIdx = i + 1;
                if (validIdx >= PlantGhostController.GhostValid.Count || !PlantGhostController.GhostValid[validIdx]) continue;

                if (!player.HaveStamina(staminaCost + staminaPerPlant)) {
                    Logger.LogDebug($"Not enough stamina to plant more (cost so far: {staminaCost})");
                    break;
                }
                if (!player.NoCostCheat() && maxByResources == plantsPlaced + 1) {
                    Logger.LogDebug($"Not enough resources for plant {plantsPlaced + 1}");
                    break;
                }

                staminaCost += staminaPerPlant;
                // Plant at the ghost's own rotation so the result matches the preview the player saw.
                GameObject.Instantiate(primaryPlantablePrefab, ghost.transform.position, ghost.transform.rotation);
                plantsPlaced++;
            }

            if (plantDef.Seeds.Count > 0) {
                Logger.LogDebug("Removing seed costs");
                foreach (Piece.Requirement req in plantDef.Seeds)
                    player.m_inventory.RemoveItem(req.m_resItem.m_itemData.m_shared.m_name, req.m_amount * plantsPlaced);
            }

            Logger.LogDebug("Applying stamina cost and XP.");
            player.UseStamina(staminaCost);
            player.RaiseSkill(Skills.SkillType.Farming, plantsPlaced);
        }

        [HarmonyPatch(typeof(Player), nameof(Player.SetupPlacementGhost))]
        static class PlayerSetupPlacementGhost {
            // Snapshot the rotation counter before Valheim's random-init (m_randomInitBuildRotation) can
            // scramble it inside SetupPlacementGhost. This method can fire several times per placement
            // (other mods rebuild the ghost), each re-randomizing — so we re-assert on every call.
            static void Prefix(Player __instance, out int __state) {
                __state = __instance.m_placeRotation;
            }

            static void Postfix(Player __instance, int __state) {
                if (ValConfig.EnableFarmingMultiPlant.Value == false || UseOtherPlantGridSystem ||
                    __instance.m_placementGhost == null || !HoldingCultivator()) {
                    PlantGhostController.DestroyPool();
                    PlantGridState.Clear();
                    return;
                }

                if (__instance.GetSkillLevel(Skills.SkillType.Farming) < ValConfig.FarmingMultiplantRequiredLevel.Value) {
                    PlantGhostController.DestroyPool();
                    PlantGridState.Clear();
                    return;
                }

                if (!IsPlantable(__instance.m_placementGhost)) {
                    PlantGhostController.DestroyPool();
                    PlantGridState.Clear();
                    return;
                }

                Plant plant = __instance.m_placementGhost.GetComponent<Plant>();
                if (plant != null) {
                    Spacing = plant.m_growRadius * ValConfig.FarmingMultiPlantDistanceBufferModifier.Value
                              + ValConfig.FarmingMultiPlantBufferSpace.Value;
                }

                PlantGridState.SetReferences(__instance.m_placementGhost);
                PlantGhostController.Prepare(__instance.m_placementGhost);
                PlantGhostController.BuildGrid(__instance.m_placementGhost);

                // Undo the random-init rotation this SetupPlacementGhost applied so the grid keeps its heading.
                if (ValConfig.FarmingMultiPlantPersistOrientation.Value && __instance.m_placeRotation != __state) {
                    if (ValConfig.EnableDebugMode.Value) {
                        Logger.LogDebug($"[Multiplant/setup] restoring placeRot {__instance.m_placeRotation} -> {__state}");
                    }
                    __instance.m_placeRotation = __state;
                }
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.UpdatePlacementGhost))]
        static class AdjustPlacementGhosts {
            static void Postfix() {
                if (GridPlantingActive == false || UseOtherPlantGridSystem || HoldingCultivator() == false) { return; }

                // MultiplantDisabled is driven by the configurable AOE toggle hotkey (see AOEToggle.cs).
                // It is handled inside the controller rather than by returning early here: the layout
                // collapses to a single cell, the extra ghosts are hidden, and the root ghost still
                // gets positioned and its placement status re-asserted for the post-snap position.
                PlantGridState.Update();
                PlantGhostController.Update();
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
        public static class PlaceMultiPlantPieces {
            // Snapshot the rotation counter before PlacePiece's own random-init runs.
            private static void Prefix(Player __instance, out int __state) {
                __state = __instance.m_placeRotation;
            }

            private static void Postfix(Player __instance, Piece piece, int __state) {
                if (GridPlantingActive == false || UseOtherPlantGridSystem || IsPlantable(piece.gameObject) == false) { return; }

                if (ValConfig.FarmingMultiPlantPersistOrientation.Value) {
                    // Remember the orientation we just planted with (covers snap-aligned headings), and
                    // undo the random-init rotation PlacePiece applied so the next ghost keeps the heading.
                    PlantGridState.SaveOrientation();
                    __instance.m_placeRotation = __state;
                    if (ValConfig.EnableDebugMode.Value) {
                        Logger.LogDebug($"[Multiplant/place] persist=True randomInit={piece.m_randomInitBuildRotation} " +
                            $"placeRot restored to {__state} baseYaw={PlantGridState.BaseRotation.eulerAngles.y:F0} " +
                            $"savedRow={PlantGridState.HeadingOf(PlantGridState.SavedRowDirection):F0}");
                    }
                }

                PlantGhostsWithCosts(__instance, piece.gameObject);
            }
        }
    }
}
