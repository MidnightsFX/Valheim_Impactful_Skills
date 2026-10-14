using HarmonyLib;
using System.Collections.Generic;
using TMPro;

namespace ImpactfulSkills.patches
{
    public static class BloodMagic
    {
        private static float shield_damage = 0f;
        private static List<Character.Faction> friendly_factions = new List<Character.Faction> {
            Character.Faction.Players,
            Character.Faction.PlayerSpawned,
        };

        [HarmonyPatch(typeof(SE_Shield), nameof(SE_Shield.OnDamaged))]
        public static class VoyagerSpeedPatch
        {
            private static void Prefix(HitData hit, Character attacker, SE_Shield __instance)
            {
                if (ValConfig.EnableBloodMagic.Value != true || Player.m_localPlayer == null) { return; }
                //Logger.LogDebug($"shield_on faction {__instance.m_character.m_faction} tamed:{__instance.m_character.m_tamed}");
                if (!friendly_factions.Contains(__instance.m_character.m_faction)) {
                    if (!__instance.m_character.m_tamed) {
                        //Logger.LogDebug("Skipping shield damage add.");
                        return;
                    }
                }

                shield_damage += hit.GetTotalDamage();
                //Logger.LogDebug($"Shield adding damage to track {hit.GetTotalDamage()} total damage now: ({shield_damage})");
                if (shield_damage > ValConfig.BloodMagicXPForShieldDamageRatio.Value) {
                    float xp = shield_damage / ValConfig.BloodMagicXPForShieldDamageRatio.Value;
                    shield_damage = 0;
                    Logger.LogDebug("BloodMagic adding XP from shield damage: " + xp);
                    Player.m_localPlayer.RaiseSkill(Skills.SkillType.BloodMagic, xp);
                }
            }
        }

        /// <summary>
        /// Shield of Protection is a status effect and when reapplying it to a creature that still has it, the damage taken by the shield is not reset,
        /// only the timer before the status effect expires is. This modifies the virtual method ResetTime which is inheirited from StatusEffect by SE_Shield
        /// to also reset the accumulated damage when reseting the status effect. 
        /// </summary>
        [HarmonyPatch(typeof(SE_Shield), nameof(SE_Shield.ResetTime))]
        public static class ShieldOfProtectionResetDamageOnRefresh
        {
            private static void Prefix(SE_Shield __instance){
                if (ValConfig.EnableBloodMagic.Value != true ||  ValConfig.EnableBloodMagicShieldRest.Value != true || !Player.m_localPlayer) { return; }
                if (Player.m_localPlayer.GetSkillLevel(Skills.SkillType.BloodMagic) < ValConfig.BloodMagicShieldResetLevel.Value) { return; }
                __instance.m_damage = 0f;
            }
        }
    }
}
