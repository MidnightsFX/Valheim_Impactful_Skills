using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ImpactfulSkills.modules {
    internal static class PlantGrid {
        static List<GameObject>[] AllGhostPlancements = null;
        internal static bool GridPlantingActive = false;
        internal static Dictionary<string, Plantable> PlantableDefinitions = new Dictionary<string, Plantable>();
        internal static Material LineMaterial;

        internal class Plantable {
            public float ReqSpace { get; set; }
            public GameObject Refgo { get; set; }
        }

        // Build all of the required information about plantable things, so we can reference them later
        //[HarmonyPriority(Priority.VeryLow)]
        //[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        //public static class Patch_ZNetScene_Awake {
        //    private static void Postfix() {
        //        BuildPlantRequirements();
        //    }
        //    private static void BuildPlantRequirements() {
        //        PlantableDefinitions.Clear();
        //        foreach (GameObject obj in ZNetScene.instance.m_prefabs) {
        //            Plant plant = obj.GetComponent<Plant>();
        //            if (plant == null || PlantableDefinitions.ContainsKey(plant.name)) {
        //                continue;
        //            }
        //            PlantableDefinitions.Add(plant.name, new Plantable() { ReqSpace = plant.m_growRadius, Refgo = obj });
        //            foreach (GameObject grownPlant in plant.m_grownPrefabs) {
        //                if (!PlantableDefinitions.ContainsKey(grownPlant.name)) {
        //                    PlantableDefinitions.Add(grownPlant.name, new Plantable() { Refgo = grownPlant, ReqSpace = plant.m_growRadius });
        //                }
        //            }
        //        }
        //        LineMaterial = Resources.FindObjectsOfTypeAll<Material>().First((Material k) => k.name == "Default-Line");
        //    }
        //}

        //[HarmonyPatch(typeof(Player), nameof(Player.SetupPlacementGhost))]
        //static class PlayerSetupPlacementGhost {
        //    static void Postfix(Player __instance, GameObject ___m_placementGhost) {
        //        if (GridPlantingActive == false) return;
        //        DestroyPlacementGhosts();

        //        if (ValConfig.EnableFarmingMultiPlant.Value == false || __instance.GetSkillLevel(Skills.SkillType.Farming) <= ValConfig.FarmingMultiplantRequiredLevel.Value || ___m_placementGhost == null || HoldingCultivator() == false || IsPlantOrPickable(___m_placementGhost) == false) {
        //            return;
        //        }

        //        CreatePlacementGhosts(___m_placementGhost, __instance);
        //    }
        //}

        internal static void DestroyPlacementGhosts() {
            foreach (List<GameObject> golist in AllGhostPlancements) {
                for (int i = 0; i < golist.Count; i++) {
                    ZNetScene.instance.Destroy(golist[i]);
                }
            }
            AllGhostPlancements = null;
        }

        internal static void CreatePlacementGhosts(GameObject placementGhost, Player player) {
            float spacing = 3f;
            string name = Utils.GetPrefabName(placementGhost);
            if (PlantableDefinitions.ContainsKey(name)) {
                spacing = PlantableDefinitions[name].ReqSpace;
            }

            int maxtoPlant = Mathf.RoundToInt(ValConfig.FarmingMultiplantMaxPlantedAtOnce.Value * player.GetSkillFactor(Skills.SkillType.Farming));
            // No ghosts needed yet
            if (maxtoPlant <= 1) { return; }

            GridPlantingActive = true;
            int maxInRow = (ValConfig.FarmingMultiplantRowCount.Value - 1);

            List<GameObject>[] ghostRowEntries = new List<GameObject>[maxInRow];

            // Column doubles as the offset modifier for Z
            int column = 0;
            int rowTarget = 0;
            for (int plantIndex = 0; plantIndex < maxtoPlant; plantIndex++) {
                // This is the position of the main ghost entry
                if (plantIndex == 0) {
                    ghostRowEntries[column].Add(placementGhost);
                    continue;
                }
                // Rowtarget doubles as the x offset entry
                rowTarget = plantIndex % maxInRow;
                if (rowTarget == 0 && plantIndex > 1) { column++; }
                if (ghostRowEntries[rowTarget] == null) { ghostRowEntries[rowTarget] = new List<GameObject>(); }

                ZNetView.m_forceDisableInit = true;
                GameObject newEntry = GameObject.Instantiate(placementGhost);
                newEntry.name = placementGhost.name;
                ZNetView.m_forceDisableInit = false;
                // Should we check the ghosts layers?

                Vector3 offsetPostion = new Vector3(placementGhost.transform.position.x + (rowTarget * spacing), placementGhost.transform.position.y, placementGhost.transform.position.z + (column * spacing));
                newEntry.transform.position = offsetPostion;
                ghostRowEntries[rowTarget].Add(newEntry);
            }

            AllGhostPlancements = ghostRowEntries;
        }

        internal static bool HoldingCultivator() {
            if (Player.m_localPlayer == null || Player.m_localPlayer.GetRightItem() == null) { return false; }
            return Player.m_localPlayer.GetRightItem().m_shared.m_name == "$item_cultivator";
        }

        internal static bool IsPlantable(GameObject go) {
           return go.GetComponent<Plant>() != null || go.GetComponent<Pickable>() != null;
        }


    }
}
