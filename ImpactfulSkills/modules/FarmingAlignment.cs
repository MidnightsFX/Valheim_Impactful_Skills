using HarmonyLib;
using ImpactfulSkills.common;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace ImpactfulSkills.modules {
    internal static class PlantGrid {
        internal static List<GameObject> AllGhostPlancements = new List<GameObject>();
        private static List<Vector3> CurrentGridPositions = new List<Vector3>();
        internal static bool GridPlantingActive = false;
        internal static Dictionary<string, Plantable> PlantableDefinitions = new Dictionary<string, Plantable>();
        private static int plantSpaceMask = 0;

        internal class Plantable {
            public float GrowRadius { get; set; }
            public GameObject Refgo { get; set; }
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
                PlantableDefinitions.Add(obj.name, new Plantable() { GrowRadius = plant.m_growRadius, Refgo = obj });
                foreach (GameObject grownPlant in plant.m_grownPrefabs) {
                    if (!PlantableDefinitions.ContainsKey(grownPlant.name)) {
                        PlantableDefinitions.Add(grownPlant.name, new Plantable() { Refgo = grownPlant, GrowRadius = plant.m_growRadius });
                    }
                }
            }
            plantSpaceMask = LayerMask.GetMask("static_solid", "Default_small", "piece", "piece_nonsolid");
            Logger.LogInfo($"Loaded {PlantableDefinitions.Count} plantable definitions");
        }

        /// <summary>Hook into player's ghost setup to create multi-plant preview</summary>
        [HarmonyPatch(typeof(Player), nameof(Player.SetupPlacementGhost))]
        static class PlayerSetupPlacementGhost {
            static void Postfix(Player __instance, GameObject ___m_placementGhost) {
                if (!ValConfig.EnableFarmingMultiPlant.Value || ___m_placementGhost == null || !HoldingCultivator()) {
                    DestroyPlacementGhosts();
                    return;
                }

                if (__instance.GetSkillLevel(Skills.SkillType.Farming) < ValConfig.FarmingMultiplantRequiredLevel.Value) {
                    DestroyPlacementGhosts();
                    return;
                }

                if (!IsPlantable(___m_placementGhost)) {
                    DestroyPlacementGhosts();
                    return;
                }

                CreatePlacementGhosts(___m_placementGhost, __instance);
            }
        }

        /// <summary>Calculate grid positions with snap-to-alignment if nearby plants exist</summary>
        private static void CalculateGridPositions(GameObject originalGhost, int maxToPlace, float spacing, Plant plantType) {
            Vector3 originPos = originalGhost.transform.position;
            Quaternion rotation = originalGhost.transform.rotation;

            // TODO: Snap to grid here?

            // Rotation-aware direction vectors
            Vector3 rightDir = rotation * Vector3.right * spacing;
            Vector3 leftDir = rotation * Vector3.left * spacing;
            Vector3 forwardDir = rotation * Vector3.forward * spacing;

            int row = 0;
            Vector3 currentPos = originPos;
            for (int entry = 0; entry <= maxToPlace; entry++) {
                if (row % 2 == 0) {
                    currentPos += leftDir;
                } else {
                    currentPos += rightDir;
                }
                currentPos.y = ZoneSystem.instance.GetGroundHeight(currentPos);
                CurrentGridPositions.Add(currentPos);

                if (ValConfig.FarmingMultiplantRowCount.Value % entry == 0) {
                    row++;
                    currentPos += forwardDir;
                }
            }
        }

        /// <summary>Check if a position is valid for planting (not blocked by other objects or plants)</summary>
        private static bool IsValidPlantPosition(Vector3 checkPos, float growRadius) {
            Collider[] nearbyObjects = Physics.OverlapSphere(checkPos, growRadius, plantSpaceMask);
            
            // Filter out ghost pieces (they shouldn't block placement)
            foreach (Collider collider in nearbyObjects) {
                if (collider.CompareTag("ghost")) {
                    continue;
                }
                
                // Check if it's a plant or other blocking piece
                if (collider.GetComponent<Plant>() != null || collider.GetComponent<Piece>() != null) {
                    return false;
                }
            }
            
            return true;
        }

        internal static void DestroyPlacementGhosts() {
            foreach (GameObject ghost in AllGhostPlancements) {
                if (ghost != null) {
                    ZNetScene.instance.Destroy(ghost);
                }
            }
            AllGhostPlancements.Clear();
            CurrentGridPositions.Clear();
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

            float spacing = (plantDef.GrowRadius + ValConfig.FarmingMultiPlantBufferSpace.Value) * ValConfig.FarmingMultiPlantSpacingMultiplier.Value;
            
            // Calculate all grid positions
            CalculateGridPositions(placementGhost, maxToPlant, spacing, plantComponent);

            // Create ghost pieces for each position
            for (int i = 0; i < CurrentGridPositions.Count; i++) {
                ZNetView.m_forceDisableInit = true;
                GameObject ghostGO = GameObject.Instantiate(placementGhost);
                ghostGO.name = placementGhost.name;
                ZNetView.m_forceDisableInit = false;

                ghostGO.transform.position = CurrentGridPositions[i];
                
                // Color code valid/invalid positions
                bool isValid = IsValidPlantPosition(CurrentGridPositions[i], plantDef.GrowRadius);
                SetGhostVisibility(ghostGO, isValid);
                
                AllGhostPlancements.Add(ghostGO);
            }

            GridPlantingActive = true;
        }

        /// <summary>Set ghost piece color based on validity</summary>
        private static void SetGhostVisibility(GameObject ghostGO, bool isValid) {
            Renderer[] renderers = ghostGO.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers) {
                foreach (Material mat in renderer.materials) {
                    if (isValid) {
                        mat.color = new Color(0, 1, 0, 0.5f); // Green for valid
                    } else {
                        mat.color = new Color(1, 0, 0, 0.5f); // Red for invalid
                    }
                }
            }
        }

        /// <summary>Place all plants in the grid at once</summary>
        internal static int BuildPlantGrid(Player player, GameObject primaryPlantablePrefab) {
            if (!GridPlantingActive || CurrentGridPositions.Count == 0) {
                return 0;
            }

            int plantsPlaced = 0;
            string plantName = Utils.GetPrefabName(primaryPlantablePrefab);
            Plantable plantDef = PlantableDefinitions[plantName];
            float grow_radius;
            if (plantDef == null) {
                grow_radius = primaryPlantablePrefab.GetComponent<Plant>().m_growRadius;
            } else {
                grow_radius = plantDef.GrowRadius;
            }

            // Place plants at each valid grid position
            foreach (Vector3 gridPos in CurrentGridPositions) {
                if (!IsValidPlantPosition(gridPos, grow_radius)) {
                    continue; // Skip invalid positions
                }
                GameObject newPlant = GameObject.Instantiate(primaryPlantablePrefab, gridPos, primaryPlantablePrefab.transform.rotation);
                plantsPlaced++;
            }

            return plantsPlaced;
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

                // Check if player has enough resources for the entire grid
                // TODO: Allow partial planting if the player doesn't have enough
                Logger.LogDebug($"Checking if player can afford multi-placement {CurrentGridPositions.Count}");
                if (!CanAffordGridPlanting(__instance, piece.gameObject, CurrentGridPositions)) {
                    return;
                }

                // Place all plants in the grid
                Logger.LogDebug($"Placing plants in grid");
                int plantsPlaced = BuildPlantGrid(__instance, piece.gameObject);

                // Deduct cumulative resources
                float staminaPerPlant = 10f * (ValConfig.PlantingCostStaminaReduction.Value * __instance.GetSkillFactor(Skills.SkillType.Farming) - 1f);
                __instance.UseStamina(staminaPerPlant * plantsPlaced);
                __instance.RaiseSkill(Skills.SkillType.Farming, plantsPlaced);

                // Log the placement
                Logger.LogInfo($"Multi-planted {plantsPlaced} crops using farming alignment grid");

                DestroyPlacementGhosts();
            }
        }

        /// <summary>Check if player can afford to plant entire grid</summary>
        private static bool CanAffordGridPlanting(Player player, GameObject plantPrefab, List<Vector3> gridPositions) {
            if (gridPositions.Count == 0) {
                return false;
            }

            // TODO: define the currently selected prefab to avoid needing to do prefab name lookups
            string plantName = Utils.GetPrefabName(plantPrefab);
            if (!PlantableDefinitions.ContainsKey(plantName)) {
                return false;
            }

            Plantable plantDef = PlantableDefinitions[plantName];
            
            // Count valid positions
            int validPositions = 0;
            foreach (Vector3 pos in gridPositions) {
                if (IsValidPlantPosition(pos, plantDef.GrowRadius)) {
                    validPositions++;
                }
            }

            if (validPositions == 0) {
                return false;
            }

            // Check stamina (each plant costs stamina to cultivate)
            float staminaPerPlant = 10f; // approximate cultivator stamina cost
            float totalStaminaCost = validPositions * staminaPerPlant;
            
            if (player.GetStamina() < totalStaminaCost) {
                return false;
            }

            // TODO: Check for seed resources if needed
            // This depends on how Valheim handles seed consumption for plants

            return true;
        }
    }
}
