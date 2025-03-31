using HarmonyLib;

namespace ImpactfulSkills.patches
{
    public static class BloodMagic
    {
        private static float shield_damage = 0f;

        [HarmonyPatch(typeof(SE_Shield), nameof(SE_Shield.OnDamaged))]
        public static class VoyagerSpeedPatch
        {
            private static void Postfix(HitData hit, Character attacker, SE_Shield __instance)
            {
                if (ValConfig.EnableBloodMagic.Value != true || Player.m_localPlayer == null) { return; }

                shield_damage += hit.GetTotalDamage();
                if (shield_damage > ValConfig.BloodMagicXPForShieldDamageRatio.Value)
                {
                    float xp = shield_damage / ValConfig.BloodMagicXPForShieldDamageRatio.Value;
                    shield_damage = 0;
                    Player.m_localPlayer.RaiseSkill(Skills.SkillType.BloodMagic, xp);

                }
            }
        }
    }
}
