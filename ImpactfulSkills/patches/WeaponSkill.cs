using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;
using System.Linq;
using System;
using static ItemDrop;
using UnityEngine;

namespace ImpactfulSkills.patches
{
    internal class WeaponSkill
    {
        // ItemData item, int qualityLevel, bool crafting, float worldLevel, int stackOverride = -1
        [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip), new Type[] { typeof(ItemData), typeof(int), typeof(bool), typeof(float), typeof(int) })]
        public static class ItemDisplay
        {
            public static void Postfix(ItemData item, ref String __result) {
                // Guard clause due to the postfixed method also having scenarios it can be called without the player defined
                if (ValConfig.EnableWeaponSkill.Value == false || Player.m_localPlayer == null) { return; }
                List<String> entry_lines = __result.Split('\n').ToList();
                List<String> result_lines = new List<string>(entry_lines);
                for (int i = 0; i < entry_lines.Count; i++) {
                    // Logger.LogDebug($"Checking line: {entry_lines[i]}");
                    if (entry_lines[i].Contains("item_staminause")) {
                        float player_skill = Player.m_localPlayer.GetSkillFactor(item.m_shared.m_skillType);
                        result_lines[i] = "$item_staminause: <color=orange>" + item.m_shared.m_attack.m_attackStamina + "</color> <color=yellow>(" + Mathf.RoundToInt(ModifyWeaponStaminaCostBySkillLevels(item.m_shared.m_attack.m_attackStamina, player_skill)) + ")</color>";
                        continue;
                    }
                    if (entry_lines[i].Contains("item_staminahold"))
                    {
                        float player_skill = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Bows); // drawStaminaDrain - drawStaminaDrain * 0.33f * skillFactor;
                        result_lines[i] = "$item_staminahold: <color=orange>" + item.m_shared.m_attack.m_drawStaminaDrain + "</color> <color=yellow>(" + Mathf.RoundToInt(item.m_shared.m_attack.m_drawStaminaDrain - item.m_shared.m_attack.m_drawStaminaDrain *  ValConfig.WeaponSkillBowDrawStaminaCostReduction.Value * player_skill) + ")</color>/s";
                        continue;
                    }
                }
                __result = String.Join("\n", result_lines);
            }
        }

        [HarmonyPatch(typeof(ItemDrop.ItemData))]
        public static class ModifyStaimaDrainBows
        {
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(ItemDrop.ItemData.GetDrawStaminaDrain))]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
            {
                var codeMatcher = new CodeMatcher(instructions);
                codeMatcher.MatchStartForward(
                    new CodeMatch(OpCodes.Dup),
                    new CodeMatch(OpCodes.Ldc_R4, 0.33f)
                    ).Advance(1).RemoveInstructions(1).InsertAndAdvance(
                    Transpilers.EmitDelegate(ModifyStaminaDrainCostForBow)
                    ).ThrowIfNotMatch("Unable to patch Stamina reduction for bows.");

                return codeMatcher.Instructions();
            }
        }

        [HarmonyPatch(typeof(Humanoid))]
        public static class ParryGivesBonusXP
        {
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(Humanoid.BlockAttack))]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
            {
                var codeMatcher = new CodeMatcher(instructions);
                codeMatcher.MatchStartForward(
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Humanoid), nameof(Humanoid.m_perfectBlockEffect)))
                    ).Advance(2).InsertAndAdvance(
                    Transpilers.EmitDelegate(ExtraXPForParryBlock)
                    ).ThrowIfNotMatch("Unable to patch extra XP for Parry block.");

                return codeMatcher.Instructions();
            }
        }


        //[HarmonyPatch(typeof(ItemDrop.ItemData))]
        //public static class DisplayStaminaCostReduction
        //{
        //    // [HarmonyDebug]
        //    [HarmonyTranspiler]
        //    [HarmonyPatch(nameof(ItemDrop.ItemData.GetTooltip))]
        //    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
        //    {
        //        var codeMatcher = new CodeMatcher(instructions);
        //        codeMatcher.MatchStartForward(
        //            new CodeMatch(OpCodes.Ble_Un_S, IL_0442),
        //            new CodeMatch(OpCodes.Ldsfld),
        //            new CodeMatch(OpCodes.Ldstr),
        //            new CodeMatch(OpCodes.Ldarg_0),
        //            new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.m_shared))),
        //            new CodeMatch(OpCodes.Ldfld),
        //            new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Attack), nameof(Attack.m_attackStamina))),
        //            new CodeMatch(OpCodes.Box),
        //            new CodeMatch(OpCodes.Callvirt),
        //            new CodeMatch(OpCodes.Pop)
        //            ).Advance(1).RemoveInstructions(7).InsertAndAdvance(
        //            new CodeInstruction(OpCodes.Ldarg_0),
        //            Transpilers.EmitDelegate(BuildStringForItemDescription)
        //            ).ThrowIfNotMatch("Unable to patch stamina decrease.");

        //        return codeMatcher.Instructions();
        //    }
        //}



        [HarmonyPatch(typeof(Attack))]
        public static class ModifyStaimaDrainWeapons
        {
            [HarmonyDebug]
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(Attack.GetAttackStamina))]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
            {
                var codeMatcher = new CodeMatcher(instructions);
                codeMatcher.MatchStartForward(
                    new CodeMatch(OpCodes.Ldloc_0),
                    new CodeMatch(OpCodes.Ldloc_0),
                    new CodeMatch(OpCodes.Ldc_R4, 0.33f),
                    new CodeMatch(OpCodes.Mul),
                    new CodeMatch(OpCodes.Ldloc_1),
                    new CodeMatch(OpCodes.Mul),
                    new CodeMatch(OpCodes.Sub),
                    new CodeMatch(OpCodes.Stloc_0)
                    ).Advance(1).RemoveInstructions(6).InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldloc_1),
                    Transpilers.EmitDelegate(ModifyWeaponStaminaCostBySkillLevels)
                    ).ThrowIfNotMatch("Unable to patch tooltip display for stamina cost reduction.");

                return codeMatcher.Instructions();
            }
        }

        private static void BuildStringForItemDescription(StringBuilder sb, ItemDrop.ItemData instance) {
            // Need a toggle for enable here and a return of the default otherwise
            if (ValConfig.EnableWeaponSkill.Value == true) {
                sb.Append("\n$item_staminause: <color=orange>" + instance.m_shared.m_attack.m_attackStamina + " (" + (ModifyWeaponStaminaCostBySkillLevels(instance.m_shared.m_attack.m_attackStamina, Player.m_localPlayer.GetSkillFactor(instance.m_shared.m_skillType)) * -1) + ")</color>");
            } else {
                sb.Append("\n$item_staminause: <color=orange>" + instance.m_shared.m_attack.m_attackStamina + "</color>");
            }
        }

        private static float ModifyStaminaDrainCostForBow() {
            if (ValConfig.EnableWeaponSkill.Value != true || Player.m_localPlayer == null) { return 0.33f; }

            return ValConfig.WeaponSkillBowDrawStaminaCostReduction.Value;
        }


        private static float ModifyWeaponStaminaCostBySkillLevels(float stam_cost, float skillfactor) {
            // Note even when disabled the stamina cost will be reduced because this is the vanilla formula
            if (ValConfig.EnableWeaponSkill.Value != true || Player.m_localPlayer == null) { 
                float vanilla_stam_reduction = stam_cost * 0.33f * skillfactor;
               //  Logger.LogDebug($"Weapon stamina reduction o:{stam_cost} r:{vanilla_stam_reduction}");
                return (stam_cost - vanilla_stam_reduction);
            }

            float stam_cost_reduction = stam_cost * ValConfig.WeaponSkillStaminaReduction.Value * skillfactor;
            // Logger.LogDebug($"Weapon stamina reduction o:{stam_cost} r:{stam_cost_reduction} s:{skillfactor}");
            return (stam_cost - stam_cost_reduction);
        }

        private static void ExtraXPForParryBlock() {
            if (ValConfig.EnableWeaponSkill.Value == true && Player.m_localPlayer != null) {
                Player.m_localPlayer.RaiseSkill(Skills.SkillType.Blocking, ValConfig.WeaponSkillParryBonus.Value);
            }
        }
    }
}
