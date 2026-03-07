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
            if (!ValConfig.EnableCrafting.Value || Player.m_localPlayer == null)
                return base_amount_crafted;
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
            
            if (currentCraftingStation != null) {
                if (instance.m_craftRecipe.m_craftingStation.m_craftingSkill != Skills.SkillType.Cooking && instance.m_craftRecipe.m_item.m_itemData.m_shared.m_maxStackSize > 1) {
                    return base_amount_crafted;
                }
                craftedTotal += Crafting.GetCraftingItemBonusAmount(instance, base_amount_crafted, skillFactor, skillLevel, instance.m_craftRecipe.m_craftingStation.m_craftingSkill);
            }
                
            if (craftedTotal != base_amount_crafted) {
                Vector3 playerUpPos = Player.m_localPlayer.transform.position + Vector3.up;
                DamageText.instance.ShowText(DamageText.TextType.Bonus, playerUpPos, $"+{(craftedTotal - base_amount_crafted)}", true);
                instance.m_craftBonusEffect.Create(playerUpPos, Quaternion.identity, null, 1f, -1);
            }
            return craftedTotal;
        }

        private static void DetermineCraftingRefund(InventoryGui instance, int num_recipe_crafted)
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

        private static int GetCraftingItemBonusAmount(InventoryGui instance, int base_amount_crafted, float skill_factor, float player_skill_level, Skills.SkillType craftingSkill) {
            int craftingItemBonusAmount = 0;
            
            if (craftingSkill == Skills.SkillType.Cooking && (ValConfig.EnableCookingBonusItems.Value == false || ValConfig.RequiredLevelForBonusCookingItems.Value > player_skill_level)) {
                return craftingItemBonusAmount;
            } else if (ValConfig.EnableBonusItemCrafting.Value == false || player_skill_level < ValConfig.CraftingBonusCraftsLevel.Value) {
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
                codeMatcher.MatchStartForward(new CodeMatch[1]
                {
          new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof (ItemDrop.ItemData.SharedData), "m_useDurabilityDrain"), (string) null)
                }).Advance(1).RemoveInstructions(4).InsertAndAdvance(
                    Transpilers.EmitDelegate(Crafting.CheckAndReduceDurabilityCost)
                ).ThrowIfNotMatch("Unable to patch Block Durability reduction.", Array.Empty<CodeMatch>());
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
                codeMatcher.MatchStartForward(
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof (ItemDrop.ItemData.SharedData), "m_useDurabilityDrain"), (string) null))
                .Advance(1)
                .InsertAndAdvance(
                    Transpilers.EmitDelegate(Crafting.CheckAndReduceDurabilityCost))
                .ThrowIfNotMatch("Unable to patch Ranged attack durability reduction.", Array.Empty<CodeMatch>());
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
                codeMatcher.MatchStartForward(new CodeMatch[1]
                {
          new CodeMatch(new OpCode?(OpCodes.Ldfld), (object) AccessTools.Field(typeof (ItemDrop.ItemData.SharedData), "m_useDurabilityDrain"), (string) null)
                }).Advance(1)
                .InsertAndAdvance(Transpilers.EmitDelegate(CheckAndReduceDurabilityCost))
                .ThrowIfNotMatch("Unable to patch Melee attack durability reduction.", Array.Empty<CodeMatch>());
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
                codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldfld, (object) AccessTools.Field(typeof (ItemDrop.ItemData.SharedData), "m_useDurabilityDrain")))
                .Advance(1)
                .InsertAndAdvance(Transpilers.EmitDelegate<Func<float, float>>(new Func<float, float>(Crafting.CheckAndReduceDurabilityCost)))
                .ThrowIfNotMatch("Unable to patch DoNonAttack durability reduction.", Array.Empty<CodeMatch>());
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
                codeMatcher.MatchStartForward(
                    // int num4 = 0;
                    new CodeMatch(OpCodes.Ldc_I4_0),
                    new CodeMatch(OpCodes.Stloc_S), // Convert.ToSByte(6)
                    new CodeMatch(OpCodes.Ldloc_S), // Convert.ToSByte(5)
                    new CodeMatch(OpCodes.Ldnull),
                    new CodeMatch(OpCodes.Call))
                .Advance(2)
                .InsertAndAdvance(
                  new CodeInstruction(OpCodes.Ldarg_0),
                  new CodeInstruction(OpCodes.Ldloc_2),
                  Transpilers.EmitDelegate(Crafting.CraftableBonus),
                  new CodeInstruction(OpCodes.Stloc_2)
                )
                .CreateLabelOffset(out Label label, offset: 45)
                .InsertAndAdvance(new CodeInstruction(OpCodes.Br, label))
                .ThrowIfNotMatch("Unable to patch Crafting bonus.");
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
                codeMatcher.MatchStartForward(new CodeMatch[1]
                {
          new CodeMatch(new OpCode?(OpCodes.Callvirt), (object) AccessTools.Method(typeof (Player), "ConsumeResources", (Type[]) null, (Type[]) null), (string) null)
                }).Advance(1).InsertAndAdvance(new CodeInstruction[3]
                {
          new CodeInstruction(OpCodes.Ldarg_0, (object) null),
          new CodeInstruction(OpCodes.Ldloc_S, (object) 14),
          Transpilers.EmitDelegate<Action<InventoryGui, int>>(new Action<InventoryGui, int>(Crafting.DetermineCraftingRefund))
                }).ThrowIfNotMatch("Unable to patch Crafting Refunds.", Array.Empty<CodeMatch>());
                return (IEnumerable<CodeInstruction>)codeMatcher.Instructions();
            }
        }
    }
}