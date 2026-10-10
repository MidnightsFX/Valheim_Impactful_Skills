using HarmonyLib;
using ImpactfulSkills.common;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
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
    ///
    /// Past RefineDecayStartLevel every level makes a refinement harder (DecayFactor). Without it a skilled smith
    /// refines on a walk that only drifts upwards - 85% success, and the failures just step the item back down a
    /// level - so nothing stops an item reaching level 100 and beyond. The success chance, the refund and forging's
    /// break protection all shrink by the same compounding factor, so the failures that grow out of the lost success
    /// increasingly destroy the item rather than lower it.
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
        /// How much of the forging bonus applies to this player. 0 when the bonus is off or not yet reached, and no
        /// more than 1 unless ForgingBonusesPastLevel100 is on - past that the break reduction alone would outlast the
        /// decay for dozens of levels.
        /// </summary>
        internal static float RefineSkillFactor(Player player) {
            if (RefinementEnabled == false || player == null) { return 0f; }
            if (Forging.ForgingLevel(player) < ValConfig.RefinementBonusLevel.Value) { return 0f; }
            return player.GetBonusSkillFactor(Forging.ForgingSkill, ValConfig.ForgingBonusesPastLevel100);
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
        /// The share of the refinement odds left when refining an item to this level: 1 up to RefineDecayStartLevel,
        /// then RefineDecayRate is taken off for every level past it, compounding.
        /// </summary>
        internal static float DecayFactor(int targetLevel) {
            if (ValConfig.EnableForging.Value == false || ValConfig.EnableRefinementDecay.Value == false) { return 1f; }
            int levelsPast = targetLevel - ValConfig.RefineDecayStartLevel.Value;
            if (levelsPast <= 0) { return 1f; }
            return Mathf.Pow(1f - Mathf.Clamp01(ValConfig.RefineDecayRate.Value), levelsPast);
        }

        /// <summary>
        /// The odds a refinement actually has, with the forging bonus applied. Success and refund gain their bonus
        /// outright. The break reduction applies to the real break chance rather than to m_breakChance: that value
        /// is 1.0 on the shipped idols, and lowering it does nothing until it drops below the failure chance, so
        /// scaling it directly would leave every failure destroying the item until around level 80.
        ///
        /// decay is DecayFactor for the level being attempted. It scales success, refund and the break reduction
        /// alike, and the break chance is worked out from the decayed success, so everything success loses becomes a
        /// failure.
        ///
        /// Written back as m_breakChance this is exact - once it is at or below 1 - success, the chance of a break
        /// is the value itself.
        /// </summary>
        internal static RefineChances EffectiveChances(ItemDrop.ItemData.SharedData idol, float skillFactor, float decay) {
            RefineChances chances = new RefineChances();
            chances.Success = Mathf.Clamp01(idol.m_upgradeChance + skillFactor * ValConfig.RefineSuccessChanceBonus.Value) * decay;
            chances.Refund = Mathf.Clamp01(idol.m_breakReturnIngreientsAmount + skillFactor * ValConfig.RefineBreakRefundBonus.Value) * decay;
            float vanillaBreak = Mathf.Clamp01(Mathf.Min(1f - chances.Success, idol.m_breakChance));
            chances.Break = vanillaBreak * Mathf.Clamp01(1f - skillFactor * ValConfig.RefineBreakChanceReduction.Value * decay);
            return chances;
        }

        /// <summary>
        /// The odds the next refinement to targetLevel will really be rolled with. False when they are not known:
        /// Reforged Potential is rolling with its own odds and this could not read them.
        /// </summary>
        internal static bool TryGetChances(Player player, ItemDrop.ItemData.SharedData idol, int targetLevel, out RefineChances chances) {
            if (compatibility.Modcheck.IsReforgedPotentialEnabled && (RefinementEnabled == false || compatibility.ReforgedPotential.Supported == false)) {
                return compatibility.ReforgedPotential.TryGetConfiguredChances(idol, out chances);
            }
            chances = EffectiveChances(idol, RefineSkillFactor(player), DecayFactor(targetLevel));
            return true;
        }

        /// <summary>
        /// A broken item hands back (GetAmount(1) + GetAmount(level)) * m_breakReturnIngreientsAmount of each
        /// recoverable ingredient, and Piece.Requirement.GetAmount keeps climbing past the item's max quality. That
        /// never mattered while every failure destroyed the item, but forging makes far higher levels reachable - a
        /// level 105 black metal pickaxe broke for hundreds of black metal. An item above its normal max level is
        /// priced as if it were at that level instead.
        ///
        /// The cap is the item's own m_maxQuality rather than a fixed 4, so an item another mod gives more levels is
        /// still priced up to its own maximum. Same stack shape as the instance call it stands in for, so
        /// CapBreakRefund only has to swap the call.
        /// </summary>
        internal static int BreakRefundAmount(Piece.Requirement requirement, int quality) {
            if (ValConfig.EnableForging.Value) {
                Recipe recipe = InventoryGui.instance != null ? InventoryGui.instance.m_craftRecipe : null;
                if (recipe != null && recipe.m_item != null) {
                    quality = Mathf.Min(quality, Mathf.Max(1, recipe.m_item.m_itemData.m_shared.m_maxQuality));
                }
            }
            return requirement.GetAmount(quality);
        }

        private static readonly FieldInfo BreakRefundField = AccessTools.Field(typeof(ItemDrop.ItemData.SharedData), nameof(ItemDrop.ItemData.SharedData.m_breakReturnIngreientsAmount));
        private static readonly MethodInfo RequirementGetAmount = AccessTools.Method(typeof(Piece.Requirement), nameof(Piece.Requirement.GetAmount), new Type[] { typeof(int) });
        private static readonly MethodInfo BreakRefundAmountMethod = AccessTools.Method(typeof(ForgingRefinement), nameof(BreakRefundAmount));

        /// <summary>
        /// Sends the break refund's GetAmount calls through BreakRefundAmount. Vanilla and Reforged Potential both
        /// guard the refund with "m_breakReturnIngreientsAmount > 0" and then multiply by that same field, so only the
        /// calls between those two reads are swapped - nothing else DoCrafting prices, or another mod's transpiler
        /// adds, is touched.
        /// </summary>
        internal static IEnumerable<CodeInstruction> CapBreakRefund(IEnumerable<CodeInstruction> instructions, string target) {
            CodeMatcher codeMatcher = new CodeMatcher(instructions);
            if (codeMatcher.TryMatchEndForward($"Unable to cap the break refund in {target}.", new CodeMatch(OpCodes.Ldfld, BreakRefundField)) == false) {
                return codeMatcher.Instructions();
            }

            int replaced = 0;
            codeMatcher.Advance(1);
            while (codeMatcher.IsValid && codeMatcher.Instruction.LoadsField(BreakRefundField) == false) {
                // Set rewrites the instruction in place, so any label on it stays put.
                if (codeMatcher.Instruction.Calls(RequirementGetAmount)) {
                    codeMatcher.Set(OpCodes.Call, BreakRefundAmountMethod);
                    replaced++;
                }
                codeMatcher.Advance(1);
            }
            if (replaced == 0) {
                Logger.LogWarning($"Unable to cap the break refund in {target}. It no longer prices the refund the way this mod expects, so items above their max level may return more than they are worth when they break.");
            }
            return codeMatcher.Instructions();
        }

        /// <summary>
        /// Vanilla's copy of the break refund. Reforged Potential carries its own, capped in
        /// compatibility.ReforgedPotential.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
        private static class BreakRefundCapPatch
        {
            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
                return CapBreakRefund(instructions, "DoCrafting");
            }
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
                if (ValConfig.EnableForging.Value == false || __instance.m_craftRecipe == null || __instance.m_craftUpgradeItem == null || Player.m_localPlayer == null) { return; }

                CraftingStation station = Player.m_localPlayer.GetCurrentCraftingStation();
                if (station == null || station.m_upgrader == false) { return; }

                ItemDrop.ItemData.SharedData idol = FindUpgraderResource(__instance.m_craftRecipe);
                if (idol == null) { return; }

                float skillFactor = RefineSkillFactor(Player.m_localPlayer);
                float decay = DecayFactor(__instance.m_craftUpgradeItem.m_quality + 1);
                // With the bonus off forging only steps in to apply the decay, and not at all while Reforged Potential
                // rolls with its own odds - those come with its own level limits.
                if (RefinementEnabled == false && (decay >= 1f || compatibility.Modcheck.IsReforgedPotentialEnabled)) { return; }

                RefineChances chances = EffectiveChances(idol, skillFactor, decay);
                // Below the bonus level these are just the idol's odds, but Reforged Potential still has to be given
                // them - otherwise its fixed odds apply until the bonus kicks in, then drop to the idol's.
                if (compatibility.Modcheck.IsReforgedPotentialEnabled) { compatibility.ReforgedPotential.OverrideChances(chances); }
                if (skillFactor <= 0f && decay >= 1f) { return; }

                OverriddenIdol = idol;
                SavedUpgradeChance = idol.m_upgradeChance;
                SavedBreakChance = idol.m_breakChance;
                SavedRefund = idol.m_breakReturnIngreientsAmount;
                idol.m_upgradeChance = chances.Success;
                idol.m_breakChance = chances.Break;
                idol.m_breakReturnIngreientsAmount = chances.Refund;
                Logger.LogDebug($"Refinement odds with forging: success {chances.Success}, break {chances.Break}, refund {chances.Refund} (decay {decay}).");
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
                if (idol == null || TryGetChances(player, idol, __instance.m_selectedRecipe.ItemData.m_quality + 1, out RefineChances chances) == false) { return; }

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
