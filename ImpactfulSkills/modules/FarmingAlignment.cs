using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ImpactfulSkills.modules {
    internal static class PlantGrid {

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
        public static class Patch_ZNetScene_Awake {
            private static void Postfix() {
                BuildPlantRequirements();
            }
            private static void BuildPlantRequirements() {
                PlantableDefinitions.Clear();
                foreach (GameObject obj in ZNetScene.instance.m_prefabs) {
                    Plant plant = obj.GetComponent<Plant>();
                    if (plant == null || PlantableDefinitions.ContainsKey(plant.name)) {
                        continue;
                    }
                    PlantableDefinitions.Add(plant.name, new Plantable() { ReqSpace = plant.m_growRadius, Refgo = obj });
                    foreach (GameObject grownPlant in plant.m_grownPrefabs) {
                        if (!PlantableDefinitions.ContainsKey(grownPlant.name)) {
                            PlantableDefinitions.Add(grownPlant.name, new Plantable() { Refgo = grownPlant, ReqSpace = plant.m_growRadius });
                        }
                    }
                }
                LineMaterial = Resources.FindObjectsOfTypeAll<Material>().First((Material k) => k.name == "Default-Line");
            }
        }

        internal static bool HoldingCultivator() {
            if (Player.m_localPlayer == null || Player.m_localPlayer.GetRightItem() == null) { return false; }
            return Player.m_localPlayer.GetRightItem().m_shared.m_name == "$item_cultivator";
        }

        internal static bool IsPlantOrPickable(GameObject go) {
           return go.GetComponent<Plant>() != null || go.GetComponent<Pickable>() != null;
        }
    }
}
