using HarmonyLib;
using ImpactfulSkills.common;
using System.Collections.Generic;
using TMPro;
using System.Reflection;
using System.Reflection.Emit;

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
        public static class BloodMagicShieldXPPatch
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
        /// only the timer before the status effect expires is. SE_Shield does not override ResetTime, so this hooks the call to it in
        /// Character.RPC_Damage instead, which is where a hit refreshes a status effect the character already has.
        /// </summary>
        [HarmonyPatch(typeof(Character))]
        public static class ShieldOfProtectionResetDamageOnRefresh
        {
            /// <summary>
            ///     Cached reference to target method info to avoid duplication in memory if the transpiler ever has to run again, 
            ///     since objects acquired via reflection are rarely (if ever) garbage collected.
            /// </summary>
            private static readonly MethodInfo StatusEffectResetTimeInfo = AccessTools.Method(typeof(StatusEffect), nameof(StatusEffect.ResetTime));

            /// <summary>
            ///     Target the call to StatusEffect.ResetTime() as it only occurs once in the RPC_Damage method and allows us
            ///     to ensure the shield health reset code only runs when a character is hit by something that triggers a refresh
            ///     of an existing status effect rather than patching the StatusEffect.ResetTime() method itself, which can run
            ///     with an extremely high frequency.  
            /// </summary>
            /// <param name="instructions"></param>
            /// <returns></returns>
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(Character.RPC_Damage))]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
            {
                var codeMatcher = new CodeMatcher(instructions);
                var codeMatches = new CodeMatch[] {
                    new(OpCodes.Callvirt, StatusEffectResetTimeInfo)
                };
                if (codeMatcher.TryMatchStartForward("Unable to patch blood magic shield reset in RPC_Damage.", codeMatches))
                {
                    // RPC_Damage(long sender, HitData hit), the hit carries the casters skill level.
                    codeMatcher.InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldarg_2),
                        Transpilers.EmitDelegate(ResetShieldHealth)
                    );
                }
                return codeMatcher.Instructions();
            }

            /// <summary>
            ///     Check if the status effect is a shield and the caster's blood magic level is high enough, then reset the shield
            ///     damage accumulated if all conditions are met. The caster's level comes from the hit, since RPC_Damage runs on
            ///     whichever machine owns the shielded character, where the local player (if any) is not the caster.
            ///     Returns the statusEffect that is passed to this
            ///     method so that it is placed back onto the stack such that the original call to StatusEffect.ResetTime()
            ///     can still run. This also avoids consuming or replacing any vanilla code to hopefully maintain compatiability
            ///     with most other mods and avoids breaking other transpiler patches that rely on the StatusEffect being placed 
            ///     on the stack.
            /// </summary>
            /// <param name="statusEffect"></param>
            /// <param name="hit"></param>
            /// <returns></returns>
            private static StatusEffect ResetShieldHealth(StatusEffect statusEffect, HitData hit)
            {
                if (ValConfig.EnableBloodMagic.Value != true ||  ValConfig.EnableBloodMagicShieldReset.Value != true ||
                    statusEffect.m_name != "$se_shield") {
                    return statusEffect;
                }

                if (hit.m_skillLevel < ValConfig.BloodMagicShieldResetLevel.Value) {
                    return statusEffect;
                }
                
                if (statusEffect is SE_Shield shield) {
                    shield.m_damage = 0f;
                }
                return statusEffect;
            }
        }
    }
}
