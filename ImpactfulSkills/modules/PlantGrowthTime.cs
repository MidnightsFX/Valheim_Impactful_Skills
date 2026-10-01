using HarmonyLib;
using ImpactfulSkills.common;
using Jotunn.Managers;
using System;

namespace ImpactfulSkills.patches
{
    /// <summary>
    /// Past FarmingGrowthTimeDisplayLevel, a healthy plant's hover text says roughly how long it has left
    /// before it is fully grown.
    ///
    /// The figure is the plant's own: GetGrowTime rolls between m_growTime and m_growTimeMax from the seed
    /// stored on the plant's ZDO, and the plant time it is measured from lives on the ZDO too, so every
    /// client reads the same number. A mod that changes grow times by editing the prefab or patching either
    /// method is reflected here as well.
    ///
    /// It is an estimate rather than a countdown because the owner only checks a plant every 10 seconds,
    /// and not until it has been loaded for 10 seconds, so a plant grows a little after its time is up
    /// rather than exactly on it. Sleeping also skips the clock ahead.
    ///
    /// An unhealthy plant shows nothing. When its time comes it fails to grow, and most crops are destroyed
    /// outright, so a countdown would promise something that is not going to happen.
    /// </summary>
    public static class PlantGrowthTime
    {
        [HarmonyPatch(typeof(Plant), nameof(Plant.GetHoverText))]
        public static class ShowGrowthTimeOnHover
        {
            private static void Postfix(Plant __instance, ref string __result) {
                if (string.IsNullOrEmpty(__result) || !ShouldShow(__instance)) { return; }
                __result += "\n" + RemainingText(__instance.GetGrowTime() - __instance.TimeSincePlanted());
            }
        }

        private static bool ShouldShow(Plant plant) {
            if (ValConfig.EnableFarmingGrowthTimeDisplay.Value == false) { return false; }
            if (plant.GetStatus() != Plant.Status.Healthy) { return false; }
            if (plant.m_nview == null || !plant.m_nview.IsValid() || ZNet.instance == null) { return false; }

            return Player.m_localPlayer != null &&
                Player.m_localPlayer.GetSkillLevel(Skills.SkillType.Farming) >= ValConfig.FarmingGrowthTimeDisplayLevel.Value;
        }

        /// <summary>
        /// Whole minutes, rounded up so the last stretch reads as one minute rather than none. Seconds
        /// would be false precision given the 10 second growth check.
        /// </summary>
        private static string RemainingText(double secondsLeft) {
            if (secondsLeft <= 0) { return LocalizationManager.Instance.TryTranslate("$farming_grows_soon"); }

            int minutes = (int)Math.Ceiling(secondsLeft / 60d);
            string duration = minutes >= 60
                ? LocalizationManager.Instance.TryTranslate("$farming_time_hours").Replace("{0}", (minutes / 60).ToString()).Replace("{1}", (minutes % 60).ToString())
                : LocalizationManager.Instance.TryTranslate("$farming_time_minutes").Replace("{0}", minutes.ToString());
            return LocalizationManager.Instance.TryTranslate("$farming_grows_in").Replace("{0}", duration);
        }
    }
}
