using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace ImpactfulSkills.patches
{
    public static class Sneaking
    {

        [HarmonyPatch(typeof(Character))]
        public static class SneakSpeedPatch
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
                if (ValConfig.EnableStealth.Value == true && Player.m_localPlayer != null && __instance == Player.m_localPlayer)
                {
                    float player_skill_factor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Sneak);
                    // Sneaking
                    float sneak_speed_bonus = ValConfig.SneakSpeedFactor.Value * (player_skill_factor * 100f);
                    // Logger.LogDebug($"Setting sneak speed bonus {sneak_speed_bonus}");
                    float modified_sneak_speed = __instance.m_crouchSpeed + sneak_speed_bonus;
                    return modified_sneak_speed;
                }
                return __instance.m_crouchSpeed;
            }
        }

        public static class SneakingReducedNoisePatch
        {
            [HarmonyPatch(typeof(Character), nameof(Character.AddNoise))]
            public static class AddNoisePatch
            {
                public static void Prefix(Character __instance, ref float range)
                {
                    if (ValConfig.EnableStealth.Value == true && Player.m_localPlayer != null && __instance == Player.m_localPlayer)
                    {
                        float sneak_skill_level = Player.m_localPlayer.GetSkillLevel(Skills.SkillType.Sneak);
                        if (sneak_skill_level >= ValConfig.SneakNoiseReductionLevel.Value) {
                            // Sneaking
                            float noise_reduction_percent = ValConfig.SneakNoiseReductionFactor.Value * sneak_skill_level;
                            float reduced_noise = (100 - noise_reduction_percent) / 100 * range;
                            // Logger.LogDebug($"Setting reduced noise {reduced_noise} from {range}");
                            range = reduced_noise;
                        }

                    }
                }
            }
        }

    }
}
