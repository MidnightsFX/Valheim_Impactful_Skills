using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

namespace ImpactfulSkills.patches
{
    /// <summary>
    /// Forging improves the odds at the Forge of Potential.
    ///
    /// Vanilla makes one roll (InventoryGui.DoCrafting), against values stored on the idol the recipe asks for:
    ///     success  when m_upgradeChance >= roll
    ///     break    when m_breakChance   >= 1 - roll    (the item is destroyed, m_breakReturnIngreientsAmount of
    ///                                                   its materials come back)
    ///     otherwise the item drops a level
    /// so the real chance of a break is min(1 - success, m_breakChance). The shipped idols use 0.65 and 1.0, so
    /// every failure destroys the item.
    ///
    /// Those values are read off the idol prefab's shared data, which every copy of that idol shares, so they
    /// are swapped in for the one DoCrafting call and put back in a finalizer.
    ///
    /// Reforged Potential makes its own roll against its own config and never reads the idol's odds, so it is
    /// handed forging's odds as well (see compatibility.ReforgedPotential).
    /// </summary>
    internal static class ForgingRefinement
    {
        internal struct RefineChances
        {
            public float Success;
            public float Break;
            public float Refund;

            public float Downgrade => Mathf.Max(0f, 1f - Success - Break);
        }

        internal static bool RefinementEnabled => ValConfig.EnableForging.Value && ValConfig.EnableRefinementBonus.Value;

        /// <summary>
        /// How much of the forging bonus applies to this player. 0 when the bonus is off or not yet reached.
        /// </summary>
        internal static float RefineSkillFactor(Player player) {
            if (RefinementEnabled == false || player == null) { return 0f; }
            if (Forging.ForgingLevel(player) < ValConfig.RefinementBonusLevel.Value) { return 0f; }
            return player.GetSkillFactor(Forging.ForgingSkill);
        }

        /// <summary>
        /// The idol a Forge of Potential recipe consumes, found the same way DoCrafting finds it.
        /// </summary>
        internal static ItemDrop.ItemData.SharedData FindUpgraderResource(Recipe recipe) {
            if (recipe == null || recipe.m_resources == null) { return null; }
            foreach (Piece.Requirement requirement in recipe.m_resources) {
                if (requirement.m_upgraderResource && requirement.m_resItem != null) {
                    return requirement.m_resItem.m_itemData.m_shared;
                }
            }
            return null;
        }

        /// <summary>
        /// The odds a refinement actually has, with the forging bonus applied. Success and refund gain their bonus
        /// outright. The break reduction applies to the real break chance rather than to m_breakChance: that value
        /// is 1.0 on the shipped idols, and lowering it does nothing until it drops below the failure chance, so
        /// scaling it directly would leave every failure destroying the item until around level 80.
        ///
        /// Written back as m_breakChance this is exact - once it is at or below 1 - success, the chance of a break
        /// is the value itself.
        /// </summary>
        internal static RefineChances EffectiveChances(ItemDrop.ItemData.SharedData idol, float skillFactor) {
            RefineChances chances = new RefineChances();
            chances.Success = Mathf.Clamp01(idol.m_upgradeChance + skillFactor * ValConfig.RefineSuccessChanceBonus.Value);
            chances.Refund = Mathf.Clamp01(idol.m_breakReturnIngreientsAmount + skillFactor * ValConfig.RefineBreakRefundBonus.Value);
            float vanillaBreak = Mathf.Clamp01(Mathf.Min(1f - chances.Success, idol.m_breakChance));
            chances.Break = vanillaBreak * Mathf.Clamp01(1f - skillFactor * ValConfig.RefineBreakChanceReduction.Value);
            return chances;
        }

