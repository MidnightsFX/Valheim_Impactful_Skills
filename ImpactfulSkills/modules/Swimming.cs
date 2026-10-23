using HarmonyLib;
using ImpactfulSkills.common;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace ImpactfulSkills.patches
{
    public static class Swimming
    {
        [HarmonyPatch(typeof(Character))]
        public static class SwimmingSpeedPatch
        {
            [HarmonyTranspiler]
            [HarmonyPatch("UpdateSwimming")]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
                CodeMatcher codeMatcher = new CodeMatcher(instructions, (ILGenerator)null);
                if (codeMatcher.TryMatchStartForward("Unable to patch Swim skill movement increase.",
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof (Character), "m_swimSpeed"))
                )) {
                    codeMatcher.RemoveInstruction()
                    .InsertAndAdvance(
                        Transpilers.EmitDelegate(ModifySwimSpeedbySkill)
                    );
                }
                return codeMatcher.Instructions();
            }

            public static float ModifySwimSpeedbySkill(Character __instance) {
                if (!ValConfig.EnableSwimming.Value) { return __instance.m_swimSpeed; }
                float skillFactor = __instance.GetSkillFactor(Skills.SkillType.Swim);
                if (ValConfig.SwimSpeedRequiredLevel.Value > skillFactor*100) { return __instance.m_swimSpeed; }
                float swim_increase_speed = 1 + (ValConfig.SwimmingSpeedFactor.Value * SkillCaps.BonusFactor(skillFactor, ValConfig.SwimmingBonusesPastLevel100));
                return __instance.m_swimSpeed + swim_increase_speed;
            }
        }

        [HarmonyPatch(typeof(Player))]
        public static class SwimmingReduceStaminaCostPatch
        {
            //[HarmonyEmitIL("./dumps")]
            [HarmonyTranspiler]
            [HarmonyPatch("OnSwimming")]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                CodeMatcher codeMatcher = new CodeMatcher(instructions, (ILGenerator)null);
                if (codeMatcher.TryMatchStartForward("Unable to patch Swim Stamina cost reduction.",
                    new CodeMatch(OpCodes.Mul),
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(Character), nameof(Character.UseStamina)))
                )) {
                    codeMatcher.Advance(1)
                    .InsertAndAdvance(
                        // new CodeInstruction(OpCodes.Ldarg_0),
                        Transpilers.EmitDelegate(ModifySwimCost)
                    );
                }
                return codeMatcher.Instructions();
            }

            public static float ModifySwimCost(float swimCost) {
                if (!ValConfig.EnableSwimming.Value || !ValConfig.EnableSwimStaminaCostReduction.Value || Player.m_localPlayer == null) { return swimCost; }
                float skillFactor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Swim);
                if (ValConfig.SwimStaminaReductionLevel.Value > skillFactor * 100) { return swimCost; }
                // Stops at 100% even past level 100, so swimming never pays stamina back.
                float modified_swim_cost_factor = (1f - Mathf.Clamp01(ValConfig.SwimStaminaCostReductionFactor.Value * SkillCaps.BonusFactor(skillFactor, ValConfig.SwimmingBonusesPastLevel100))) * swimCost;
                return modified_swim_cost_factor;
            }
        }
    }
}
