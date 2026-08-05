using HarmonyLib;
using ImpactfulSkills.common;
using System.Collections.Generic;
using UnityEngine;

namespace ImpactfulSkills.modules.Multiplant {

    /// Many of the classes in this file are roughly based on
    /// https://github.com/AdvizeGH/Advize_ValheimMods/tree/main/Advize_PlantEasily
    /// These are modified, but original design and implementation is credited to Advize
    /// This project uses the GNU 3.0 License also and all references to this implementation must do the same

    internal class Plantable {
        public float GrowRadius { get; set; }
        public GameObject Refgo { get; set; }
        public List<Piece.Requirement> Seeds { get; set; }
    }

    internal static class PlantDefinitions {
        internal static Dictionary<string, Plantable> PlantableDefinitions = new Dictionary<string, Plantable>();

        /// <summary>
        /// The layers a plant needs clear around it, matching Valheim's own check in
        /// Plant.HaveGrowSpace. We declare this rather than reading Plant.m_spaceMask because that
        /// is a private static which stays 0 until some plant instance happens to run its grow-space
        /// check — and an OverlapSphere against layer mask 0 matches nothing, which reports every
        /// cell as free and lets overlapping plants be placed.
        /// Initialized at declaration so it can never be read before it is assigned.
        /// </summary>
        internal static readonly int plantSpaceMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid");
        internal static readonly int GhostLayer = LayerMask.NameToLayer("ghost");

        internal static void BuildPlantRequirements() {
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
                    foreach (Piece.Requirement req in piece.m_resources) {
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
            Logger.LogInfo($"Loaded {PlantableDefinitions.Count} plantable definitions");
        }

        /// <summary>Build all plantable information when ZNetScene is ready</summary>
        [HarmonyPriority(Priority.VeryLow)]
        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        public static class Patch_ZNetScene_Awake {
            private static void Postfix() {
                BuildPlantRequirements();
            }
        }
    }
}
