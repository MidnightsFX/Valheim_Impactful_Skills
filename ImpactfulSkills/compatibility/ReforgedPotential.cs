using BepInEx.Configuration;
using HarmonyLib;
using ImpactfulSkills.patches;
using System;
using System.Reflection;
using UnityEngine;

namespace ImpactfulSkills.compatibility {

    /// <summary>
    /// Reforged Potential replaces the Forge of Potential's roll with its own: a return false prefix on
    /// InventoryGui.DoCrafting that ignores the idol's odds and rolls against two of its config values instead,
    /// "Upgrade Chance" and "Break Chance" (1 and 0 by default, so every refinement succeeds). A failed upgrade then
    /// makes a second, separate roll against Break Chance - so unlike vanilla, where the break chance is taken out
    /// of the same roll, its Break Chance is the chance a failure breaks the item.
    ///
    /// Forging's odds are meant to replace those fixed values, so for the one DoCrafting call ForgingRefinement
    /// writes its own odds into the two entries and puts the originals back afterwards. That only works if our
    /// DoCrafting prefixes run ahead of theirs, which they are ordered to do (HarmonyBefore). The refund is left
    /// alone here: Reforged Potential reads it off the idol like vanilla does, where ForgingRefinement already
    /// puts it.
    ///
    /// The entries are written through their backing field rather than Value. The setter raises SettingChanged,
    /// and Reforged Potential saves its config file on every change (SaveOnConfigSet), which its file watcher then
    /// reloads - twice a refinement.
    /// </summary>
    internal static class ReforgedPotential {
        internal const string PluginGUID = "akuichi.ReforgedPotential";

        private const string PluginType = "ReforgedPotential.ReforgedPotential";
        private const string UpgradeChanceField = "UpgradeChance";
        private const string BreakChanceField = "BreakChance";

        private static FieldInfo upgrade_chance_field;
        private static FieldInfo break_chance_field;
        private static AccessTools.FieldRef<ConfigEntry<float>, float> config_value;
        private static bool? supported;

        /// <summary>
        /// Whether Reforged Potential is installed and looks the way this expects. When it is installed but does
        /// not, the refinement odds are its own and forging cannot change them.
        /// </summary>
        internal static bool Supported {
            get {
                if (supported == null) { supported = Resolve(); }
                return supported.Value;
            }
        }

        private static bool Resolve() {
            if (Modcheck.IsReforgedPotentialEnabled == false) { return false; }

            try {
                Type plugin = AccessTools.TypeByName(PluginType);
                if (plugin != null) {
                    upgrade_chance_field = AccessTools.Field(plugin, UpgradeChanceField);
                    break_chance_field = AccessTools.Field(plugin, BreakChanceField);
                }
                config_value = AccessTools.FieldRefAccess<ConfigEntry<float>, float>("_typedValue");
            } catch (Exception e) {
                Logger.LogDebug($"Reforged Potential lookup failed: {e.Message}");
            }

            bool found = IsFloatEntry(upgrade_chance_field) && IsFloatEntry(break_chance_field) && config_value != null;
            if (found) {
                Logger.LogInfo("Reforged Potential detected, forging odds replace its Upgrade Chance and Break Chance settings at the Forge of Potential while EnableRefinementBonus is on.");
            } else {
                Logger.LogWarning($"Reforged Potential is installed but {PluginType} does not look the way this mod expects, so its own Upgrade Chance and Break Chance settings decide refinements and forging cannot improve them. This mod may need an update to match the installed Reforged Potential version.");
            }
            return found;
        }

        private static bool IsFloatEntry(FieldInfo field) {
            return field != null && field.IsStatic && field.FieldType == typeof(ConfigEntry<float>);
        }

        private static ConfigEntry<float> Entry(FieldInfo field) {
            return field.GetValue(null) as ConfigEntry<float>;
        }

        private static ConfigEntry<float> overridden_upgrade = null;
        private static ConfigEntry<float> overridden_break = null;
        private static float saved_upgrade;
        private static float saved_break;

        /// <summary>
        /// Hands Reforged Potential these odds for its next roll. RestoreChances must follow.
        /// </summary>
        internal static void OverrideChances(ForgingRefinement.RefineChances chances) {
            RestoreChances();
            if (Supported == false) { return; }

            ConfigEntry<float> upgrade = Entry(upgrade_chance_field);
            ConfigEntry<float> breakOnFailure = Entry(break_chance_field);
            if (upgrade == null || breakOnFailure == null) { return; }

            overridden_upgrade = upgrade;
            overridden_break = breakOnFailure;
            saved_upgrade = config_value(upgrade);
            saved_break = config_value(breakOnFailure);

            // chances.Break is the chance of a break out of every attempt, their Break Chance is out of the failures.
            float failure = 1f - chances.Success;
            config_value(upgrade) = chances.Success;
            config_value(breakOnFailure) = failure > 0f ? Mathf.Clamp01(chances.Break / failure) : 0f;
            Logger.LogDebug($"Reforged Potential odds replaced with forging's: upgrade {config_value(upgrade)}, break on failure {config_value(breakOnFailure)}.");
        }

        internal static void RestoreChances() {
            if (overridden_upgrade == null) { return; }
            config_value(overridden_upgrade) = saved_upgrade;
            config_value(overridden_break) = saved_break;
            overridden_upgrade = null;
            overridden_break = null;
        }

        /// <summary>
        /// The odds Reforged Potential rolls with when forging is not replacing them, in ForgingRefinement's terms.
        /// </summary>
        internal static bool TryGetConfiguredChances(ItemDrop.ItemData.SharedData idol, out ForgingRefinement.RefineChances chances) {
            chances = new ForgingRefinement.RefineChances();
            if (Supported == false) { return false; }

            ConfigEntry<float> upgrade = Entry(upgrade_chance_field);
            ConfigEntry<float> breakOnFailure = Entry(break_chance_field);
            if (upgrade == null || breakOnFailure == null) { return false; }

            chances.Success = Mathf.Clamp01(upgrade.Value);
            chances.Break = (1f - chances.Success) * Mathf.Clamp01(breakOnFailure.Value);
            chances.Refund = Mathf.Clamp01(idol.m_breakReturnIngreientsAmount);
            return true;
        }
    }
}
