using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;
using System.Linq;
using System;
using static ItemDrop;
using UnityEngine;
using ImpactfulSkills.common;

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
                        result_lines[i] = "$item_staminause: <color=orange>" + item.m_shared.m_attack.m_attackStamina + "</color> <color=yellow>(" + Mathf.RoundToInt(ModifyWeaponStaminaCostBySkillLevelInheritFactor(item.m_shared.m_attack.m_attackStamina, 0.33f, player_skill)) + ")</color>";
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
            //[HarmonyEmitIL("./dumps")]
            //[HarmonyDebug]
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(ItemDrop.ItemData.GetDrawStaminaDrain))]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
            {
                var codeMatcher = new CodeMatcher(instructions);
                codeMatcher.MatchStartForward(
                    new CodeMatch(OpCodes.Dup),
                    new CodeMatch(OpCodes.Ldc_R4)
                ).Advance(1).RemoveInstructions(1).InsertAndAdvance(
                    Transpilers.EmitDelegate(ModifyStaminaDrainCostForBow)
                ).ThrowIfNotMatch("Unable to patch Stamina reduction for bows compatibility.");

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


        [HarmonyPatch(typeof(Attack))]
        public static class ModifyStaimaDrainWeapons
        {
            //[HarmonyEmitIL("./dumps")]
            //[HarmonyDebug]
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(Attack.GetAttackStamina))]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
            {
                var codeMatcher = new CodeMatcher(instructions);
                codeMatcher.MatchStartForward(
                    new CodeMatch(OpCodes.Ldloc_0),
                    new CodeMatch(OpCodes.Ldloc_0),
                    new CodeMatch(OpCodes.Ldc_R4),
                    new CodeMatch(OpCodes.Mul),
                    new CodeMatch(OpCodes.Ldloc_1),
                    new CodeMatch(OpCodes.Mul),
                    new CodeMatch(OpCodes.Sub),
                    new CodeMatch(OpCodes.Stloc_0)
                )
                .RemoveInstruction()
                .Advance(2)
                .InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldloc_1),
                    Transpilers.EmitDelegate(ModifyWeaponStaminaCostBySkillLevelInheritFactor)
                )
                .RemoveInstructions(4)
                .ThrowIfNotMatch("Unable to patch stamina cost reduction modification, inherit modifier compatibility.");
                return codeMatcher.Instructions();
            }
        }

        private static float ModifyStaminaDrainCostForBow() {
            if (ValConfig.EnableWeaponSkill.Value != true || Player.m_localPlayer == null) { return 0.33f; }
            float skilled_cost = 0.33f - (ValConfig.WeaponSkillBowDrawStaminaCostReduction.Value * 0.33f * Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Bows));
            if (skilled_cost <0.01f) { skilled_cost = 0.01f; }
            return skilled_cost;
        }

        private static float ModifyWeaponStaminaCostBySkillLevelInheritFactor(float stam_cost, float reduce_factor, float skillfactor) {
            // Note even when disabled the stamina cost will be reduced because this is the vanilla formula
            if (ValConfig.EnableWeaponSkill.Value != true) {
                float vanilla_stam_reduction = stam_cost * reduce_factor * skillfactor;
                //  Logger.LogDebug($"Weapon stamina reduction o:{stam_cost} r:{vanilla_stam_reduction}");
                return (stam_cost - vanilla_stam_reduction);
            }

            float stam_cost_reduction = stam_cost * ValConfig.WeaponSkillStaminaReduction.Value * skillfactor;
            stam_cost_reduction *= (1 + reduce_factor); // Inherit vanilla/other mod reduction factor into the new reduction calculation
            //Logger.LogDebug($"Weapon stamina reduction o:{stam_cost} - r:{stam_cost_reduction} = {(stam_cost - stam_cost_reduction)} s:{skillfactor}");
            return (stam_cost - stam_cost_reduction);
        }


        private static void ExtraXPForParryBlock() {
            if (ValConfig.EnableWeaponSkill.Value == true && Player.m_localPlayer != null) {
                Player.m_localPlayer.RaiseSkill(Skills.SkillType.Blocking, ValConfig.WeaponSkillParryBonus.Value);
            }
        }
    }
}