        /// <summary>
        /// The odds the next refinement will really be rolled with. False when they are not known: Reforged
        /// Potential is rolling with its own odds and this could not read them.
        /// </summary>
        internal static bool TryGetChances(Player player, ItemDrop.ItemData.SharedData idol, out RefineChances chances) {
            if (compatibility.Modcheck.IsReforgedPotentialEnabled && (RefinementEnabled == false || compatibility.ReforgedPotential.Supported == false)) {
                return compatibility.ReforgedPotential.TryGetConfiguredChances(idol, out chances);
            }
            chances = EffectiveChances(idol, RefineSkillFactor(player));
            return true;
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
        private static class RefinementChancePatch
        {
            private static ItemDrop.ItemData.SharedData OverriddenIdol = null;
            private static float SavedUpgradeChance;
            private static float SavedBreakChance;
            private static float SavedRefund;

            // Ahead of Reforged Potential, whose prefix makes the roll itself and then skips the original.
            [HarmonyPrefix]
            [HarmonyBefore(compatibility.ReforgedPotential.PluginGUID)]
            private static void Prefix(InventoryGui __instance) {
                Restore();
                if (RefinementEnabled == false || __instance.m_craftRecipe == null || Player.m_localPlayer == null) { return; }

                CraftingStation station = Player.m_localPlayer.GetCurrentCraftingStation();
                if (station == null || station.m_upgrader == false) { return; }

                ItemDrop.ItemData.SharedData idol = FindUpgraderResource(__instance.m_craftRecipe);
                if (idol == null) { return; }

                float skillFactor = RefineSkillFactor(Player.m_localPlayer);
                RefineChances chances = EffectiveChances(idol, skillFactor);
                // Below the bonus level these are just the idol's odds, but Reforged Potential still has to be given
                // them - otherwise its fixed odds apply until the bonus kicks in, then drop to the idol's.
                if (compatibility.Modcheck.IsReforgedPotentialEnabled) { compatibility.ReforgedPotential.OverrideChances(chances); }
                if (skillFactor <= 0f) { return; }

                OverriddenIdol = idol;
                SavedUpgradeChance = idol.m_upgradeChance;
                SavedBreakChance = idol.m_breakChance;
                SavedRefund = idol.m_breakReturnIngreientsAmount;
                idol.m_upgradeChance = chances.Success;
                idol.m_breakChance = chances.Break;
                idol.m_breakReturnIngreientsAmount = chances.Refund;
                Logger.LogDebug($"Refinement odds with forging: success {chances.Success}, break {chances.Break}, refund {chances.Refund}.");
            }

            // A finalizer runs even when DoCrafting throws, and the idol's values are shared by every idol of its
            // kind, so they must never be left modified.
            [HarmonyFinalizer]
            private static void Finalizer() {
                Restore();
            }

            private static void Restore() {
                if (compatibility.Modcheck.IsReforgedPotentialEnabled) { compatibility.ReforgedPotential.RestoreChances(); }
                if (OverriddenIdol == null) { return; }
                OverriddenIdol.m_upgradeChance = SavedUpgradeChance;
                OverriddenIdol.m_breakChance = SavedBreakChance;
                OverriddenIdol.m_breakReturnIngreientsAmount = SavedRefund;
                OverriddenIdol = null;
            }
        }

        /// <summary>
        /// Vanilla never says what the odds are. Show them at the top of the recipe description while an item is
        /// selected at the Forge of Potential. UpdateRecipe rebuilds the description every frame, so this does too.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateRecipe))]
        private static class RefinementChanceDisplayPatch
        {
            private static void Postfix(InventoryGui __instance, Player player) {
                if (ValConfig.EnableForging.Value == false || player == null) { return; }

                CraftingStation station = player.GetCurrentCraftingStation();
                if (station == null || station.m_upgrader == false) { return; }
                if (__instance.m_selectedRecipe.Recipe == null || __instance.m_selectedRecipe.ItemData == null) { return; }

                ItemDrop.ItemData.SharedData idol = FindUpgraderResource(__instance.m_selectedRecipe.Recipe);
                if (idol == null || TryGetChances(player, idol, out RefineChances chances) == false) { return; }

                string odds = LocalizationManager.Instance.TryTranslate("$forging_refine_chances")
                    .Replace("{0}", Percent(chances.Success))
                    .Replace("{1}", Percent(chances.Downgrade))
                    .Replace("{2}", Percent(chances.Break));
                string refund = LocalizationManager.Instance.TryTranslate("$forging_refine_refund").Replace("{0}", Percent(chances.Refund));
                __instance.m_recipeDecription.text = $"<color=orange>{odds}</color>\n{refund}\n\n{__instance.m_recipeDecription.text}";
            }

            private static string Percent(float value) {
                return (value * 100f).ToString("0.#");
            }
        }
    }
}
