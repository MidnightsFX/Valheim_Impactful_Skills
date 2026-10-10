using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ImpactfulSkills.patches
{
    /// <summary>
    /// The forging skill. Smiths raise their crafting stations a level above what their extensions give them, get
    /// better odds at the Forge of Potential (ForgingRefinement), and at high levels craft masterwork and
    /// lightweight equipment (ForgedItems).
    /// </summary>
    internal static class Forging
    {
        public static Skills.SkillType ForgingSkill = 0;

        private static HashSet<string> Tier1Stations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static HashSet<string> Tier2Stations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, float> StationXPWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private static HashSet<string> NoXPItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static HashSet<string> MetalIngredients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static void SetupForgingSkill() {
            SkillConfig forging = new SkillConfig();
            forging.Name = "$skill_Forging";
            forging.Description = "$skill_Forging_description";
            // Forge.png is only in the bundle once it has been rebuilt from the Unity project. Until then borrow an
            // icon that is already in it rather than registering the skill without one.
            Sprite icon = ImpactfulSkills.EmbeddedResourceBundle.LoadAsset<Sprite>("Assets/Custom/Icons/skill_icons/Forge.png");
            if (icon == null) {
                icon = ImpactfulSkills.EmbeddedResourceBundle.LoadAsset<Sprite>("Assets/Custom/Icons/skill_icons/gatherer.png");
            }
            forging.Icon = icon;
            forging.Identifier = "midnightsfx.forging";
            forging.IncreaseStep = 1f;
            ForgingSkill = SkillManager.Instance.AddSkill(forging);
            ConsoleSkillNames.Register(forging);

            Tier1Stations = ParseNameList(ValConfig.StationBonusTier1Stations.Value);
            Tier2Stations = ParseNameList(ValConfig.StationBonusTier2Stations.Value);
            StationXPWeights = ParseStationXPWeights(ValConfig.ForgingStationXPWeights.Value);
            NoXPItems = ParseNameList(ValConfig.ForgingNoXPItems.Value);
            MetalIngredients = ParseNameList(ValConfig.ForgingMetalIngredients.Value);
            ValConfig.StationBonusTier1Stations.SettingChanged += (s, e) => Tier1Stations = ParseNameList(ValConfig.StationBonusTier1Stations.Value);
            ValConfig.StationBonusTier2Stations.SettingChanged += (s, e) => Tier2Stations = ParseNameList(ValConfig.StationBonusTier2Stations.Value);
            ValConfig.ForgingStationXPWeights.SettingChanged += (s, e) => StationXPWeights = ParseStationXPWeights(ValConfig.ForgingStationXPWeights.Value);
            ValConfig.ForgingNoXPItems.SettingChanged += (s, e) => NoXPItems = ParseNameList(ValConfig.ForgingNoXPItems.Value);
            ValConfig.ForgingMetalIngredients.SettingChanged += (s, e) => MetalIngredients = ParseNameList(ValConfig.ForgingMetalIngredients.Value);
        }

        private static HashSet<string> ParseNameList(string value) {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string entry in value.Split(',')) {
                string name = entry.Trim();
                if (name.Length > 0) { names.Add(name); }
            }
            return names;
        }

        /// <summary>
        /// Whether a name list holds this name, either outright or through an entry ending in * that it starts with.
        /// </summary>
        private static bool ListMatches(HashSet<string> names, string name) {
            if (names.Contains(name)) { return true; }
            foreach (string entry in names) {
                if (entry.Length > 1 && entry.EndsWith("*") && name.StartsWith(entry.Substring(0, entry.Length - 1), StringComparison.OrdinalIgnoreCase)) { return true; }
            }
            return false;
        }

        private static Dictionary<string, float> ParseStationXPWeights(string value) {
            Dictionary<string, float> weights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            foreach (string entry in value.Split(',')) {
                string[] parts = entry.Split(':');
                string name = parts[0].Trim();
                if (name.Length == 0) { continue; }

                float weight = 1f;
                if (parts.Length > 1 && float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)) {
                    weight = parsed;
                } else if (parts.Length > 1) {
                    Logger.LogWarning($"Could not read the XP multiplier in ForgingStationXPWeights entry '{entry}', using 1.");
                }
                weights[name] = weight;
            }
            return weights;
        }

        internal static float ForgingLevel(Player player) {
            return player.GetSkillLevel(ForgingSkill);
        }

        /// <summary>
        /// How many levels the forging skill adds to a station with this name.
        /// </summary>
        internal static int StationLevelBonus(Player player, string stationName) {
            if (ValConfig.EnableForging.Value == false || player == null || string.IsNullOrEmpty(stationName)) { return 0; }

            float level = ForgingLevel(player);
            int bonus = 0;
            if (ValConfig.EnableStationBonusTier1.Value && level >= ValConfig.StationBonusTier1Level.Value && Tier1Stations.Contains(stationName)) {
                bonus += ValConfig.StationBonusTier1Amount.Value;
            }
            if (ValConfig.EnableStationBonusTier2.Value && level >= ValConfig.StationBonusTier2Level.Value && Tier2Stations.Contains(stationName)) {
                bonus += ValConfig.StationBonusTier2Amount.Value;
            }
            return bonus;
        }

        /// <summary>
        /// GetLevel only ever runs for the local player's own crafting: the recipe gate (RequiredCraftingStation),
        /// the station level shown in the crafting panel, repairs, and AddKnownStation's recipe discovery.
        ///
        /// That last one records the boosted level in the player's known stations on purpose. Recipes are only
        /// listed once the player knows a station of their minimum level, so leaving it out would make the bonus
        /// unlock recipes that never show up. A recipe learned this way behaves as it does in vanilla after an
        /// extension is destroyed - still listed, not craftable until the level is back.
        ///
        /// Build range is untouched, it counts extensions directly rather than asking for the level.
        /// </summary>
        [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.GetLevel))]
        private static class StationLevelBonusPatch
        {
            private static void Postfix(CraftingStation __instance, ref int __result) {
                if (Player.m_localPlayer == null) { return; }
                __result += StationLevelBonus(Player.m_localPlayer, __instance.m_name);
            }
        }

        /// <summary>
        /// How much of the usual forging XP this item is worth: none for starter gear (ForgingNoXPItems), the
        /// non-metal multiplier for anything made without metal, full for everything else.
        ///
        /// The whole recipe is checked rather than what the current quality level costs, so an item is worth the same
        /// whether it is crafted, upgraded or refined - the Forge of Potential uses the same recipe, with the idol as
        /// one more resource.
        /// </summary>
        internal static float ItemXPMultiplier(Recipe recipe) {
            if (recipe == null || recipe.m_item == null) { return 1f; }
            if (ListMatches(NoXPItems, recipe.m_item.gameObject.name)) { return 0f; }

            if (recipe.m_resources != null) {
                foreach (Piece.Requirement requirement in recipe.m_resources) {
                    if (requirement.m_resItem != null && ListMatches(MetalIngredients, requirement.m_resItem.gameObject.name)) { return 1f; }
                }
            }
            return ValConfig.ForgingNonMetalXPMultiplier.Value;
        }

        /// <summary>
        /// Awards forging XP for a craft that went through. Called by ForgedItems once DoCrafting has finished, when
        /// what actually happened is known.
        /// </summary>
        internal static void GrantCraftXP(Player player, ForgedItems.CraftContext craft) {
            if (ValConfig.EnableForging.Value == false || player == null) { return; }

            float itemMultiplier = ItemXPMultiplier(craft.Recipe);
            if (itemMultiplier <= 0f) { return; }

            float xp = 0f;
            if (craft.AtUpgrader) {
                if (craft.RefineAttempted == false) { return; }
                xp = ValConfig.ForgingRefineAttemptXP.Value * craft.TargetQuality;
                if (craft.RefineSucceeded) { xp *= ValConfig.ForgingRefineSuccessXPMultiplier.Value; }
            } else {
                if (craft.Produced == null) { return; }

                string stationName = craft.Station != null ? craft.Station.m_name : null;
                float weight = 1f;
                bool listed = stationName != null && StationXPWeights.TryGetValue(stationName, out weight);
                if (listed == false) { weight = 1f; }
                if (listed) {
                    xp += ValConfig.ForgingStationCraftXP.Value * weight * craft.Multiplier;
                }
                if (craft.Forgeable) {
                    if (craft.IsUpgrade) {
                        xp += ValConfig.ForgingUpgradeXP.Value * weight * craft.TargetQuality;
                    } else {
                        // The recipe's minimum station level is the closest thing to a tier an item has: later items
                        // at the same station ask for more extensions.
                        xp += ValConfig.ForgingEquipmentCraftXP.Value * weight * Mathf.Max(1, craft.Recipe.m_minStationLevel) * craft.Multiplier;
                    }
                }
            }

            xp *= itemMultiplier;
            if (xp <= 0f) { return; }
            Logger.LogDebug($"Forging XP {xp} for {craft.PrefabName} (upgrade: {craft.IsUpgrade}, refinement: {craft.AtUpgrader}, item multiplier: {itemMultiplier}).");
            player.RaiseSkill(ForgingSkill, xp);
        }
    }
}
