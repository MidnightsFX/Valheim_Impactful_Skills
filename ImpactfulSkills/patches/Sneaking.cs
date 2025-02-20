using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImpactfulSkills.patches
{
    public static class Sneaking
    {
        [HarmonyPatch(typeof(Character), nameof(Character.IsCrouching))]
        public static class MovespeedWhenCrouchingPatch
        {
            private static void Postfix(Character __instance, bool __result)
            {
                if (Player.m_localPlayer != null && __instance == Player.m_localPlayer) {
                    float player_skill_factor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Sneak);
                    if (__result) {
                        // Sneaking
                        Player.m_localPlayer.m_crouchSpeed += 0.5f;
                    } else {
                        // Not sneaking
                        Player.m_localPlayer.m_crouchSpeed -= 1f;
                    }
                }
            }
        }
        
    }
}
