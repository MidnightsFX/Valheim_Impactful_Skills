using HarmonyLib;
using UnityEngine;

namespace ImpactfulSkills.patches
{
    [HarmonyPatch]
    public static class ElementalMagic
    {
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
        ///
        ///     The reduction only applies from ElementalMagicSpreadReductionLevel on. Like other level requirements it
        ///     reads the boosted level (see SkillCaps), and once reached the full reduction for your level applies.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="__result">False when the attack did not start, and is thrown away.</param>
        [HarmonyPostfix]
        [HarmonyBefore("blacks7ar.BowPlugin", "Searica.Valheim.ProjectileTweaks")]
        [HarmonyPriority(Priority.VeryHigh)]
        [HarmonyPatch(typeof(Attack), nameof(Attack.Start))]
        internal static void AttackStartPostfix(Attack __instance, bool __result)
        {
            if (!__result || !ValConfig.EnableElementalMagic.Value || !ValConfig.EnableElementalMagicSpreadReduction.Value)
            {
                return;
            }

            if (!IsLocalPlayerElementalAttack(__instance))
            {
                return;
            }

            if (Player.m_localPlayer.GetSkillLevel(Skills.SkillType.ElementalMagic) < ValConfig.ElementalMagicSpreadReductionLevel.Value)
            {
                return;
            }

            SetSpread(__instance, ComputeSpreadMultiplier());
        }

        /// <summary>
        ///     Postfix patch runs on all attacks but we only care about the local player's elemental magic weapons.
        ///     Start has already set m_weapon to the item this attack belongs to.
        /// </summary>
        /// <param name="attack"></param>
        /// <returns></returns>
        private static bool IsLocalPlayerElementalAttack(Attack attack)
        {
            if (attack == null || !attack.m_character || attack.m_character != Player.m_localPlayer)
            {
                return false;
            }

            return attack.m_weapon != null && attack.m_weapon.m_shared.m_skillType == Skills.SkillType.ElementalMagic;
        }


        /// <summary>
        ///     Projectile accuracy is a misleading name in the game code. It dictates how much the projectile can
        ///     deviate from the intended path so it is equivalent to amount of spread in the launched projectiles.
        ///
        ///     Humanoid.StartAttack clones the weapon's attack for every swing, so scaling this copy in place never
        ///     compounds, and a changed config setting is picked up by the next swing without any extra handling.
        /// </summary>
        /// <param name="attack"></param>
        /// <param name="multiplier"></param>
        private static void SetSpread(Attack attack, float multiplier)
        {
            attack.m_projectileAccuracy *= multiplier;
            attack.m_projectileAccuracyMin *= multiplier;
        }


        /// <summary>
        ///     Lower spread multiplier is better so scale the multiplier down as skill level increases. Uses SkillCaps method
        ///     to ensure the configuration settings for bonuses past level 100 are considered and allow to pushing the spread multiplier
        ///     even closer to 0 for skill levels above 100 if it is not already 0 at level 100. Output is clamped to range 0-1 to prevent
        ///     negative spread from occuring or the spread of the attack ever becoming worse than vanilla.
        /// </summary>
        /// <returns></returns>
        private static float ComputeSpreadMultiplier()
        {
            return Mathf.Clamp01(1.0f - (ValConfig.ElementalMagicProjectileSpreadReduction.Value * SkillCaps.GetBonusSkillFactor(Player.m_localPlayer, Skills.SkillType.ElementalMagic, ValConfig.WeaponSkillBonusesPastLevel100)));
        }

    }
}
