using HarmonyLib;
using ImpactfulSkills.common;

namespace ImpactfulSkills.patches
{
    /// <summary>
    /// Past FarmingBiomeUnrestrictedLevel, a farmer's plants no longer care which biome they are in.
    ///
    /// The permission is stamped onto the plant's own ZDO the first time a qualifying player's client
    /// evaluates it, the same way beehives earn theirs (AnimalWhisper.BehivesInAnyBiome). A plant's
    /// health is worked out by whoever owns it rather than whoever planted it, so without something
    /// recorded on the plant a field would live or die depending on which player happened to be
    /// standing nearest to it.
    /// </summary>
    public static class PlantBiome
    {
        internal const string BiomeFreeFlag = "IS_ANYBIOME_PLANT";

        /// <summary>The prefab's own limits, held for the length of one UpdateHealth call.</summary>
        public struct GrowLimits {
            internal bool Applied;
            internal Heightmap.Biome Biome;
            internal bool TolerateHeat;
            internal bool TolerateCold;
        }

        /// <summary>
        /// UpdateHealth decides the biome, heat and cold questions inline and returns on the first
        /// failure, so the least invasive way in is to widen the plant's own limits for the duration of
        /// the call and put them back afterwards. Everything else the method weighs - cultivated
        /// ground, sunlight, grow space, a wall for vines - still decides the outcome, and a plant that
        /// is unhealthy for one of those reasons still reads as unhealthy.
        ///
        /// Heat and cold tolerance are relaxed alongside the biome mask on purpose: every crop prefab
        /// ships with m_tolerateHeat and m_tolerateCold off, so lifting the mask alone would still bar
        /// the Ashlands, the Mountains and the Deep North - three of the biomes "any biome" has to mean.
        /// </summary>
        [HarmonyPatch(typeof(Plant), nameof(Plant.UpdateHealth))]
        public static class PlantsGrowInAnyBiome
        {
            private static void Prefix(Plant __instance, out GrowLimits __state) {
                __state = default;
                if (!IsBiomeUnrestricted(__instance)) { return; }

                __state = new GrowLimits {
                    Applied = true,
                    Biome = __instance.m_biome,
                    TolerateHeat = __instance.m_tolerateHeat,
                    TolerateCold = __instance.m_tolerateCold,
                };
                __instance.m_biome = Heightmap.Biome.All;
                __instance.m_tolerateHeat = true;
                __instance.m_tolerateCold = true;
            }

            private static void Postfix(Plant __instance, GrowLimits __state) {
                if (!__state.Applied) { return; }
                __instance.m_biome = __state.Biome;
                __instance.m_tolerateHeat = __state.TolerateHeat;
                __instance.m_tolerateCold = __state.TolerateCold;
            }
        }

        private static bool IsBiomeUnrestricted(Plant plant) {
            if (ValConfig.EnableFarmingBiomeUnrestricted.Value == false) { return false; }
            if (plant.m_nview == null || !plant.m_nview.IsValid()) { return false; }

            ZDO zdo = plant.m_nview.GetZDO();
            if (zdo == null) { return false; }
            if (zdo.GetBool(BiomeFreeFlag, false)) { return true; }

            if (Player.m_localPlayer == null ||
                Player.m_localPlayer.GetSkillLevel(Skills.SkillType.Farming) < ValConfig.FarmingBiomeUnrestrictedLevel.Value) {
                return false;
            }

            // Only an owner's write travels to the rest of the network. A guest still gets the relaxed
            // check for its own view of the plant, and stamps it for good the moment it owns it.
            if (plant.m_nview.IsOwner()) {
                zdo.Set(BiomeFreeFlag, true);
            }
            return true;
        }
    }
}
