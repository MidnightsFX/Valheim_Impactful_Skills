using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

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
                codeMatcher.MatchStartForward(
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof (Character), "m_swimSpeed"))
                ).RemoveInstruction()
                .InsertAndAdvance(
                    Transpilers.EmitDelegate(ModifySwimSpeedbySkill)
                ).ThrowIfNotMatch("Unable to patch Swim skill movement increase.");
                return codeMatcher.Instructions();
            }

            public static float ModifySwimSpeedbySkill(Character __instance) {
                if (!ValConfig.EnableSwimming.Value) { return __instance.m_swimSpeed; }
                float skillFactor = __instance.GetSkillFactor(Skills.SkillType.Swim);
                if (ValConfig.SwimSpeedRequiredLevel.Value > skillFactor*100) { return __instance.m_swimSpeed; }
                float swim_increase_speed = 1 + (ValConfig.SwimmingSpeedFactor.Value * skillFactor);
                return __instance.m_swimSpeed + swim_increase_speed;
            }
        }

        [HarmonyPatch(typeof(Player))]
        public static class SwimmingReduceStaminaCostPatch
        {
            [HarmonyEmitIL("./dumps")]
            [HarmonyTranspiler]
            [HarmonyPatch("OnSwimming")]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                CodeMatcher codeMatcher = new CodeMatcher(instructions, (ILGenerator)null);
                codeMatcher.MatchStartForward(
                    new CodeMatch(OpCodes.Mul),
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(Character), nameof(Character.UseStamina)))
                )
                .Advance(1)
                .InsertAndAdvance(
                    // new CodeInstruction(OpCodes.Ldarg_0),
                    Transpilers.EmitDelegate(ModifySwimCost)
                )
                .ThrowIfNotMatch("Unable to patch Swim Stamina cost reduction.");
                return codeMatcher.Instructions();
            }

            public static float ModifySwimCost(float swimCost) {
                if (!ValConfig.EnableSwimming.Value || !ValConfig.EnableSwimStaminaCostReduction.Value || Player.m_localPlayer == null) { return swimCost; }
                float skillFactor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Swim);
                if (ValConfig.SwimStaminaReductionLevel.Value > skillFactor * 100) { return swimCost; }
                float modified_swim_cost_factor = (1f - (ValConfig.SwimStaminaCostReductionFactor.Value * skillFactor)) * swimCost;
                return modified_swim_cost_factor;
            }
        }
    }
}
