using HarmonyLib;
using ImpactfulSkills.common;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace ImpactfulSkills.modules {
    internal static class PlantGrid {
        internal static List<PlantGhost> GhostPlacementGrid = new List<PlantGhost>();
        internal static bool GridPlantingActive = false;
        internal static Dictionary<string, Plantable> PlantableDefinitions = new Dictionary<string, Plantable>();
        private static int plantSpaceMask = 0;
        private static int GhostLayer = 0;
        internal static float Spacing = 0;
        internal static Quaternion OriginalRotation = Quaternion.identity;

        internal class PlantGhost {
            public GameObject Ghost { get; set; }
        }

        internal class Plantable {
            public float GrowRadius { get; set; }
            public GameObject Refgo { get; set; }
            public List<Piece.Requirement> Seeds { get; set; }
        }

        /// <summary>Build all plantable information when ZNetScene is ready</summary>
        [HarmonyPriority(Priority.VeryLow)]
        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        public static class Patch_ZNetScene_Awake {
            private static void Postfix() {
                BuildPlantRequirements();
            }
        }

        private static void BuildPlantRequirements() {
            PlantableDefinitions.Clear();
            if (ZNetScene.instance == null || ZNetScene.instance.m_prefabs == null) {
                Logger.LogWarning("ZNetScene not ready for plant definitions");
                return;
            }

            foreach (GameObject obj in ZNetScene.instance.m_prefabs) {
                Plant plant = obj.GetComponent<Plant>();
                if (plant == null || PlantableDefinitions.ContainsKey(obj.name)) {
                    continue;
                }
                List<Piece.Requirement> seedItems = new List<Piece.Requirement>();
                Piece piece = obj.GetComponent<Piece>();
                if (piece != null) {
                    foreach(Piece.Requirement req in piece.m_resources) {
                        seedItems.Add(req);
                    }
                }
                PlantableDefinitions.Add(obj.name, new Plantable() { GrowRadius = plant.m_growRadius, Refgo = obj, Seeds = seedItems });
                foreach (GameObject grownPlant in plant.m_grownPrefabs) {
                    if (!PlantableDefinitions.ContainsKey(grownPlant.name)) {
                        PlantableDefinitions.Add(grownPlant.name, new Plantable() { Refgo = grownPlant, GrowRadius = plant.m_growRadius });
                    }
                }
                Logger.LogDebug($"Added plant cache entry: {obj.name}");
            }
            plantSpaceMask = LayerMask.GetMask("static_solid", "Default_small", "piece", "piece_nonsolid");
            GhostLayer = LayerMask.NameToLayer("ghost");
            Logger.LogInfo($"Loaded {PlantableDefinitions.Count} plantable definitions");
        }

        /// <summary>Hook into player's ghost setup to create multi-plant preview</summary>
        [HarmonyPatch(typeof(Player), nameof(Player.SetupPlacementGhost))]
        static class PlayerSetupPlacementGhost {
            static void Postfix(Player __instance) {
                if (!ValConfig.EnableFarmingMultiPlant.Value || __instance.m_placementGhost == null || !HoldingCultivator()) {
                    DestroyPlacementGhosts();
                    return;
                }

                if (__instance.GetSkillLevel(Skills.SkillType.Farming) < ValConfig.FarmingMultiplantRequiredLevel.Value) {
                    DestroyPlacementGhosts();
                    return;
                }

                if (!IsPlantable(__instance.m_placementGhost)) {
                    DestroyPlacementGhosts();
                    return;
                }

                CreatePlacementGhosts(__instance.m_placementGhost, __instance);
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.UpdatePlacementGhost))]
        static class AdjustPlacementGhosts {
            static void Postfix(Player __instance) {
                if (GridPlantingActive == false || HoldingCultivator() == false) {
                    return;
                }

                UpdateGridGhostPositions(__instance, __instance.m_placementGhost);
            }
        }

        private static void UpdateGridGhostPositions(Player player, GameObject placementGhost) {
            string plantName = Utils.GetPrefabName(placementGhost);
            PlantableDefinitions.TryGetValue(plantName, out Plantable plantDef);
            if (plantDef == null) {
                // TODO Safety fallback?
            }

            //Quaternion newRotation = OriginalRotation * Quaternion.Inverse(placementGhost.transform.rotation);
            //Quaternion AdjustedRotation = OriginalRotation * newRotation;

            Vector3 RowIncrementSpacing = Vector3.right * Spacing;
            Vector3 ColIncrementSpacing = Vector3.forward * Spacing;
            RowIncrementSpacing = Quaternion.AngleAxis(22.5f, Vector3.up) * RowIncrementSpacing;
            ColIncrementSpacing = Quaternion.AngleAxis(22.5f, Vector3.up) * ColIncrementSpacing;

            //Logger.LogDebug($"Rotations old:{OriginalRotation} new:{newRotation} = adjust:{AdjustedRotation} \nSpacing:{Spacing} rowspace:{RowIncrementSpacing} colspace:{ColIncrementSpacing}");

            int row = 0;
            int column = 1;
            foreach(PlantGhost pghost in GhostPlacementGrid) {
                //Vector3 currentPos = placementGhost.transform.position;
                //currentPos.x += Spacing * (column - 1);
                //currentPos.z += Spacing * row;
                //Heightmap.GetHeight(pghost.Ghost.transform.position, out float height);
                //currentPos.y = height;
                Vector3 targetPosition = placementGhost.transform.position + RowIncrementSpacing * row + ColIncrementSpacing * column;
                Heightmap.GetHeight(targetPosition, out float height);
                targetPosition.y = height;
                pghost.Ghost.transform.position = targetPosition;
                pghost.Ghost.transform.rotation = placementGhost.transform.rotation;

                // Color code valid/invalid positions
                bool isValid = IsValidPlantPosition(targetPosition, plantDef.GrowRadius);
                SetGhostVisibility(pghost.Ghost, isValid);
                column++;
                // Increment the row if the current placement is an end
                if (ValConfig.FarmingMultiplantRowCount.Value % column == 0) {
                    row++;
                    column = 1;
                }
            }
        }

        /// <summary>Calculate grid positions with snap-to-alignment if nearby plants exist</summary>
        private static void CalculateGridPositions(Player player, GameObject originalGhost, int maxToPlace, Plantable plantDef) {
            // TODO: Snap to grid here?

            OriginalRotation = originalGhost.transform.rotation;

            // Rotation-aware direction vectors
            //Vector3 rightDir = Vector3.right * Spacing;
            //Vector3 leftDir = Vector3.left * Spacing;
            //Vector3 forwardDir = Vector3.forward * Spacing;
            Vector3 RowIncrementSpacing = Vector3.forward;
            Vector3 ColIncrementSpacing = Vector3.Cross(Vector3.up, RowIncrementSpacing);
            Logger.LogDebug($"Placement original:{OriginalRotation} \nSpacing:{Spacing} rowspace:{RowIncrementSpacing} colspace:{ColIncrementSpacing}");
            int row = 0;
            int column = 1;
            for (int entry = 1; entry <= maxToPlace; entry++) {
                Vector3 targetPosition = originalGhost.transform.position + RowIncrementSpacing * row + ColIncrementSpacing * column;
                Heightmap.GetHeight(targetPosition, out float height);
                targetPosition.y = height;
                ZNetView.m_forceDisableInit = true;
                GameObject ghostGO = GameObject.Instantiate(originalGhost);
                ghostGO.name = originalGhost.name;
                ghostGO.transform.position = targetPosition;
                ZNetView.m_forceDisableInit = false;

                // Color code valid/invalid positions
                bool isValid = IsValidPlantPosition(targetPosition, plantDef.GrowRadius);
                SetGhostVisibility(ghostGO, isValid);
                GhostPlacementGrid.Add(new PlantGhost() { Ghost = ghostGO });
                column++;
                // Increment the row if the current placement is an end
                if (ValConfig.FarmingMultiplantRowCount.Value % entry == 0) {
                    row++;
                    targetPosition.z += Spacing;
                }
            }
        }

        /// <summary>Check if a position is valid for planting (not blocked by other objects or plants)</summary>
        private static bool IsValidPlantPosition(Vector3 checkPos, float growRadius) {
            Collider[] nearbyObjects = Physics.OverlapSphere(checkPos, growRadius, plantSpaceMask);
            
            // Filter out ghost pieces (they shouldn't block placement)
            foreach (Collider collider in nearbyObjects) {
                if (collider.gameObject.layer == GhostLayer) {
                    continue;
                }

                return false;
            }
            
            return true;
        }

        internal static void DestroyPlacementGhosts() {
            foreach (PlantGhost pghost in GhostPlacementGrid) {
                if (pghost.Ghost != null) {
                    GameObject.Destroy(pghost.Ghost);
                    //ZNetScene.instance.Destroy(pghost.Ghost);
                }
            }
            GhostPlacementGrid.Clear();
            GridPlantingActive = false;
        }

        internal static void CreatePlacementGhosts(GameObject placementGhost, Player player) {
            if (ValConfig.EnableFarmingMultiPlant.Value == false) {
                return;
            }

            DestroyPlacementGhosts();

            string plantName = Utils.GetPrefabName(placementGhost);
            if (!PlantableDefinitions.ContainsKey(plantName)) {
                Logger.LogWarning($"Plant {plantName} not in definitions, cannot create grid");
                return;
            }

            Plantable plantDef = PlantableDefinitions[plantName];
            Plant plantComponent = placementGhost.GetComponent<Plant>();
            if (plantComponent == null) {
                return;
            }

            int maxToPlant = Mathf.RoundToInt(ValConfig.FarmingMultiplantMaxPlantedAtOnce.Value * player.GetSkillFactor(Skills.SkillType.Farming));
            if (maxToPlant <= 1) {
                return;
            }

            Spacing = (plantDef.GrowRadius + ValConfig.FarmingMultiPlantBufferSpace.Value) * ValConfig.FarmingMultiPlantSpacingMultiplier.Value;
            
            // Calculate all grid positions
            CalculateGridPositions(player, placementGhost, maxToPlant, plantDef);

            GridPlantingActive = true;
        }

        /// <summary>Set ghost piece color based on validity</summary>
        private static void SetGhostVisibility(GameObject ghostGO, bool isValid) {
            Renderer[] renderers = ghostGO.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers) {
                foreach (Material mat in renderer.materials) {
                    if (isValid) {
                        mat.color = new Color(1, 1, 1, 1f); // normal coloring, but less vivid
                    } else {
                        mat.color = new Color(1, 0, 0, 1f); // Red for invalid
                    }
                }
            }
        }

        /// <summary>Place all plants in the grid at once</summary>
        internal static void PlantGhostsWithCosts(Player player, GameObject primaryPlantablePrefab) {
            if (!GridPlantingActive || GhostPlacementGrid.Count == 0) {
                return;
            }
            Logger.LogDebug($"Placing plants in grid");

            int plantsPlaced = 0;
            string plantName = Utils.GetPrefabName(primaryPlantablePrefab);
            Plantable plantDef = PlantableDefinitions[plantName];

            int max_plantable_with_resources = 100;
            if (plantDef.Seeds.Count > 0) {
                foreach(Piece.Requirement sedreq in plantDef.Seeds ) {
                    int available = player.m_inventory.CountItems(sedreq.m_resItem.m_itemData.m_shared.m_name);
                    int canmake = (available / sedreq.m_amount);
                    if (canmake < max_plantable_with_resources) {
                        max_plantable_with_resources = canmake;
                    }
                }
            }
            Logger.LogDebug($"Determined resources availabe will support planing up to {max_plantable_with_resources}");

            float staminaPerPlant = 10f * (ValConfig.PlantingCostStaminaReduction.Value * player.GetSkillFactor(Skills.SkillType.Farming) - 1f);
            float staminacost = 0;
            // Place plants at each valid grid position
            foreach (PlantGhost gridPos in GhostPlacementGrid) {
                if (!player.HaveStamina(staminacost + staminaPerPlant)) {
                    Logger.LogDebug($"Player does not have enough stamina to plant more, current stamina cost: {staminacost}");
                    break;
                }
                if (player.NoCostCheat() == false && max_plantable_with_resources == (plantsPlaced + 1)) {
                    Logger.LogDebug($"Player does not have the resources to plant {plantsPlaced + 1}");
                    break;
                }

                staminacost += staminaPerPlant;
                GameObject newPlant = GameObject.Instantiate(primaryPlantablePrefab, gridPos.Ghost.transform.position, primaryPlantablePrefab.transform.rotation);
                plantsPlaced++;
            }

            if (plantDef.Seeds.Count > 0) {
                Logger.LogDebug("Removing planting resource costs");
                foreach (Piece.Requirement sedreq in plantDef.Seeds) {
                    player.m_inventory.RemoveItem(sedreq.m_resItem.m_itemData.m_shared.m_name, sedreq.m_amount * plantsPlaced);
                }
            }

            Logger.LogDebug("Reducing stamina, providing planting XP.");
            player.UseStamina(staminacost);
            player.RaiseSkill(Skills.SkillType.Farming, plantsPlaced);

            return;
        }

        internal static bool HoldingCultivator() {
            if (Player.m_localPlayer == null || Player.m_localPlayer.GetRightItem() == null) {
                return false;
            }
            return Player.m_localPlayer.GetRightItem().m_shared.m_name == "$item_cultivator";
        }

        internal static bool IsPlantable(GameObject go) {
            Plant plant = go.GetComponent<Plant>();
            return plant != null;
        }

        /// <summary>Hook into Piece placement to batch place plants with multi-plant grid</summary>
        [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
        public static class PlaceMultiPlantPieces {
            private static void Postfix(Player __instance, Piece piece) {
                // Only intercept plant placements
                // If grid planting is not active, let normal placement proceed
                if (GridPlantingActive == false || IsPlantable(piece.gameObject) == false) {
                    return;
                }

                // Place all plants in the grid
                PlantGhostsWithCosts(__instance, piece.gameObject);

                DestroyPlacementGhosts();
            }
        }
    }
}
