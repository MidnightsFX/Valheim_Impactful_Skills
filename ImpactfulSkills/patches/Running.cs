using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace ImpactfulSkills.patches
{
    public static class Running
    {
        [HarmonyPatch(typeof(Character))]
        public static class RunningSpeedPatch
        {
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(Character.UpdateWalking))]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
            {
                var codeMatcher = new CodeMatcher(instructions);
                codeMatcher.MatchStartForward(
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Character), nameof(Character.m_runSpeed)))
                    ).RemoveInstruction().InsertAndAdvance(
                    Transpilers.EmitDelegate(ModifyRunSpeedBySkill)
                    ).ThrowIfNotMatch("Unable to patch Run skill movement increase.");

                return codeMatcher.Instructions();
            }

            public static float ModifyRunSpeedBySkill(Character __instance)
            {
                if (ValConfig.EnableRun.Value == true && Player.m_localPlayer != null && __instance == Player.m_localPlayer) {
                    float player_skill_factor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Run);
                    float run_speed_bonus = ValConfig.RunSpeedFactor.Value * (player_skill_factor * 100f);
                    float modified_run_speed = __instance.m_runSpeed + run_speed_bonus;
                    return modified_run_speed;
                }
                return __instance.m_runSpeed;
            }
        }
    }
}
