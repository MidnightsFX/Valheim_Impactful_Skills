using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace ImpactfulSkills.patches
{
    public static class Sneaking
    {

        [HarmonyPatch(typeof(Character))]
        public static class DamageHandler_Apply_Patch
        {
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(Character.UpdateWalking))]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
            {
                var codeMatcher = new CodeMatcher(instructions);
                codeMatcher.MatchStartForward(
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Character), nameof(Character.m_crouchSpeed)))
                    ).RemoveInstruction().InsertAndAdvance(
                    Transpilers.EmitDelegate(ModifyMovementSpeedBySkill)
                    ).ThrowIfNotMatch("Unable to patch Sneak skill movement increase.");

                return codeMatcher.Instructions();
            }

            public static float ModifyMovementSpeedBySkill(Character __instance)
            {
                if (Player.m_localPlayer != null && __instance == Player.m_localPlayer)
                {
                    float player_skill_factor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Sneak);
                    // Sneaking
                    float sneak_speed_bonus = ValConfig.SneakSpeedFactor.Value * (player_skill_factor * 100f);
                    Logger.LogDebug($"Setting sneak speed bonus {sneak_speed_bonus}");
                    float modified_sneak_speed = __instance.m_crouchSpeed + sneak_speed_bonus;
                    return modified_sneak_speed;
                }
                return __instance.m_crouchSpeed;
            }
        }

    }
}
