using HarmonyLib;
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
            [HarmonyTranspiler]
            [HarmonyPatch("UpdateWalking")]
            public static IEnumerable<CodeInstruction> Transpiler(
              IEnumerable<CodeInstruction> instructions)
            {
                CodeMatcher codeMatcher = new CodeMatcher(instructions, (ILGenerator)null);
                codeMatcher.MatchStartForward(new CodeMatch[1]
                {
          new CodeMatch(new OpCode?(OpCodes.Ldfld), (object) AccessTools.Field(typeof (Character), "m_crouchSpeed"), (string) null)
                }).RemoveInstruction().InsertAndAdvance(new CodeInstruction[1]
                {
          Transpilers.EmitDelegate<Func<Character, float>>(new Func<Character, float>(Sneaking.SneakSpeedPatch.ModifyMovementSpeedBySkill))
                }).ThrowIfNotMatch("Unable to patch Sneak skill movement increase.", Array.Empty<CodeMatch>());
                return (IEnumerable<CodeInstruction>)codeMatcher.Instructions();
            }

            public static float ModifyMovementSpeedBySkill(Character __instance)
            {
                if (!ValConfig.EnableStealth.Value || __instance.IsEncumbered())
                    return __instance.m_crouchSpeed;
                float skillFactor = __instance.GetSkillFactor((Skills.SkillType)101);
                float num = ValConfig.SneakSpeedFactor.Value * (skillFactor * 100f);
                return __instance.m_crouchSpeed + num;
            }
        }

        public static class SneakingReducedNoisePatch
        {
            [HarmonyPatch(typeof(Character), "AddNoise")]
            public static class AddNoisePatch
            {
                public static void Prefix(Character __instance, ref float range)
                {
                    if (!ValConfig.EnableStealth.Value || !((UnityEngine.Object)Player.m_localPlayer != (UnityEngine.Object)null) || !((UnityEngine.Object)__instance == (UnityEngine.Object)Player.m_localPlayer))
                        return;
                    float skillLevel = ((Character)Player.m_localPlayer).GetSkillLevel((Skills.SkillType)101);
                    if ((double)skillLevel < (double)ValConfig.SneakNoiseReductionLevel.Value)
                        return;
                    float num = (float)((100.0 - (double)(ValConfig.SneakNoiseReductionFactor.Value * skillLevel)) / 100.0) * range;
                    range = num;
                }
            }
        }

        public static class SneakingBackstabBonusDmg
        {
            public static float ModifyBackstab(float backstab_base, Attack attack_instance)
            {
                if (!ValConfig.EnableSneakBonusDamage.Value || !((Character)attack_instance.m_character).IsPlayer() || (double)ValConfig.SneakBackstabBonusLevel.Value > (double)((Character)attack_instance.m_character).GetSkillLevel((Skills.SkillType)101))
                    return backstab_base;
                float skillFactor = ((Character)attack_instance.m_character).GetSkillFactor((Skills.SkillType)101);
                float num1 = backstab_base * (ValConfig.SneakBackstabBonusFactor.Value * skillFactor);
                float num2 = backstab_base + num1;
                Logger.LogDebug(string.Format("Adding bonus backstab {0} = total ({1})", (object)num1, (object)num2));
                return num2;
            }

            [HarmonyPatch(typeof(Attack))]
            public static class AddMeleeBonusBackstab
            {
                [HarmonyTranspiler]
                [HarmonyPatch("DoMeleeAttack")]
                public static IEnumerable<CodeInstruction> Transpiler(
                  IEnumerable<CodeInstruction> instructions)
                {
                    CodeMatcher codeMatcher = new CodeMatcher(instructions, (ILGenerator)null);
                    codeMatcher.MatchStartForward(new CodeMatch[1]
                    {
            new CodeMatch(new OpCode?(OpCodes.Ldfld), (object) AccessTools.Field(typeof (ItemDrop.ItemData.SharedData), "m_backstabBonus"), (string) null)
                    }).Advance(1).InsertAndAdvance(new CodeInstruction[2]
                    {
            new CodeInstruction(OpCodes.Ldarg_0, (object) 0.0f),
            Transpilers.EmitDelegate<Func<float, Attack, float>>(new Func<float, Attack, float>(Sneaking.SneakingBackstabBonusDmg.ModifyBackstab))
                    }).ThrowIfNotMatch("Unable to patch Melee Backstab.", Array.Empty<CodeMatch>());
                    return (IEnumerable<CodeInstruction>)codeMatcher.Instructions();
                }
            }

            [HarmonyPatch(typeof(Attack))]
            public static class AddRangedBonusBackstab
            {
                [HarmonyTranspiler]
                [HarmonyPatch("FireProjectileBurst")]
                public static IEnumerable<CodeInstruction> Transpiler(
                  IEnumerable<CodeInstruction> instructions)
                {
                    CodeMatcher codeMatcher = new CodeMatcher(instructions, (ILGenerator)null);
                    codeMatcher.MatchStartForward(new CodeMatch[1]
                    {
            new CodeMatch(new OpCode?(OpCodes.Ldfld), (object) AccessTools.Field(typeof (ItemDrop.ItemData.SharedData), "m_backstabBonus"), (string) null)
                    }).Advance(1).InsertAndAdvance(new CodeInstruction[2]
                    {
            new CodeInstruction(OpCodes.Ldarg_0, (object) 0.0f),
            Transpilers.EmitDelegate<Func<float, Attack, float>>(new Func<float, Attack, float>(Sneaking.SneakingBackstabBonusDmg.ModifyBackstab))
                    }).ThrowIfNotMatch("Unable to patch Ranged Backstab.", Array.Empty<CodeMatch>());
                    return (IEnumerable<CodeInstruction>)codeMatcher.Instructions();
                }
            }
        }
    }
}
