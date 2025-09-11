using HarmonyLib;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;


namespace ImpactfulSkills.patches
{
    internal class Crafting
    {
        public static float CheckAndReduceDurabilityCost(float item_durability_drain)
        {
            if (ValConfig.EnableDurabilityLossPrevention.Value && (UnityEngine.Object)Player.m_localPlayer != (UnityEngine.Object)null)
            {
                float skillFactor = ((Character)Player.m_localPlayer).GetSkillFactor((Skills.SkillType)107);
                float num1 = UnityEngine.Random.value;
                float num2 = ValConfig.ChanceForDurabilityLossPrevention.Value;
                if (ValConfig.ScaleDurabilitySaveBySkillLevel.Value)
                    num2 *= skillFactor;
                if ((double)ValConfig.DurabilitySaveLevel.Value <= (double)skillFactor * 100.0 && (double)num1 < (double)num2)
                {
                    Logger.LogDebug(string.Format("Skipping durability usage {0} < {1}", (object)num1, (object)num2));
                    return 0.0f;
                }
            }
            return item_durability_drain;
        }

        private static int CraftableBonus(InventoryGui instance, int base_amount_crafted)
        {
            if (!ValConfig.EnableCrafting.Value || (UnityEngine.Object)Player.m_localPlayer == (UnityEngine.Object)null)
                return base_amount_crafted;
            int num = base_amount_crafted;
            CraftingStation currentCraftingStation = Player.m_localPlayer.GetCurrentCraftingStation();
            float skillFactor = ((Character)Player.m_localPlayer).GetSkillFactor((Skills.SkillType)107);
            float skillLevel = ((Character)Player.m_localPlayer).GetSkillLevel((Skills.SkillType)107);
            if ((UnityEngine.Object)currentCraftingStation != (UnityEngine.Object)null && instance.m_craftRecipe.m_item.m_itemData.m_shared.m_maxStackSize > 1)
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
        public static class PreserveDurabilityChance
        {
            [HarmonyPatch("DrainEquipedItemDurability")]
            private static bool Prefix()
            {
                if (ValConfig.EnableDurabilitySaves.Value && (UnityEngine.Object)Player.m_localPlayer != (UnityEngine.Object)null)
                {
                    float skillFactor = ((Character)Player.m_localPlayer).GetSkillFactor((Skills.SkillType)107);
                    float num1 = UnityEngine.Random.value;
                    float num2 = ValConfig.ChanceForDurabilityLossPrevention.Value;
                    if (ValConfig.ScaleDurabilitySaveBySkillLevel.Value)
                        num2 *= skillFactor;
                    if ((double)ValConfig.DurabilitySaveLevel.Value <= (double)skillFactor * 100.0 && (double)num1 < (double)num2)
                        return true;
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(Humanoid))]
        public static class BlockDurabilityReduction
        {
            [HarmonyTranspiler]
            [HarmonyPatch("BlockAttack")]
            private static IEnumerable<CodeInstruction> Transpiler(
              IEnumerable<CodeInstruction> instructions)
            {
                CodeMatcher codeMatcher = new CodeMatcher(instructions, (ILGenerator)null);
                codeMatcher.MatchStartForward(new CodeMatch[1]
                {
          new CodeMatch(new OpCode?(OpCodes.Ldfld), (object) AccessTools.Field(typeof (ItemDrop.ItemData.SharedData), "m_useDurabilityDrain"), (string) null)
                }).Advance(1).RemoveInstructions(4).InsertAndAdvance(new CodeInstruction[1]
                {
          Transpilers.EmitDelegate<Func<float, float>>(new Func<float, float>(Crafting.CheckAndReduceDurabilityCost))
                }).ThrowIfNotMatch("Unable to patch Block Durability reduction.", Array.Empty<CodeMatch>());
                return (IEnumerable<CodeInstruction>)codeMatcher.Instructions();
            }
        }

        [HarmonyPatch(typeof(Attack))]
        public static class RangedAttackReduceDurabilityCost
        {
            [HarmonyTranspiler]
            [HarmonyPatch("ProjectileAttackTriggered")]
            private static IEnumerable<CodeInstruction> Transpiler(
              IEnumerable<CodeInstruction> instructions)
            {
                CodeMatcher codeMatcher = new CodeMatcher(instructions, (ILGenerator)null);
                codeMatcher.MatchStartForward(new CodeMatch[1]
                {
          new CodeMatch(new OpCode?(OpCodes.Ldfld), (object) AccessTools.Field(typeof (ItemDrop.ItemData.SharedData), "m_useDurabilityDrain"), (string) null)
                }).Advance(1).InsertAndAdvance(new CodeInstruction[1]
                {
          Transpilers.EmitDelegate<Func<float, float>>(new Func<float, float>(Crafting.CheckAndReduceDurabilityCost))
                }).ThrowIfNotMatch("Unable to patch Ranged attack durability reduction.", Array.Empty<CodeMatch>());
                return (IEnumerable<CodeInstruction>)codeMatcher.Instructions();
            }
        }

        [HarmonyPatch(typeof(Attack))]
        public static class MeleeAttackReduceDurabilityCost
        {
            [HarmonyTranspiler]
            [HarmonyPatch("DoMeleeAttack")]
            private static IEnumerable<CodeInstruction> Transpiler(
              IEnumerable<CodeInstruction> instructions)
            {
                CodeMatcher codeMatcher = new CodeMatcher(instructions, (ILGenerator)null);
                codeMatcher.MatchStartForward(new CodeMatch[1]
                {
          new CodeMatch(new OpCode?(OpCodes.Ldfld), (object) AccessTools.Field(typeof (ItemDrop.ItemData.SharedData), "m_useDurabilityDrain"), (string) null)
                }).Advance(1).InsertAndAdvance(new CodeInstruction[1]
                {
          Transpilers.EmitDelegate<Func<float, float>>(new Func<float, float>(Crafting.CheckAndReduceDurabilityCost))
                }).ThrowIfNotMatch("Unable to patch Melee attack durability reduction.", Array.Empty<CodeMatch>());
                return (IEnumerable<CodeInstruction>)codeMatcher.Instructions();
            }
        }

        [HarmonyPatch(typeof(Attack))]
        public static class DoNonAttackReduceDurabilityCost
        {
            [HarmonyTranspiler]
            [HarmonyPatch("DoNonAttack")]
            private static IEnumerable<CodeInstruction> Transpiler(
              IEnumerable<CodeInstruction> instructions)
            {
                CodeMatcher codeMatcher = new CodeMatcher(instructions, (ILGenerator)null);
                codeMatcher.MatchStartForward(new CodeMatch[1]
                {
          new CodeMatch(new OpCode?(OpCodes.Ldfld), (object) AccessTools.Field(typeof (ItemDrop.ItemData.SharedData), "m_useDurabilityDrain"), (string) null)
                }).Advance(1).InsertAndAdvance(new CodeInstruction[1]
                {
          Transpilers.EmitDelegate<Func<float, float>>(new Func<float, float>(Crafting.CheckAndReduceDurabilityCost))
                }).ThrowIfNotMatch("Unable to patch DoNonAttack durability reduction.", Array.Empty<CodeMatch>());
                return (IEnumerable<CodeInstruction>)codeMatcher.Instructions();
            }
        }

        [HarmonyPatch(typeof(InventoryGui))]
        public static class CraftingItemBonusDropsPatch
        {
            [HarmonyTranspiler]
            [HarmonyPatch("DoCrafting")]
            private static IEnumerable<CodeInstruction> Transpiler(
              IEnumerable<CodeInstruction> instructions)
            {
                CodeMatcher codeMatcher = new CodeMatcher(instructions, (ILGenerator)null);
                codeMatcher.MatchStartForward(new CodeMatch[3]
                {
          new CodeMatch(new OpCode?(OpCodes.Ldloc_S), (object) null, (string) null),
          new CodeMatch(new OpCode?(OpCodes.Ldnull), (object) null, (string) null),
          new CodeMatch(new OpCode?(OpCodes.Call), (object) null, (string) null)
                }).RemoveInstructions(45).InsertAndAdvance(new CodeInstruction[4]
                {
          new CodeInstruction(OpCodes.Ldarg_0, (object) null),
          new CodeInstruction(OpCodes.Ldloc_2, (object) null),
          Transpilers.EmitDelegate<Func<InventoryGui, int, int>>(new Func<InventoryGui, int, int>(Crafting.CraftableBonus)),
          new CodeInstruction(OpCodes.Stloc_2, (object) null)
                }).ThrowIfNotMatch("Unable to patch Crafting bonus.", Array.Empty<CodeMatch>());
                return (IEnumerable<CodeInstruction>)codeMatcher.Instructions();
            }
        }

        [HarmonyPatch(typeof(InventoryGui))]
        public static class CraftingItemRefundPatch
        {
            [HarmonyTranspiler]
            [HarmonyPatch("DoCrafting")]
            private static IEnumerable<CodeInstruction> Transpiler(
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