using HarmonyLib;
using ImpactfulSkills.common;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace ImpactfulSkills.patches
{
    public static class Jumping
    {
        // Patch jump force for increased jump height
        [HarmonyPatch(typeof(Character))]
        public static class JumpForcePatch
        {
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(Character.Jump))]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codeMatcher = new CodeMatcher(instructions);

                // Find where m_jumpForce is loaded and modify it
                codeMatcher.MatchStartForward(
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Character), nameof(Character.m_jumpForce)))
                    ).RemoveInstruction().InsertAndAdvance(
                        Transpilers.EmitDelegate(ModifyJumpForceBySkill)
                    ).ThrowIfNotMatch("Unable to patch jump force modification.");

                return codeMatcher.Instructions();
            }

            private static float ModifyJumpForceBySkill(Character character) {
                if (!ValConfig.EnableJump.Value || Player.m_localPlayer == null || character != Player.m_localPlayer) {
                    return character.m_jumpForce;
                }
                float skillFactor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Jump);
                float skillLevel = skillFactor * 100f;

                if (ValConfig.JumpHeightRequiredLevel.Value > skillLevel) {
                    return character.m_jumpForce;
                }

                // JumpHeightFactor is a percentage where 100 = original height
                // 50% = 0.5x height at level 100, 200% = 2x height at level 100
                float jumpHeightMultiplier = ValConfig.JumpHeightFactor.Value / 100f;
                // Scale the multiplier based on skill level (0 at level 0, full at level 100)
                float scaledMultiplier = 1f + ((jumpHeightMultiplier - 1f) * skillFactor);
                float modifiedJumpForce = character.m_jumpForce * scaledMultiplier;

                Logger.LogDebug($"Jump force modified: {character.m_jumpForce} -> {modifiedJumpForce} (skill: {skillLevel}, multiplier: {scaledMultiplier})");

                return modifiedJumpForce;
            }
        }

        // Patch fall damage reduction in OnLand method
        [HarmonyPatch(typeof(Character))]
        public static class FallDamagePatch
        {
            internal static float MinFallDamageHeight = 4f;

            //[HarmonyEmitIL("./dumps")]
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(Character.UpdateGroundContact))]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codeMatcher = new CodeMatcher(instructions);
                codeMatcher
                .MatchStartForward(
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(Character), nameof(Character.IsPlayer)))
                ).Advance(2).MatchStartForward(
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(Character), nameof(Character.IsPlayer)))
                ).MatchForward(true, new CodeMatch(OpCodes.Ldc_R4))
                .RemoveInstruction()
                .InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    Transpilers.EmitDelegate(ModifyMinFallHeightForDamageBySkill)
                ).ThrowIfNotMatch("Unable to patch min damage height increase.")
                .MatchStartForward(
                    //new CodeMatch(OpCodes.Ldloc_2),
                    //new CodeMatch(OpCodes.Ldloca_S),
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(SEMan), nameof(SEMan.ModifyFallDamage)))
                ).Advance(1).InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldloc_2), // Load the fall damage to be taken
                    new CodeInstruction(OpCodes.Ldarg_0),
                    Transpilers.EmitDelegate(ModifyFallDamageBySkill),
                    new CodeInstruction(OpCodes.Stloc_2)
                ).ThrowIfNotMatch("Unable to patch Fall damage reduction.");

                return codeMatcher.Instructions();
            }

            private static float ModifyFallDamageBySkill(float falldmg, Character chara) {
                if (ValConfig.EnableJump.Value == false || ValConfig.EnableFallDamageReduction.Value == false) {
                    return falldmg;
                }

                float skillFactor = chara.GetSkillFactor(Skills.SkillType.Jump);
                float skillLevel = skillFactor * 100f;

                if (ValConfig.FallDamageReductionRequiredLevel.Value > skillLevel) {
                    return falldmg;
                }

                // Calculate fall damage reduction
                float damageReductionFactor = 1f - (ValConfig.FallDamageReductionFactor.Value * skillFactor);
                float reducedDamage = falldmg * damageReductionFactor;
                Logger.LogDebug($"Fall damage reduced: {falldmg} -> {reducedDamage} (reduction: {damageReductionFactor})");

                return reducedDamage;
            }

            private static float ModifyMinFallHeightForDamageBySkill(Character character) {
                // This character is ALWAYS the player, as the first part of this modified block checks if the character is a player
                if (ValConfig.EnableJump.Value == false || ValConfig.EnableFallDamageHeightBonus.Value == false) {
                    return MinFallDamageHeight;
                }

                float skillFactor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Jump);
                float skillLevel = skillFactor * 100f;

                if (ValConfig.FallDamageHeightRequiredLevel.Value > skillLevel) {
                    return MinFallDamageHeight;
                }

                // Increase max fall height based on skill
                float modifiedMaxFallHeight = MinFallDamageHeight + (ValConfig.FallDamageHeightBonus.Value * skillFactor);
                //Logger.LogDebug($"Modified jump height that you can fall without recieving damage {modifiedMaxFallHeight}");
                return modifiedMaxFallHeight;
            }
        }
    }
}