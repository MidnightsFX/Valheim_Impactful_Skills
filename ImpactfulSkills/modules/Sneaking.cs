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
            // Run before mods that replace the crouch speed field load outright, so that our
            // non-destructive insert happens while the anchor still exists and theirs still finds it.
            [HarmonyPriority(Priority.First)]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
                CodeMatcher codeMatcher = new CodeMatcher(instructions, null);

                // Add our bonus onto the crouch speed rather than replacing the field load, so that
                // mods which anchor on the same 'ldfld m_crouchSpeed' (SNEAKer, TalentTree's Silent
                // Stride) still find it no matter which of us transpiles first.
                if (codeMatcher.TryMatchStartForward("Unable to patch Sneak skill movement increase.",
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Character), "m_crouchSpeed"))
                )) {
                    codeMatcher.Advance(1)
                    .InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldarg_0),
                        Transpilers.EmitDelegate(Sneaking.SneakSpeedPatch.SneakSpeedBonus),
                        new CodeInstruction(OpCodes.Add)
                    );
                    return codeMatcher.Instructions();
                }

                // The field load is gone, so another mod replaced it with its own speed calculation.
                // Fall back to appending our bonus after that calculation instead.
                Logger.LogDebug("Crouch speed already patched by another mod, using compatibility anchor.");
                codeMatcher.Start();
                if (codeMatcher.TryMatchStartForward("Unable to patch Sneak skill movement increase for mod compatibility.",
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(Character), "IsEncumbered")),
                    new CodeMatch(OpCodes.Brfalse),
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Call)
                )) {
                    codeMatcher.Advance(4)
                    .InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldarg_0),
                        Transpilers.EmitDelegate(Sneaking.SneakSpeedPatch.SneakSpeedBonus),
                        new CodeInstruction(OpCodes.Add)
                    );
                }

                return codeMatcher.Instructions();
            }

            public static float SneakSpeedBonus(Character __instance) {
                if (!ValConfig.EnableStealth.Value || __instance.IsEncumbered()) { return 0f; }
                float skillFactor = __instance.GetSkillFactor(Skills.SkillType.Sneak);
                float num = ValConfig.SneakSpeedFactor.Value * (skillFactor * 100f);
                return num;
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
                    if (codeMatcher.TryMatchStartForward("Unable to patch Melee Backstab.",
                            new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(ItemDrop.ItemData.SharedData), "m_backstabBonus"))
                        )) {
                        codeMatcher.Advance(1)
                        .InsertAndAdvance(
                            new CodeInstruction(OpCodes.Ldarg_0),
                            Transpilers.EmitDelegate(Sneaking.SneakingBackstabBonusDmg.ModifyBackstab)
                        );
                    }
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
                    if (codeMatcher.TryMatchStartForward("Unable to patch Ranged Backstab.",
                        new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(ItemDrop.ItemData.SharedData), "m_backstabBonus"))
                    )) {
                        codeMatcher.Advance(1)
                        .InsertAndAdvance(
                            new CodeInstruction(OpCodes.Ldarg_0),
                            Transpilers.EmitDelegate(Sneaking.SneakingBackstabBonusDmg.ModifyBackstab)
                        );
                    }
                    return codeMatcher.Instructions();
                }
            }
        }
    }
}
