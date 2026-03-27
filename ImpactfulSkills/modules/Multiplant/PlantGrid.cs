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
        internal static int UserDefinedMax = ValConfig.FarmingMultiplantMaxPlantedAtOnce.Value;

        internal static bool UseOtherPlantGridSystem = IsOtherPlantGridSystemAvailable();

        // Rotation saved at placement time, restored to Valheim's m_placeRotation on next SetupPlacementGhost
        private static Quaternion? _pendingGhostRotation;

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
                GameObject.Instantiate(primaryPlantablePrefab, ghost.transform.position, primaryPlantablePrefab.transform.rotation);
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
            static void Postfix(Player __instance) {
                // Consume pending rotation regardless of path so it is never applied stale
                Quaternion? pendingRotation = _pendingGhostRotation;
                _pendingGhostRotation = null;

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

                // Restore the rotation the player had before Valheim reset m_placeRotation
                if (pendingRotation.HasValue) {
                    RestorePlaceRotation(__instance, pendingRotation.Value);
                } 
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.UpdatePlacementGhost))]
        static class AdjustPlacementGhosts {
            static void Postfix() {
                if (GridPlantingActive == false || UseOtherPlantGridSystem || HoldingCultivator() == false) { return; }

                if (Player.m_localPlayer != null && ZInput.GetButtonDown("Crouch")) {
                    MultiplantDisabled = !MultiplantDisabled;
                    string msg = MultiplantDisabled ? Localization.instance.Localize("$multi_plant_disabled") : Localization.instance.Localize("$multi_plant_enabled");
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, msg);
                }

                PlantGridState.Update();

                if (MultiplantDisabled) {
                    foreach (GameObject g in PlantGhostController.ExtraGhosts) {
                        if (g != null) { g.SetActive(false); }
                    }
                    return;
                }

                PlantGhostController.Update();
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
        public static class PlaceMultiPlantPieces {
            private static void Postfix(Player __instance, Piece piece) {
                if (GridPlantingActive == false || UseOtherPlantGridSystem || IsPlantable(piece.gameObject) == false) { return; }

                // Save rotation before Valheim resets m_placeRotation on the next SetupPlacementGhost call
                _pendingGhostRotation = PlantGridState.BaseRotation;
                PlantGhostsWithCosts(__instance, piece.gameObject);
            }
        }

        /// <summary>Write back the Y rotation to Valheim's m_placeRotation counter so
        /// UpdatePlacementGhost continues to use the saved angle on subsequent frames.</summary>
        private static void RestorePlaceRotation(Player player, Quaternion rotation) {
            player.m_placeRotation = Mathf.RoundToInt(rotation.eulerAngles.y / player.m_placeRotationDegrees);
        }
    }
}
