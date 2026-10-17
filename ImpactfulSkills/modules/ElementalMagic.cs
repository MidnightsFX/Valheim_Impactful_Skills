using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ImpactfulSkills.patches
{
    [HarmonyPatch]
    public static class ElementalMagic
    {
        private static float cached_projectileAccuracy;
        private static float cached_projectileAccuracyMin;
        private static ItemDrop.ItemData WeaponRef;

        /// <summary>
        ///     Checks if elemental magic config settings are enabled does an early return if they are not.
        ///     Checks if the attack is made by the local player using an elementa magic weapon, and if it 
        ///     is it modifies the spread of the projectiles it launches based on the local players 
        ///     elemental magic skill. 
        ///     
        ///     The intention behind this is to bring parity between Elemental Magic and Bow projectiles.
        ///     As your bow skill increases, the time to draw the bow reduces and the game reduces the spread 
        ///     of the launched arrows based on how drawn the bow is. As a result, higher bow skill means that
        ///     you can aim more accurately faster. However, the elemental magic ice staff never becomes more
        ///     accurate in the vanilla game no matter how high your elemental magic skill is and it is the only
        ///     magic weapon that has a random scatter to it's projectiles.
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPostfix]
        [HarmonyBefore("blacks7ar.BowPlugin", "Searica.Valheim.ProjectileTweaks")]
        [HarmonyPriority(Priority.VeryHigh)]
        [HarmonyPatch(typeof(Attack), nameof(Attack.Start))]
        internal static void AttackStartPrefix(Attack __instance)
        {
            if (!ValConfig.EnableElementalMagic.Value || !ValConfig.EnableElementalMagicSpreadReduction.Value)
            {
                return;
            }

            if (!TryGetPlayerWeapon(__instance, out WeaponRef))
            {
                return;
            }

            if (WeaponRef == null || WeaponRef.m_shared.m_skillType != Skills.SkillType.ElementalMagic)
            {
                return;
            }
  
            SetSpread(__instance, ValConfig.ElementalMagicProjectileSpreadReduction);
        }

        /// <summary>
        ///     Postfix patch runs on all attacks but we only care about getting the weapon from the local player.
        /// </summary>
        /// <param name="attack"></param>
        /// <returns></returns>
        private static bool TryGetPlayerWeapon(Attack attack, out ItemDrop.ItemData weapon)
        {
            if (attack == null || !attack.m_character || !attack.m_character.IsPlayer() || attack.m_character != Player.m_localPlayer)
            {
                weapon = null;
                return false;
            }

            weapon = attack.m_character.GetCurrentWeapon();
            return true;
        }


        /// <summary>
        ///     Projectile accuracy is a misleading name in the game code. It dictates how much the projectile can 
        ///     deviate from the intended path so it is equivalent to amount of spread in the launched projectiles.
        ///     Cache the default values of the attack and set up a delegate method to trigger updates to the attack
        ///     if the config setting is changed while the attack still exists. Then do a manual update of the accuracy 
        ///     values for this attack.
        /// </summary>
        /// <param name="attack"></param>
        /// <param name="spreadConfig"></param>
        private static void SetSpread(Attack attack, ConfigEntry<float> spreadConfig)
        {
            cached_projectileAccuracy = attack.m_projectileAccuracy;
            cached_projectileAccuracyMin = attack.m_projectileAccuracyMin;

            spreadConfig.SettingChanged += (sender, e) =>
            {
                float multiplier = ComputeSpreadMultiplier(spreadConfig);
                attack.m_projectileAccuracy = cached_projectileAccuracy * multiplier;
                attack.m_projectileAccuracyMin = cached_projectileAccuracyMin * multiplier;
            };

            float multiplier = ComputeSpreadMultiplier(spreadConfig);
            attack.m_projectileAccuracy *= multiplier;
            attack.m_projectileAccuracyMin *= multiplier;
        }


        /// <summary>
        ///     Lower spread multiplier is better so scale the multiplier down as skill level increases. Uses SkillCaps method
        ///     to ensure the configuration settings for bonuses past level 100 are considered and allow to pushing the spread multiplier
        ///     even closer to 0 for skill levels above 100 if it is not already 0 at level 100. Output is clamped to range 0-1 to prevent 
        ///     negative spread from occuring or the spread of the attack ever becoming worse than vanilla.
        /// </summary>
        /// <param name="spreadConfig"></param>
        /// <returns></returns>
        private static float ComputeSpreadMultiplier(ConfigEntry<float> spreadConfig)
        {
            return Mathf.Clamp01(1.0f - (spreadConfig.Value * SkillCaps.GetBonusSkillFactor(Player.m_localPlayer, Skills.SkillType.ElementalMagic, ValConfig.WeaponSkillBonusesPastLevel100)));
        }

    }
}
