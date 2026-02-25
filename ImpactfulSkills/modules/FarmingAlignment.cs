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
                return; // Safety check
            }

            Vector3 gridOrigin = placementGhost.transform.position;
            // Rotation-aware direction vectors based on current placement ghost rotation
            Vector3 colIncrementSpacing = placementGhost.transform.rotation * Vector3.right * Spacing;   // Column moves right
            Vector3 rowIncrementSpacing = placementGhost.transform.rotation * Vector3.forward * Spacing;  // Row moves forward
            
            // Try to snap to a nearby existing plant of the same type
            if (TrySnapToNearbyPlants(gridOrigin, plantName, out Vector3 snappedOrigin)) {
                gridOrigin = snappedOrigin + rowIncrementSpacing;
            }

            int row = 0;
            int column = 0;
            int gridCols = ValConfig.FarmingMultiplantRowCount.Value;
            foreach(PlantGhost pghost in GhostPlacementGrid) {
                Vector3 targetPosition = gridOrigin + (colIncrementSpacing * column) + (rowIncrementSpacing * row);
                Heightmap.GetHeight(targetPosition, out float height);
                targetPosition.y = height;
                pghost.Ghost.transform.position = targetPosition;
                pghost.Ghost.transform.rotation = placementGhost.transform.rotation;

                // Color code valid/invalid positions
                bool isValid = IsValidPlantPosition(targetPosition, plantDef.GrowRadius);
                SetGhostVisibility(pghost.Ghost, isValid);
                
                // Increment column and wrap to next row
                column++;
                if (column >= gridCols) {
                    column = 0;
                    row++;
                }
            }
        }

        /// <summary>If very close to an existing plant of the same type, snap grid origin to the nearest one</summary>
        private static bool TrySnapToNearbyPlants(Vector3 originPos, string plantName, out Vector3 snappedOrigin) {
            snappedOrigin = originPos;

            if (!ValConfig.FarmingMultiPlantSnapToExisting.Value) {
                return false;
            }

            // Search for nearby plants of the same type within snap distance
            float snapDistance = ValConfig.PlantingSnapDistance.Value;
            Collider[] nearbyObjects = Physics.OverlapSphere(originPos, snapDistance, plantSpaceMask);
            
            Plant nearestPlant = null;
            float nearestDistance = float.MaxValue;

            foreach (Collider collider in nearbyObjects) {
                Plant plant = collider.GetComponent<Plant>();
                if (plant != null) {
                    // Check if it's the same type
                    string objectName = Utils.GetPrefabName(collider.gameObject);
                    if (objectName == plantName) {
                        float distanceToPlant = Vector3.Distance(originPos, plant.transform.position);
                        if (distanceToPlant < nearestDistance) {
                            nearestDistance = distanceToPlant;
                            nearestPlant = plant;
                        }
                    }
                }
            }

            if (nearestPlant != null) {
                snappedOrigin = nearestPlant.transform.position;
                Logger.LogDebug($"Snapped grid origin to nearest plant at {snappedOrigin} (distance: {nearestDistance})");
                return true;
            }

            return false;
        }

        /// <summary>Calculate grid positions with snap-to-alignment if nearby plants exist</summary>
        private static void CalculateGridPositions(Player player, GameObject originalGhost, int maxToPlace, Plantable plantDef) {
            OriginalRotation = originalGhost.transform.rotation;
            Vector3 gridOrigin = originalGhost.transform.position;
            // Rotation-aware direction vectors based on the original ghost's rotation
            Vector3 colIncrementSpacing = OriginalRotation * Vector3.right * Spacing;  // Each column moves right
            Vector3 rowIncrementSpacing = OriginalRotation * Vector3.forward * Spacing; // Each row moves forward

            // Try to snap to a nearby existing plant of the same type
            string plantName = Utils.GetPrefabName(originalGhost);

            // Try to snap to a nearby existing plant of the same type
            if (TrySnapToNearbyPlants(gridOrigin, plantName, out Vector3 snappedOrigin)) {
                gridOrigin = snappedOrigin + rowIncrementSpacing;
            }
            
            int row = 0;
            int column = 0;
            int gridCols = ValConfig.FarmingMultiplantRowCount.Value;
            for (int entry = 0; entry < maxToPlace; entry++) {
                Vector3 targetPosition = gridOrigin + (colIncrementSpacing * column) + (rowIncrementSpacing * row);
                Heightmap.GetHeight(targetPosition, out float height);
                targetPosition.y = height;
                if (entry == 0) {
                    GhostPlacementGrid.Add(new PlantGhost() { Ghost = originalGhost });
                    continue;
                }
                
                ZNetView.m_forceDisableInit = true;
                GameObject ghostGO = GameObject.Instantiate(originalGhost);
                ghostGO.name = originalGhost.name;
                ghostGO.transform.position = targetPosition;
                ghostGO.transform.rotation = originalGhost.transform.rotation;
                ZNetView.m_forceDisableInit = false;

                // Color code valid/invalid positions
                bool isValid = IsValidPlantPosition(targetPosition, plantDef.GrowRadius);
                SetGhostVisibility(ghostGO, isValid);
                GhostPlacementGrid.Add(new PlantGhost() { Ghost = ghostGO });
                
                // Increment column and wrap to next row
                column++;
                if (column >= gridCols) {
                    column = 0;
                    row++;
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
