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
            int num = base_amount_crafted;
            CraftingStation currentCraftingStation = Player.m_localPlayer.GetCurrentCraftingStation();
            float skillFactor = (Player.m_localPlayer).GetSkillFactor((Skills.SkillType)107);
            float skillLevel = (Player.m_localPlayer).GetSkillLevel((Skills.SkillType)107);
            if (currentCraftingStation != null && instance.m_craftRecipe.m_item.m_itemData.m_shared.m_maxStackSize > 1)
                num += Crafting.GetCraftingItemBonusAmount(instance, base_amount_crafted, skillFactor, skillLevel);
            if (num != base_amount_crafted)
            {
                Vector3 vector3 = ((Component)Player.m_localPlayer).transform.position + Vector3.up;
                DamageText.instance.ShowText((DamageText.TextType)7, vector3, string.Format("+{0}", (object)(num - base_amount_crafted)), true);
                instance.m_craftBonusEffect.Create(vector3, Quaternion.identity, (Transform)null, 1f, -1);
            }
            return num;
        }

        private static void DetermineCraftingRefund(InventoryGui instance, int num_recipe_crafted)
        {
            float skillLevel = ((Character)Player.m_localPlayer).GetSkillLevel((Skills.SkillType)107);
            float skillFactor = ((Character)Player.m_localPlayer).GetSkillFactor((Skills.SkillType)107);
            if (!ValConfig.EnableMaterialReturns.Value || (double)skillLevel < (double)ValConfig.CraftingMaterialReturnsLevel.Value)
                return;
            Dictionary<ItemDrop, int> dictionary = new Dictionary<ItemDrop, int>();
            if (instance.m_craftRecipe.m_requireOnlyOneIngredient)
            {
                Logger.LogDebug("Require any resource recipes do not get a refund.");
            }
            else
            {
                foreach (Piece.Requirement resource in instance.m_craftRecipe.m_resources)
                {
                    float num1 = UnityEngine.Random.value;
                    float num2 = ValConfig.ChanceForMaterialReturn.Value * skillFactor;
                    Logger.LogDebug(string.Format("Checking refund chance for {0} {1} < {2}", (object)((UnityEngine.Object)resource.m_resItem).name, (object)num1, (object)num2));
                    if ((double)num1 <= (double)num2)
                    {
                        if (resource.m_amount > 1)
                        {
                            int num3 = Mathf.RoundToInt((float)resource.m_amount * (ValConfig.MaxCraftingMaterialReturnPercent.Value * skillFactor));
                            dictionary.Add(resource.m_resItem, num3);
                        }
                        else if ((double)num1 < (double)num2 / 2.0)
                            dictionary.Add(resource.m_resItem, 1);
                    }
                }
                if (dictionary.Count == 0)
                    return;
                Vector3 vector3 = ((Component)Player.m_localPlayer).transform.position + Vector3.up;
                DamageText.instance.ShowText((DamageText.TextType)7, vector3, LocalizationManager.Instance.TryTranslate("$craft_refund"), true);
                instance.m_craftBonusEffect.Create(vector3, Quaternion.identity, (Transform)null, 1f, -1);
                foreach (KeyValuePair<ItemDrop, int> keyValuePair in dictionary)
                {
                    bool flag = ((Humanoid)Player.m_localPlayer).GetInventory().AddItem(((Component)keyValuePair.Key).gameObject, keyValuePair.Value);
                    Logger.LogDebug(string.Format("Refund to add: {0} {1} | refunded? {2}", (object)((UnityEngine.Object)keyValuePair.Key).name, (object)keyValuePair.Value, (object)flag));
                }
            }
        }

        private static int GetCraftingItemBonusAmount(
          InventoryGui instance,
          int base_amount_crafted,
          float skill_factor,
          float player_skill_level)
        {
            int craftingItemBonusAmount = 0;
            if (!ValConfig.EnableBonusItemCrafting.Value || (double)player_skill_level < (double)ValConfig.CraftingBonusCraftsLevel.Value)
                return craftingItemBonusAmount;
            float num1 = ValConfig.CraftingBonusChance.Value * skill_factor;
            int num2 = 1;
            if (instance.m_craftRecipe.m_amount > 1 && ValConfig.EnableCraftBonusAsFraction.Value)
            {
                num2 = Mathf.RoundToInt((float)instance.m_craftRecipe.m_amount * ValConfig.CraftBonusFractionOfCraftNumber.Value);
                Logger.LogDebug(string.Format("Bonus updated now {0}, using fraction of result.", (object)num2));
            }
            for (int index = 0; index <= ValConfig.CraftingMaxBonus.Value; ++index)
            {
                float num3 = UnityEngine.Random.value;
                Logger.LogDebug(string.Format("Bonus crafting roll {0}: {1} >= {2}", (object)index, (object)num1, (object)num3));
                if ((double)num1 >= (double)num3)
                    craftingItemBonusAmount += num2;
                else
                    break;
            }
            Logger.LogDebug(string.Format("Crafting {0} with new total {1} + (bonus) {2}.", (object)instance.m_craftRecipe.m_item.m_itemData.m_shared.m_name, (object)base_amount_crafted, (object)craftingItemBonusAmount));
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