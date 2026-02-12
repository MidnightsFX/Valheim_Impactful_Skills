using HarmonyLib;
using ImpactfulSkills.common;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace ImpactfulSkills.patches
{
    public static class Sneaking
    {
        [HarmonyPatch(typeof(Character))]
        public static class SneakSpeedPatch
        {
            //[HarmonyEmitIL("./dumps")]
            //[HarmonyDebug]
            [HarmonyTranspiler]
            [HarmonyPatch("UpdateWalking")]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
                CodeMatcher codeMatcher = new CodeMatcher(instructions, null);
                if (Compatibility.IsSNEAKerEnabled) {
                    Logger.LogDebug("SNEAKer detected, using compatibility patch.");
                    codeMatcher.MatchStartForward(
                        new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(Character), "IsEncumbered")),
                        new CodeMatch(OpCodes.Brfalse),
                        new CodeMatch(OpCodes.Ldarg_0),
                        new CodeMatch(OpCodes.Call)
                    ).Advance(4)
                    .InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldarg_0),
                        Transpilers.EmitDelegate(Sneaking.SneakSpeedPatch.ModifySneakSpeedBonusOnly),
                        new CodeInstruction(OpCodes.Add)
                    ).ThrowIfNotMatch("Unable to patch Sneak skill movement increase with SNEAKer.");
                } else {
                    codeMatcher.MatchStartForward(
                        new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Character), "m_crouchSpeed"))
                    ).RemoveInstruction()
                    .InsertAndAdvance(
                        Transpilers.EmitDelegate(Sneaking.SneakSpeedPatch.ModifyMovementSpeedBySkill)
                    ).ThrowIfNotMatch("Unable to patch Sneak skill movement increase.");
                }
                return codeMatcher.Instructions();
            }

            public static float ModifySneakSpeedBonusOnly(Character __instance) {
                if (!ValConfig.EnableStealth.Value || __instance.IsEncumbered()) { return 0f; }
                float skillFactor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Sneak);
                float num = ValConfig.SneakSpeedFactor.Value * (skillFactor * 100f);
                return num;
            }

            public static float ModifyMovementSpeedBySkill(Character __instance) {
                if (!ValConfig.EnableStealth.Value || __instance.IsEncumbered()) { return __instance.m_crouchSpeed; }
                float skillFactor = __instance.GetSkillFactor(Skills.SkillType.Sneak);
                float num = ValConfig.SneakSpeedFactor.Value * (skillFactor * 100f);
                return __instance.m_crouchSpeed + num;
            }
        }

        public static class SneakingReducedNoisePatch {
            [HarmonyPatch(typeof(Character), "AddNoise")]
            public static class AddNoisePatch {
                public static void Prefix(Character __instance, ref float range) {
                    if (!ValConfig.EnableStealth.Value || !(Player.m_localPlayer != null) || !(__instance == Player.m_localPlayer)) { return; }
                    float skillLevel = Player.m_localPlayer.GetSkillLevel(Skills.SkillType.Sneak);
                    if ((double)skillLevel < (double)ValConfig.SneakNoiseReductionLevel.Value) { return; }
                    float num = (float)((100.0 - (double)(ValConfig.SneakNoiseReductionFactor.Value * skillLevel)) / 100.0) * range;
                    range = num;
                }
            }
        }

        public static class SneakingBackstabBonusDmg {
            public static float ModifyBackstab(float backstab_base, Attack attack_instance) {
                if (!ValConfig.EnableSneakBonusDamage.Value || !attack_instance.m_character.IsPlayer() || ValConfig.SneakBackstabBonusLevel.Value > (double)attack_instance.m_character.GetSkillLevel(Skills.SkillType.Sneak)) { return backstab_base; }
                float skillFactor = attack_instance.m_character.GetSkillFactor(Skills.SkillType.Sneak);
                float num1 = backstab_base * (ValConfig.SneakBackstabBonusFactor.Value * skillFactor);
                float num2 = backstab_base + num1;
                Logger.LogDebug(string.Format($"Adding bonus backstab {num1} = total ({num2})"));
                return num2;
            }

            [HarmonyPatch(typeof(Attack))]
            public static class AddMeleeBonusBackstab {
                [HarmonyTranspiler]
                [HarmonyPatch("DoMeleeAttack")]
                public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
                    CodeMatcher codeMatcher = new CodeMatcher(instructions, null);
                    codeMatcher.MatchStartForward(
                            new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(ItemDrop.ItemData.SharedData), "m_backstabBonus"))
                        ).Advance(1)
                        .InsertAndAdvance(
                            new CodeInstruction(OpCodes.Ldarg_0),
                            Transpilers.EmitDelegate(Sneaking.SneakingBackstabBonusDmg.ModifyBackstab)
                        ).ThrowIfNotMatch("Unable to patch Melee Backstab.");
                    return codeMatcher.Instructions();
                }
            }

            [HarmonyPatch(typeof(Attack))]
            public static class AddRangedBonusBackstab
            {
                [HarmonyTranspiler]
                [HarmonyPatch("FireProjectileBurst")]
                public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
                    CodeMatcher codeMatcher = new CodeMatcher(instructions, null);
                    codeMatcher.MatchStartForward(
                        new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(ItemDrop.ItemData.SharedData), "m_backstabBonus"))
                    ).Advance(1)
                    .InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldarg_0),
                        Transpilers.EmitDelegate(Sneaking.SneakingBackstabBonusDmg.ModifyBackstab)
                    ).ThrowIfNotMatch("Unable to patch Ranged Backstab.");
                    return codeMatcher.Instructions();
                }
            }
        }
    }
}
