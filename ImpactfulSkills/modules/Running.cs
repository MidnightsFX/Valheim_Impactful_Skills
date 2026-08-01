using HarmonyLib;
using ImpactfulSkills.common;
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
            // See SneakSpeedPatch - same reasoning, m_runSpeed is just as attractive an anchor.
            [HarmonyPriority(Priority.First)]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
            {
                var codeMatcher = new CodeMatcher(instructions);
                // Add onto the run speed instead of replacing the field load, so that other mods
                // anchoring on the same 'ldfld m_runSpeed' can still find it regardless of order.
                if (codeMatcher.TryMatchStartForward("Unable to patch Run skill movement increase.",
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Character), nameof(Character.m_runSpeed)))
                )) {
                    codeMatcher.Advance(1).InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldarg_0),
                        Transpilers.EmitDelegate(RunSpeedBonus),
                        new CodeInstruction(OpCodes.Add)
                    );
                }

                return codeMatcher.Instructions();
            }

            public static float RunSpeedBonus(Character __instance)
            {
                if (ValConfig.EnableRun.Value == true && Player.m_localPlayer != null && __instance == Player.m_localPlayer) {
                    float player_skill_factor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Run);
                    return ValConfig.RunSpeedFactor.Value * (player_skill_factor * 100f);
                }
                return 0f;
            }
        }
    }
}
