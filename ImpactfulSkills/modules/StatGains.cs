using HarmonyLib;
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
        }

        [Flags]
        public enum BonusEitrSkills
        {
            BloodMagic = 1 << 0,
            ElementalMagic = 1 << 1,
        }

        /// <summary>
        ///     Every skill in each enum, counted from the enum itself so a skill added to it is
        ///     included without updating a bit count by hand. These are the setting defaults.
        /// </summary>
        internal static readonly BonusStaminaSkills AllStaminaSkills = AllFlags<BonusStaminaSkills>();
        internal static readonly BonusEitrSkills AllEitrSkills = AllFlags<BonusEitrSkills>();

        /// <summary>
        ///     A skill that adds to a base stat. The skill and its own on/off setting are read each time
        ///     the bonus is worked out instead of being stored here: this mod's own skills only get their
        ///     SkillType in the plugin's Awake, and ValConfig builds this table while it is still binding.
        /// </summary>
        private sealed class BonusSkill
        {
            public readonly int Flag;
            public readonly Func<Skills.SkillType> Skill;
            public readonly Func<bool> Enabled;

            public BonusSkill(Enum flag, Func<Skills.SkillType> skill, Func<bool> enabled)
            {
                Flag = Convert.ToInt32(flag);
                Skill = skill;
                Enabled = enabled;
            }
        }

        private static readonly BonusSkill[] StaminaSkills =
        {
            new(BonusStaminaSkills.Swim, () => Skills.SkillType.Swim, () => ValConfig.EnableSwimming.Value),
            // Fishing has no setting of its own that turns it off.
            new(BonusStaminaSkills.Fishing, () => Skills.SkillType.Fishing, () => true),
            new(BonusStaminaSkills.Sneak, () => Skills.SkillType.Sneak, () => ValConfig.EnableStealth.Value),
            new(BonusStaminaSkills.Jump, () => Skills.SkillType.Jump, () => ValConfig.EnableJump.Value),
            new(BonusStaminaSkills.Dodge, () => Skills.SkillType.Dodge, () => ValConfig.EnableDodge.Value),
            new(BonusStaminaSkills.Ride, () => Skills.SkillType.Ride, () => ValConfig.EnableRide.Value),
            new(BonusStaminaSkills.Woodcutting, () => Skills.SkillType.WoodCutting, () => ValConfig.EnableWoodcutting.Value),
            new(BonusStaminaSkills.Mining, () => Skills.SkillType.Pickaxes, () => ValConfig.EnableMining.Value),
            new(BonusStaminaSkills.Run, () => Skills.SkillType.Run, () => ValConfig.EnableRun.Value),
            new(BonusStaminaSkills.Hauling, () => HaulingModule.HaulingSkill, () => ValConfig.EnableHauling.Value),
        };

        private static readonly BonusSkill[] EitrSkills =
        {
            new(BonusEitrSkills.BloodMagic, () => Skills.SkillType.BloodMagic, () => ValConfig.EnableBloodMagic.Value),
            new(BonusEitrSkills.ElementalMagic, () => Skills.SkillType.ElementalMagic, () => ValConfig.EnableElementalMagic.Value),
        };


        private static float BaseStamina = float.NaN;
        //private static float BaseEitr = 0f;  // fixed at zero by base game and has no field to check as a result

        
        /// <summary>
        ///     Cache base stamina value the first time a local player is created, just in
        ///     case they ever change it. Not on Player.Awake: that runs inside Instantiate,
        ///     before Game.SpawnPlayer makes the new player the local one.
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.SetLocalPlayer))]
        private static void CacheBaseStats(Player __instance)
        {
            if (!__instance || __instance != Player.m_localPlayer)
            {
                return;
            }

            if (float.IsNaN(BaseStamina))
            {
                BaseStamina = __instance.m_baseStamina;
            }
        }

        /// <summary>
        ///     Low priority, so the bonus goes on after other mods' postfixes have adjusted the food totals.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        [HarmonyPatch(typeof(Player), nameof(Player.GetTotalFoodValue))]
        private static void UpdateMaxStats(Player __instance, ref float stamina, ref float eitr)
        {
            if (!__instance || !Player.m_localPlayerExists || __instance != Player.m_localPlayer)
            {
                return;
            }

            if (ValConfig.EnableExtraStamina.Value)
            {
                stamina += GetExtraBaseStat(__instance, StaminaSkills, (int)ValConfig.EnableExtraStaminaSkills.Value, ValConfig.ExtraStaminaPerSkill.Value);
            }

            if (ValConfig.EnableExtraEitr.Value)
            {
                eitr += GetExtraBaseStat(__instance, EitrSkills, (int)ValConfig.EnableExtraEitrSkills.Value, ValConfig.ExtraEitrPerSkill.Value);
            }
        }

        /// <summary>
        ///     Adds up the bonus from each selected skill that its own setting has not turned off.
        /// </summary>
        private static float GetExtraBaseStat(Player __instance, BonusSkill[] bonusSkills, int selectedSkills, float bonusPerSkill)
        {
            float bonus = 0f;
            foreach (BonusSkill bonusSkill in bonusSkills)
            {
                if ((selectedSkills & bonusSkill.Flag) == 0 || !bonusSkill.Enabled())
                {
                    continue;
                }
                bonus += bonusPerSkill * SkillCaps.GetBonusSkillFactor(__instance, bonusSkill.Skill());
            }
            return bonus;
        }

        private static T AllFlags<T>() where T : Enum
        {
            int all = 0;
            foreach (object flag in Enum.GetValues(typeof(T)))
            {
                all |= Convert.ToInt32(flag);
            }
            return (T)Enum.ToObject(typeof(T), all);
        }
    }
}
