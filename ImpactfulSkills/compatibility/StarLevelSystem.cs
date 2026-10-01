using HarmonyLib;
using System;
using System.Reflection;

namespace ImpactfulSkills.compatibility {

    /// <summary>
    /// Star Level System lets creatures go well past the vanilla two stars, and keeps its own record of each
    /// creature's level next to the vanilla one. Animal Handling's bonus star is only capped at vanilla's two stars
    /// when it is not installed, and when it is the level is set through its API so that record is updated too.
    ///
    /// UpdateCreatureLevel writes the level on the creature's owner (relaying the call there from any other peer)
    /// along with its max health. It does not rebuild the per-level stats, size and colour Star Level System has
    /// already applied, so those catch up the next time the creature loads.
    /// </summary>
    internal static class StarLevelSystem {
        internal const string PluginGUID = "MidnightsFX.StarLevelSystem";

        private const string ReceiverType = "StarLevelSystem.modules.APIReciever";
        private const string UpdateLevelMethod = "UpdateCreatureLevel";

        private static MethodInfo update_level;
        private static bool? supported;

        internal static bool Installed => Modcheck.IsStarLevelSystemEnabled;

        private static bool Supported {
            get {
                if (supported == null) { supported = Resolve(); }
                return supported.Value;
            }
        }

        private static bool Resolve() {
            if (Installed == false) { return false; }

            try {
                Type receiver = AccessTools.TypeByName(ReceiverType);
                if (receiver != null) {
                    update_level = AccessTools.Method(receiver, UpdateLevelMethod, new Type[] { typeof(Character), typeof(int) });
                }
            } catch (Exception e) {
                Logger.LogDebug($"Star Level System lookup failed: {e.Message}");
            }

            if (update_level == null) {
                Logger.LogWarning($"Star Level System is installed but {ReceiverType}.{UpdateLevelMethod} was not found, so Animal Handling's bonus star is set on the creature directly and Star Level System may not pick it up until the creature reloads. This mod may need an update to match the installed Star Level System version.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Sets the creature's level through Star Level System. False when it is not installed or its API could not
        /// be found, in which case the caller sets the level the vanilla way.
        /// </summary>
        internal static bool SetLevel(Character chara, int level) {
            if (Supported == false) { return false; }

            try {
                return (bool)update_level.Invoke(null, new object[] { chara, level });
            } catch (Exception e) {
                Logger.LogWarning($"Star Level System could not set {chara.m_name} to level {level}: {e.InnerException?.Message ?? e.Message}");
                return false;
            }
        }
    }
}
