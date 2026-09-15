using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace ImpactfulSkills.patches {
    /// <summary>
    /// Fish carry a quality level (1-5, their size), but that quality only changes the result of a single recipe -
    /// turning a whole fish into raw fish. Every other whole fish recipe ignores it, and worse, will happily eat
    /// the quality 5 anglerfish you spent an evening catching for exactly the same output a quality 1 would have
    /// given.
    ///
    /// This scales those recipes with the quality of the fish that is spent. Recipes that produce equipment are
    /// not handled here - a better fish makes a better item rather than more of it, which lives in
    /// <see cref="Crafting"/>. Picking which of the two a recipe earns, and making the craft consume the tier that
    /// was actually priced in, is <see cref="IngredientQuality"/>.
    /// </summary>
    public static class Fishing {
        private static readonly FieldInfo RecipeAmountField = AccessTools.Field(typeof(Recipe), nameof(Recipe.m_amount));
        private static readonly MethodInfo DisplayAmountMethod = AccessTools.Method(typeof(Fishing), nameof(DisplayAmount));
        private static readonly MethodInfo ListAmountMethod = AccessTools.Method(typeof(Fishing), nameof(ListAmount));

        /// <summary>
        /// The extra output a recipe earns from the quality of the ingredient that will be spent on it. Mirrors
        /// vanilla's own expression in Recipe.GetAmount, including where the ceiling sits, and is driven off the
        /// recipe's authored m_qualityResultAmountMultiplier so that recipe editing mods can tune it per recipe.
        ///
        /// The two multipliers are separate because the crafting panel needs them to be: it computes a per unit
        /// amount and multiplies by the craft count itself, so it has to pick its tier against the full multicrafted
        /// cost (tierMultiplier) while returning only one unit's worth (craftMultiplier). Passing the same value for
        /// both, as the actual craft does, is what makes the panel and the craft agree.
        /// </summary>
        internal static int QualityBonus(Recipe recipe, int craftMultiplier, int tierMultiplier) {
            if (Player.m_localPlayer == null) { return 0; }

            int index;
            if (IngredientQuality.Classify(recipe, Player.m_localPlayer.GetCurrentCraftingStation(), out index) != IngredientQuality.CraftMode.Amount) { return 0; }

            Piece.Requirement requirement = recipe.m_resources[index];
            // Quality level 1: the bonus never applies to upgrades, so the requirement always costs its base amount.
            int required = requirement.GetAmount(1) * tierMultiplier;
            if (required <= 0) { return 0; }

            int tier = IngredientQuality.SelectTier(Player.m_localPlayer.GetInventory(), requirement.m_resItem.m_itemData, required);
            if (tier <= 1) { return 0; }

            return (int)Mathf.Ceil((float)((tier - 1) * recipe.m_amount) * recipe.m_qualityResultAmountMultiplier * ValConfig.QualityIngredientOutputMultiplier.Value) * craftMultiplier;
        }

        /// <summary>
        /// What the crafting panel should print as the recipe's output amount. Deliberately does not go through
        /// Recipe.GetAmount: for an "any one ingredient" recipe that call sweeps the inventory once per requirement
        /// per quality tier, which for the raw fish recipe is 72 sweeps - every frame the panel is open - and it
        /// dereferences its singleReqItem unguarded, so it throws when you are carrying none of the ingredients.
        /// </summary>
        public static int DisplayAmount(Recipe recipe) {
            if (recipe == null) { return 0; }
            if (ValConfig.EnableQualityIngredientScaling.Value == false) { return recipe.m_amount; }

            // The caller multiplies this by the craft count, so hand back one unit but price the tier against all of them.
            return recipe.m_amount + QualityBonus(recipe, 1, IngredientQuality.PanelCraftMultiplier());
        }

        /// <summary>
        /// The same number for the recipe list, which has no multicraft of its own - the list is drawn for every
        /// available recipe, not just the selected one, so pricing them all against the selected recipe's craft
        /// count would put the wrong bonus on every other row.
        /// </summary>
        public static int ListAmount(Recipe recipe) {
            if (recipe == null) { return 0; }
            if (ValConfig.EnableQualityIngredientScaling.Value == false) { return recipe.m_amount; }

            return recipe.m_amount + QualityBonus(recipe, 1, 1);
        }

        /// <summary>
        /// Rewrites every load of Recipe.m_amount into a call to <paramref name="replacement"/>. The recipe reference
        /// is already on the stack at each load, so swapping the opcode is stack neutral.
        ///
        /// Bails on anything other than two replacements. One is the dangerous count, not zero: a future game build
        /// that hoisted the value into a local would leave the label showing the boosted amount while the "> 1" test
        /// still read the raw one.
        /// </summary>
        private static IEnumerable<CodeInstruction> RedirectRecipeAmount(IEnumerable<CodeInstruction> instructions, MethodInfo replacement, string target) {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);

            int matches = 0;
            foreach (CodeInstruction instruction in code) {
                // LoadsField matches ldfld/ldsfld but never ldflda, so the struct address loads for m_selectedRecipe
                // cannot be caught by this.
                if (instruction.LoadsField(RecipeAmountField)) { matches++; }
            }
            if (matches != 2) {
                Logger.LogWarning($"Unable to patch the crafting panel amount for quality ingredients. Expected 2 loads of Recipe.m_amount in {target}, found {matches}. Skipping this patch, crafted amounts will still scale but the panel will show the unscaled number.");
                return instructions;
            }

            foreach (CodeInstruction instruction in code) {
                if (instruction.LoadsField(RecipeAmountField) == false) { continue; }
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
            }
            return code;
        }

        /// <summary>
        /// Patching GetAmount rather than DoCrafting is load bearing, not convenience: OnCraftPressed prechecks
        /// CanAddItem against this same value, and Inventory.AddItem can add part of a stack and still return null,
        /// which would send DoCrafting down a path that adds the items and then skips consuming the resources.
        ///
        /// The need/singleReqItem out params are deliberately left alone - setting singleReqItem diverts DoCrafting
        /// into the single item removal branch, which would never consume the bread dough.
        /// </summary>
        [HarmonyPatch(typeof(Recipe), nameof(Recipe.GetAmount))]
        internal static class RecipeQualityIngredientAmountPatch {
            [HarmonyPostfix]
            private static void Postfix(Recipe __instance, int quality, int craftMultiplier, ref int __result) {
                // quality > 1 is an upgrade, which never scales. No local player means a dedicated server, which has
                // no inventory to pick a tier out of.
                if (ValConfig.EnableQualityIngredientScaling.Value == false || quality > 1 || Player.m_localPlayer == null) { return; }

                int bonus = QualityBonus(__instance, craftMultiplier, craftMultiplier);
                if (bonus <= 0) { return; }

                Logger.LogDebug($"Quality ingredient bonus for {__instance.name}: {__result} + {bonus}");
                __result += bonus;
            }
        }

        /// <summary>
        /// Makes the crafting panel and the recipe list show the amount that will actually be crafted, before the
        /// craft is committed. UpdateRecipe runs every frame the panel is open; AddRecipeToList only when the list
        /// is rebuilt.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGui))]
        public static class CraftingPanelQualityAmountPatch {
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(InventoryGui.UpdateRecipe))]
            private static IEnumerable<CodeInstruction> UpdateRecipeTranspiler(IEnumerable<CodeInstruction> instructions) {
                return RedirectRecipeAmount(instructions, DisplayAmountMethod, nameof(InventoryGui.UpdateRecipe));
            }

            [HarmonyTranspiler]
            [HarmonyPatch(nameof(InventoryGui.AddRecipeToList))]
            private static IEnumerable<CodeInstruction> AddRecipeToListTranspiler(IEnumerable<CodeInstruction> instructions) {
                return RedirectRecipeAmount(instructions, ListAmountMethod, nameof(InventoryGui.AddRecipeToList));
            }
        }
    }
}
