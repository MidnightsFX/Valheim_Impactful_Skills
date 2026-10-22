using HarmonyLib;
using System.Collections.Generic;
using System;
using HaulingModule = ImpactfulSkills.patches.Hauling;

namespace ImpactfulSkills.modules
{
    [HarmonyPatch]
    internal static class StatGains
    {
        [Flags]
        public enum BonusStaminaSkills
        {
            Swim = 1 << 0,
            Fishing = 1 << 1,
            Sneak = 1 << 2,
            Jump = 1 << 3,
            Dodge = 1 << 4,
            Ride = 1 << 5,
            Woodcutting = 1 << 6,
            Mining = 1 << 7,
            Run = 1 << 8,
            Hauling = 1 << 9,
            All = ~(-1 << 10),
        }

        [Flags]
        public enum BonusEitrSkills
        {
            BloodMagic = 1 << 0,
            ElementalMagic = 1 << 1,
            All = ~(-1 << 2),
        }

        private static readonly Dictionary<BonusStaminaSkills, Skills.SkillType> StaminaSkillMap = new()
        {
            {BonusStaminaSkills.Swim, Skills.SkillType.Swim},
            {BonusStaminaSkills.Fishing, Skills.SkillType.Fishing},
            {BonusStaminaSkills.Sneak, Skills.SkillType.Sneak},
            {BonusStaminaSkills.Jump, Skills.SkillType.Jump},
            {BonusStaminaSkills.Dodge, Skills.SkillType.Dodge},
            {BonusStaminaSkills.Ride, Skills.SkillType.Ride},
            {BonusStaminaSkills.Woodcutting, Skills.SkillType.WoodCutting},
            {BonusStaminaSkills.Mining, Skills.SkillType.Pickaxes},
            {BonusStaminaSkills.Run, Skills.SkillType.Run},
            {BonusStaminaSkills.Hauling, HaulingModule.HaulingSkill},
        };

        private static readonly Dictionary<BonusEitrSkills, Skills.SkillType> EitrSkillMap = new()
        {
            {BonusEitrSkills.BloodMagic, Skills.SkillType.BloodMagic},
            {BonusEitrSkills.ElementalMagic, Skills.SkillType.ElementalMagic},
        };


        private static float BaseStamina = float.NaN;
        //private static float BaseEitr = 0f;  // fixed at zero by base game and has no field to check as a result

        
        /// <summary>
        ///     Cache base stamina value the first time a local player is created, just in
        ///     case they ever change it. 
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.Awake))]
        private static void CacheBaseStats(Player __instance)
        {
            if (!__instance || !Player.m_localPlayerExists || __instance != Player.m_localPlayer)
            {
                return;
            }


            if (float.IsNaN(BaseStamina))
            {
                BaseStamina = __instance.m_baseStamina;
            }
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(Player), nameof(Player.GetTotalFoodValue))]
        private static void UpdateMaxStats(Player __instance, ref float stamina, ref float eitr) 
        {
            if (!__instance || !Player.m_localPlayerExists || __instance != Player.m_localPlayer)
            {
                return;
            }

            if (ValConfig.EnableExtraStamina.Value)
            {
                stamina += GetExtraBaseStamina(__instance);
            }

            if (ValConfig.EnableExtraEitr.Value)
            {
                eitr += GetExtraBaseEitr(__instance);
            }
        }

        private static float GetExtraBaseStamina(Player __instance) 
        {
            float bonusStamina = 0f;
            foreach(KeyValuePair<BonusStaminaSkills, Skills.SkillType> kvp in StaminaSkillMap)
            {
                if (!ValConfig.EnableExtraStaminaSkills.Value.HasFlag(kvp.Key))
                {
                    continue;
                }
                bonusStamina += ValConfig.ExtraStaminaPerSkill.Value * SkillCaps.GetBonusSkillFactor(__instance, kvp.Value);
            }
            return bonusStamina;
        }

        private static float GetExtraBaseEitr(Player __instance)
        {
            float bonusEitr = 0f;
            foreach (KeyValuePair<BonusEitrSkills, Skills.SkillType> kvp in EitrSkillMap)
            {
                if (!ValConfig.EnableExtraEitrSkills.Value.HasFlag(kvp.Key))
                {
                    continue;
                }
                bonusEitr += ValConfig.ExtraEitrPerSkill.Value * SkillCaps.GetBonusSkillFactor(__instance, kvp.Value);
            }
            return bonusEitr;
        }
    }
}
