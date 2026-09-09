using HarmonyLib;
using ImpactfulSkills.common;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;


namespace ImpactfulSkills.patches
{
    internal class Crafting {
        public static float CheckAndReduceDurabilityCost(float item_durability_drain) {
            if (ValConfig.EnableDurabilityLossPrevention.Value && Player.m_localPlayer != null) {
                float skillFactor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Crafting);
                float chance = UnityEngine.Random.value;
                float chanceToNotUseDurability = ValConfig.ChanceForDurabilityLossPrevention.Value;
                if (ValConfig.ScaleDurabilitySaveBySkillLevel.Value) {
                    chanceToNotUseDurability *= skillFactor;
                }
                if (ValConfig.DurabilitySaveLevel.Value <= skillFactor * 100.0 && chance < chanceToNotUseDurability) {
                    Logger.LogDebug(string.Format("Skipping durability usage {0} < {1}", chance, chanceToNotUseDurability));
                    return 0.0f;
                }
            }
            return item_durability_drain;
        }

        private static int CraftableBonus(InventoryGui instance, int base_amount_crafted)
        {
            if (!ValConfig.EnableCrafting.Value || Player.m_localPlayer == null) {
                return base_amount_crafted;
            } 
            int craftedTotal = base_amount_crafted;
            CraftingStation currentCraftingStation = Player.m_localPlayer.GetCurrentCraftingStation();
            float skillFactor;
            float skillLevel;
            // Maybe this should just use the skill that is defined as the crafting station skill?
            if (instance.m_craftRecipe.m_craftingStation != null && instance.m_craftRecipe.m_craftingStation.m_craftingSkill == Skills.SkillType.Cooking) {
                skillFactor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Cooking);
                skillLevel = Player.m_localPlayer.GetSkillLevel(Skills.SkillType.Cooking);
            } else {
                skillFactor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Crafting);
                skillLevel = Player.m_localPlayer.GetSkillLevel(Skills.SkillType.Crafting);
            }
            
            // Skip bonus rolls for upgrades, or if the crafting station is not set
            if (currentCraftingStation != null && instance.m_craftRecipe.m_craftingStation != null && instance.m_craftUpgradeItem == null) {
                Logger.LogDebug($"Determining crafting recipe bonus amount with {instance.m_craftRecipe.m_craftingStation.m_craftingSkill} {skillLevel}");
                craftedTotal += Crafting.GetCraftingItemBonusAmount(instance, base_amount_crafted, skillFactor, skillLevel, instance.m_craftRecipe.m_craftingStation.m_craftingSkill);
            }
                
            if (craftedTotal != base_amount_crafted) {
                Vector3 playerUpPos = Player.m_localPlayer.transform.position + Vector3.up;
                DamageText.instance.ShowText(DamageText.TextType.Bonus, playerUpPos, $"+{(craftedTotal - base_amount_crafted)}", true);
                instance.m_craftBonusEffect.Create(playerUpPos, Quaternion.identity, null, 1f, -1);
            }
            return craftedTotal;
        }

        private static void DetermineCraftingRefund(InventoryGui instance)
        {
            float skillLevel = Player.m_localPlayer.GetSkillLevel(Skills.SkillType.Crafting);
            float skillFactor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Crafting);
            if (ValConfig.EnableMaterialReturns.Value == false || skillLevel < ValConfig.CraftingMaterialReturnsLevel.Value) { return; }

            Dictionary<ItemDrop, int> resourcesToReturn = new Dictionary<ItemDrop, int>();
            if (instance.m_craftRecipe.m_requireOnlyOneIngredient) {
                Logger.LogDebug("Require any resource recipes do not get a refund.");
            } else {
                foreach (Piece.Requirement resource in instance.m_craftRecipe.m_resources) {
                    float roll = UnityEngine.Random.value;
                    float chance = ValConfig.ChanceForMaterialReturn.Value * skillFactor;
                    Logger.LogDebug($"Checking refund chance for {resource.m_resItem.name} {roll} < {chance}");
                    if (roll <= chance) {
                        if (resource.m_amount > 1) {
                            int returnedAmount = Mathf.RoundToInt(resource.m_amount * (ValConfig.MaxCraftingMaterialReturnPercent.Value * skillFactor));
                            resourcesToReturn.Add(resource.m_resItem, returnedAmount);
                        } else if (roll < chance / 2.0) {
                            resourcesToReturn.Add(resource.m_resItem, 1);
                        }
                    }
                }
                if (resourcesToReturn.Count == 0) { return; }

                Vector3 vector3 = Player.m_localPlayer.transform.position + Vector3.up;
                DamageText.instance.ShowText(DamageText.TextType.Bonus, vector3, LocalizationManager.Instance.TryTranslate("$craft_refund"), true);
                instance.m_craftBonusEffect.Create(vector3, Quaternion.identity, (Transform)null, 1f, -1);
                foreach (KeyValuePair<ItemDrop, int> keyValuePair in resourcesToReturn) {
                    bool didRefund = Player.m_localPlayer.GetInventory().AddItem(keyValuePair.Key.gameObject, keyValuePair.Value);
                    Logger.LogDebug($"Refund to add: {keyValuePair.Key.name} {keyValuePair.Value} | refunded? {didRefund}");
                }
            }
        }

        // Quality transfer state. The DoCrafting prefix works out what quality the craft has earned, the
        // Inventory.AddItem prefix a few lines later applies it to the one item being created, and the DoCrafting
        // postfix disarms whatever is left over so an early return inside DoCrafting cannot leave it primed.
        private static int PendingCraftQuality = 0;
        private static string PendingCraftPrefab = null;
        private static int AppliedCraftQuality = 0;

        /// <summary>
        /// The average quality of the ingredients a craft is about to spend, rounded down, weighted by how many of
        /// each are consumed. Ingredients without a quality level do not vote, and neither does one the recipe does
        /// not actually ask for at this level - the Ashlands infusion recipes set m_amountPerLevel 0 on the base
        /// weapon, so upgrading an infused weapon does not consume another one.
        ///
        /// Tiers come from IngredientQuality.SelectTier, the same call ConsumeResources makes moments later, so the
        /// quality handed out is always the quality that was actually spent.
        /// </summary>
        internal static int AverageIngredientTier(Recipe recipe, Inventory inventory, int baseQuality, int multiplier) {
            Piece.Requirement[] resources = recipe.m_resources;
            if (resources == null || inventory == null) { return 0; }

            int weightedTiers = 0;
            int itemsSpent = 0;
            foreach (Piece.Requirement requirement in resources) {
                if (IngredientQuality.HasQuality(requirement) == false) { continue; }

                int required = requirement.GetAmount(baseQuality) * multiplier;
                if (required <= 0) { continue; }

                // A tier of 0 means no single tier covers the cost, which HaveRequirementItems would already have
                // blocked - outside of the no cost cheats, where counting it as 1 keeps the craft at vanilla quality.
                int tier = IngredientQuality.SelectTier(inventory, requirement.m_resItem.m_itemData, required);
                weightedTiers += Mathf.Max(1, tier) * required;
                itemsSpent += required;
            }
            if (itemsSpent <= 0) { return 0; }

            // Integer division is the round down the average is supposed to get.
            return weightedTiers / itemsSpent;
        }

        /// <summary>
        /// The quality an equipment craft has earned from its ingredients, or 0 when it has earned nothing above
        /// what vanilla would have given. Never lowers a result: baseQuality is vanilla's own answer, so upgrading
        /// with junk ingredients behaves exactly as it always did.
        /// </summary>
        internal static int CraftedItemQuality(Recipe recipe, ItemDrop.ItemData upgradeItem, int multiplier) {
            if (ValConfig.ScaleCraftedEquipmentQuality.Value == false || Player.m_localPlayer == null) { return 0; }

            if (IngredientQuality.Classify(recipe, out int _) != IngredientQuality.CraftMode.ItemQuality) { return 0; }

            int baseQuality = upgradeItem != null ? upgradeItem.m_quality + 1 : 1;
            int earned = AverageIngredientTier(recipe, Player.m_localPlayer.GetInventory(), baseQuality, multiplier);
            if (earned <= baseQuality) { return 0; }

            earned = Mathf.Min(earned, recipe.m_item.m_itemData.m_shared.m_maxQuality);
            // Vanilla gates every quality level behind a station level (Recipe.GetRequiredStationLevel is
            // max(1, m_minStationLevel) + quality - 1). Handing out a quality the station could not have crafted
            // would skip that progression entirely, so invert the same expression and cap by what it allows.
            if (recipe.m_craftingStation != null || recipe.m_repairStation != null) {
                CraftingStation station = Player.m_localPlayer.GetCurrentCraftingStation();
                int stationAllows = (station != null ? station.GetLevel() : 0) - Mathf.Max(1, recipe.m_minStationLevel) + 1;
                earned = Mathf.Min(earned, stationAllows);
            }

            return earned > baseQuality ? earned : 0;
        }

        private static int GetCraftingItemBonusAmount(InventoryGui instance, int base_amount_crafted, float skill_factor, float player_skill_level, Skills.SkillType craftingSkill) {
            int craftingItemBonusAmount = 0;
            
            if (craftingSkill == Skills.SkillType.Cooking && (ValConfig.EnableCookingBonusItems.Value == false || ValConfig.RequiredLevelForBonusCookingItems.Value > player_skill_level)) {
                Logger.LogDebug($"Cooking bonus not enabled ({ValConfig.EnableCookingBonusItems.Value}) or skill level too low ({player_skill_level} < {ValConfig.RequiredLevelForBonusCookingItems.Value})");
                return craftingItemBonusAmount;
            } else if (craftingSkill == Skills.SkillType.Crafting && (ValConfig.EnableBonusItemCrafting.Value == false || ValConfig.CraftingBonusCraftsLevel.Value > player_skill_level)) {
                Logger.LogDebug($"Crafting bonus not enabled ({ValConfig.EnableBonusItemCrafting.Value}) or skill level too low ({player_skill_level} < {ValConfig.CraftingBonusCraftsLevel.Value})");
                return craftingItemBonusAmount;
            }
            float success_chance;
            if (craftingSkill == Skills.SkillType.Cooking) {
                success_chance = ValConfig.ChanceForCookingBonusItems.Value * skill_factor;
            } else {
                success_chance = ValConfig.CraftingBonusChance.Value * skill_factor;
            }

            int bonusAmount = 1;
            // Bonus amount improvements for things like Nails
            if (craftingSkill != Skills.SkillType.Cooking && instance.m_craftRecipe.m_amount > 1 && ValConfig.EnableCraftBonusAsFraction.Value) {
                bonusAmount = Mathf.RoundToInt(instance.m_craftRecipe.m_amount * ValConfig.CraftBonusFractionOfCraftNumber.Value);
                Logger.LogDebug($"Bonus updated now {bonusAmount}, using fraction of result.");
            }

            int maxItems;
            if (craftingSkill == Skills.SkillType.Cooking) {
                maxItems = ValConfig.CookingBonusItemMaxAmount.Value;
            } else {
                maxItems = ValConfig.CraftingMaxBonus.Value;
            }

            for (int index = 1; index <= maxItems; ++index) {
                float roll = UnityEngine.Random.Range(0, 1f);
                Logger.LogDebug($"Bonus crafting roll {index}: {success_chance} >= {roll}");
                if (success_chance >= roll) {
                    craftingItemBonusAmount += bonusAmount;
                } else {
                    break;
                }
            }
            Logger.LogDebug($"Crafting {instance.m_craftRecipe.m_item.m_itemData.m_shared.m_name} with new total {base_amount_crafted} + (bonus) {craftingItemBonusAmount}.");
            return craftingItemBonusAmount;
        }

        [HarmonyPatch(typeof(Humanoid))]
        public static class BlockDurabilityReduction
        {
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(Humanoid.BlockAttack))]
            public static IEnumerable<CodeInstruction> Transpiler(
              IEnumerable<CodeInstruction> instructions)
            {
                CodeMatcher codeMatcher = new CodeMatcher(instructions, (ILGenerator)null);
                if (codeMatcher.TryMatchStartForward("Unable to patch Block Durability reduction.",
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof (ItemDrop.ItemData.SharedData), "m_useDurabilityDrain"), (string) null)
                )) {
                    codeMatcher.Advance(1).RemoveInstructions(4).InsertAndAdvance(
                        Transpilers.EmitDelegate(Crafting.CheckAndReduceDurabilityCost)
                    );
                }
                return codeMatcher.Instructions();
            }
        }

        [HarmonyPatch(typeof(Attack))]
        public static class RangedAttackReduceDurabilityCost
        {
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(Attack.ProjectileAttackTriggered))]
            public static IEnumerable<CodeInstruction> Transpiler(
              IEnumerable<CodeInstruction> instructions)
            {
                CodeMatcher codeMatcher = new CodeMatcher(instructions, (ILGenerator)null);
                if (codeMatcher.TryMatchStartForward("Unable to patch Ranged attack durability reduction.",
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof (ItemDrop.ItemData.SharedData), "m_useDurabilityDrain"), (string) null))) {
                    codeMatcher.Advance(1)
                    .InsertAndAdvance(
                        Transpilers.EmitDelegate(Crafting.CheckAndReduceDurabilityCost));
                }
                return (IEnumerable<CodeInstruction>)codeMatcher.Instructions();
            }
        }

        [HarmonyPatch(typeof(Attack))]
        public static class MeleeAttackReduceDurabilityCost
        {
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(Attack.DoMeleeAttack))]
            public static IEnumerable<CodeInstruction> Transpiler(
              IEnumerable<CodeInstruction> instructions)
            {
                CodeMatcher codeMatcher = new CodeMatcher(instructions, (ILGenerator)null);
                if (codeMatcher.TryMatchStartForward("Unable to patch Melee attack durability reduction.",
                    new CodeMatch(new OpCode?(OpCodes.Ldfld), (object) AccessTools.Field(typeof (ItemDrop.ItemData.SharedData), "m_useDurabilityDrain"), (string) null)
                )) {
                    codeMatcher.Advance(1)
                    .InsertAndAdvance(Transpilers.EmitDelegate(CheckAndReduceDurabilityCost));
                }
                return (IEnumerable<CodeInstruction>)codeMatcher.Instructions();
            }
        }

        [HarmonyPatch(typeof(Attack))]
        public static class DoNonAttackReduceDurabilityCost
        {
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(Attack.DoNonAttack))]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
                CodeMatcher codeMatcher = new CodeMatcher(instructions, (ILGenerator)null);
                if (codeMatcher.TryMatchStartForward("Unable to patch DoNonAttack durability reduction.",
                    new CodeMatch(OpCodes.Ldfld, (object) AccessTools.Field(typeof (ItemDrop.ItemData.SharedData), "m_useDurabilityDrain")))) {
                    codeMatcher.Advance(1)
                    .InsertAndAdvance(Transpilers.EmitDelegate<Func<float, float>>(new Func<float, float>(Crafting.CheckAndReduceDurabilityCost)));
                }
                return (IEnumerable<CodeInstruction>)codeMatcher.Instructions();
            }
        }

        [HarmonyPatch(typeof(InventoryGui))]
        public static class CraftingItemBonusDropsPatch
        {
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(InventoryGui.DoCrafting))]
            static IEnumerable<CodeInstruction> ConstructorTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
                CodeMatcher codeMatcher = new CodeMatcher(instructions, generator);

                // Vanilla rolls its own craft bonus:
                //     int num4 = 0;
                //     if (currentCraftingStation != null && ...) { for (...) { num4 += m_craftBonusAmount; num3 += num4; } }
                // We replace that whole block with our own roll. 1.0 broke the previous version of this patch
                // twice over: currentCraftingStation moved to local 1 so it loads via the compact ldloc.1 rather
                // than ldloc.s and the anchor stopped matching, and the crafted amount moved off local 2, which is
                // now a bool. Match on opcode families and read the locals out of vanilla instead of naming them.
                if (!codeMatcher.TryMatchStartForward("Unable to patch Crafting bonus.",
                    new CodeMatch(OpCodes.Ldc_I4_0),
                    new CodeMatch(instr => instr.IsStloc()),                        // int num4 = 0;
                    new CodeMatch(instr => instr.IsLdloc()),                        // currentCraftingStation
                    new CodeMatch(OpCodes.Ldnull),
                    new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Object), "op_Inequality")),
                    new CodeMatch(instr => instr.opcode == OpCodes.Brfalse || instr.opcode == OpCodes.Brfalse_S))) {
                    return codeMatcher.Instructions();
                }
                int blockStart = codeMatcher.Pos;

                // Where vanilla jumps when it skips the bonus block is exactly where we want to land, so borrow
                // its own branch target instead of counting instructions to the end of the block.
                if (!(codeMatcher.InstructionAt(5).operand is Label afterVanillaBonus)) {
                    Logger.LogWarning("Unable to patch Crafting bonus. Vanilla's bonus block no longer branches to a label. Skipping this patch.");
                    return codeMatcher.Instructions();
                }

                // Vanilla's own "num3 += num4" names the local holding the crafted amount, so read the load and
                // store out of it rather than hardcoding an index the next update will shuffle again.
                if (!codeMatcher.TryMatchStartForward("Unable to patch Crafting bonus.",
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(InventoryGui), nameof(InventoryGui.m_craftBonusAmount))),
                    new CodeMatch(OpCodes.Add),
                    new CodeMatch(instr => instr.IsStloc()),                        // num4 +=
                    new CodeMatch(instr => instr.IsLdloc()),                        // num3
                    new CodeMatch(instr => instr.IsLdloc()),                        // num4
                    new CodeMatch(OpCodes.Add),
                    new CodeMatch(instr => instr.IsStloc()))) {                     // num3 =
                    return codeMatcher.Instructions();
                }
                CodeInstruction loadAmount = codeMatcher.InstructionAt(3).Clone();
                CodeInstruction storeAmount = codeMatcher.InstructionAt(6).Clone();

                // Insert after "num4 = 0" rather than before it: that leaves the local definitely assigned and
                // leaves any labels pointing at the head of the block on an instruction that still runs.
                codeMatcher.Start().Advance(blockStart + 2).Insert(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    loadAmount,
                    Transpilers.EmitDelegate(Crafting.CraftableBonus),
                    storeAmount,
                    new CodeInstruction(OpCodes.Br, afterVanillaBonus));

                return codeMatcher.Instructions();
            }
        }

        [HarmonyPatch(typeof(InventoryGui))]
        public static class CraftingItemRefundPatch
        {
            [HarmonyTranspiler]
            [HarmonyPatch("DoCrafting")]
            public static IEnumerable<CodeInstruction> Transpiler(
              IEnumerable<CodeInstruction> instructions)
            {
                CodeMatcher codeMatcher = new CodeMatcher(instructions, (ILGenerator)null);
                if (codeMatcher.TryMatchStartForward("Unable to patch Crafting Refunds.",
                    new CodeMatch(new OpCode?(OpCodes.Callvirt), (object) AccessTools.Method(typeof (Player), "ConsumeResources", (Type[]) null, (Type[]) null), (string) null)
                )) {
                    // No local is loaded here on purpose. This used to push local 14 as a "how many were
                    // crafted" argument that DetermineCraftingRefund never read, and 1.0 renumbered that local
                    // to a bool - so it was feeding a bool to an int parameter that was then ignored.
                    codeMatcher.Advance(1).InsertAndAdvance(new CodeInstruction[2]
                    {
          new CodeInstruction(OpCodes.Ldarg_0, (object) null),
          Transpilers.EmitDelegate<Action<InventoryGui>>(new Action<InventoryGui>(Crafting.DetermineCraftingRefund))
                    });
                }
                return (IEnumerable<CodeInstruction>)codeMatcher.Instructions();
            }
        }

        /// <summary>
        /// Recipes that eat something with a quality level and produce equipment craft it better rather than
        /// crafting more of it. The Fishing Hat wants one of every fish; the Ashlands infusion recipes eat a base
        /// weapon that may itself be four stars, and vanilla throws all of that away and hands back a quality 1
        /// result.
        ///
        /// DoCrafting passes one local to both Inventory.AddItem and Player.ConsumeResources, and only the first
        /// may change - the player pays quality 1 material costs and receives a better item, which is the reward.
        /// That argument sits four pushes deep in the AddItem call, so rather than guess a local slot index this
        /// arms a value here and applies it in an AddItem prefix. No IL assumptions, nothing to re-verify against
        /// the next game build.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
        public static class CraftedItemQualityPatch {
            [HarmonyPrefix]
            private static void Prefix(InventoryGui __instance) {
                PendingCraftQuality = 0;
                PendingCraftPrefab = null;
                AppliedCraftQuality = 0;
                if (__instance.m_craftRecipe == null) { return; }

                // The same two values DoCrafting itself is about to use, so the tier we price is the tier it spends.
                int multiplier = __instance.m_multiCrafting ? __instance.m_multiCraftAmount : 1;
                int quality = CraftedItemQuality(__instance.m_craftRecipe, __instance.m_craftUpgradeItem, multiplier);
                if (quality <= 0) { return; }

                PendingCraftQuality = quality;
                PendingCraftPrefab = __instance.m_craftRecipe.m_item.gameObject.name;
                Logger.LogDebug($"Ingredient quality earns {PendingCraftPrefab} a quality of {quality}.");
            }

            [HarmonyPostfix]
            private static void Postfix(InventoryGui __instance) {
                if (AppliedCraftQuality > 0 && Player.m_localPlayer != null && DamageText.instance != null) {
                    Vector3 playerUpPos = Player.m_localPlayer.transform.position + Vector3.up;
                    string label = LocalizationManager.Instance.TryTranslate("$craft_quality_bonus");
                    DamageText.instance.ShowText(DamageText.TextType.Bonus, playerUpPos, label.Replace("{0}", AppliedCraftQuality.ToString()), true);
                    __instance.m_craftBonusEffect.Create(playerUpPos, Quaternion.identity, null, 1f, -1);
                }
                // Still armed means DoCrafting returned before the item was added. Either way nothing may survive
                // the call.
                PendingCraftQuality = 0;
                PendingCraftPrefab = null;
                AppliedCraftQuality = 0;
            }
        }

        /// <summary>
        /// Applies the armed quality to the single item the craft creates. Matching on the prefab name keeps this
        /// off anything else added during DoCrafting - the material refund above runs inside the same call, though
        /// that goes through the AddItem(GameObject, int) overload and could not be caught here anyway.
        /// </summary>
        // string name, int stack, int quality, int variant, long crafterID, string crafterName, Vector2i position, bool cheated, bool pickedUp = false, bool dropIfFullInv = true
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new Type[] { typeof(string), typeof(int), typeof(int), typeof(int), typeof(long), typeof(string), typeof(Vector2i), typeof(bool), typeof(bool), typeof(bool) })]
        public static class CraftedItemQualityAddPatch {
            [HarmonyPrefix]
            private static void Prefix(string name, ref int quality) {
                if (PendingCraftQuality <= 0 || name != PendingCraftPrefab) { return; }

                // One shot: whatever else this craft adds, it adds at its own quality.
                if (quality < PendingCraftQuality) {
                    AppliedCraftQuality = PendingCraftQuality;
                    quality = PendingCraftQuality;
                }
                PendingCraftQuality = 0;
                PendingCraftPrefab = null;
            }
        }

        /// <summary>
        /// Shows the quality the selected recipe would produce before it is crafted, the same way the amount
        /// preview does.
        ///
        /// This appends to the finished label rather than touching UpdateRecipe's own quality local, which feeds
        /// SetupRequirementList, GetRequiredStationLevel, HaveRequirements and the craft button caption - raising
        /// that would price the recipe at upgrade material costs and relabel the button "Upgrade".
        /// </summary>
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateRecipe))]
        public static class CraftedItemQualityPreviewPatch {
            [HarmonyPostfix]
            private static void Postfix(InventoryGui __instance) {
                if (ValConfig.ScaleCraftedEquipmentQuality.Value == false || Player.m_localPlayer == null) { return; }

                Recipe recipe = __instance.m_selectedRecipe.Recipe;
                if (recipe == null) { return; }

                int quality = CraftedItemQuality(recipe, __instance.m_selectedRecipe.ItemData, IngredientQuality.PanelCraftMultiplier());
                if (quality <= 0) { return; }

                string label = LocalizationManager.Instance.TryTranslate("$craft_quality_bonus");
                __instance.m_recipeName.text = $"{__instance.m_recipeName.text} <color=orange>{label.Replace("{0}", quality.ToString())}</color>";
            }
        }
    }
}