using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Managers;
using UnityEngine;

namespace ImpactfulSkills.patches
{
    public static class AnimalWhisper
    {
        public static Skills.SkillType AnimalHandling = 0;
        public static void SetupAnimalSkill()
        {
            SkillConfig animalh = new SkillConfig();
            animalh.Name = "AnimalHandling";
            animalh.Description = "Your knowledge of animals.";
            animalh.Icon = ImpactfulSkills.EmbeddedResourceBundle.LoadAsset<Sprite>("Assets/Custom/Icons/skill_icons/animalWhisper.png");
            animalh.Identifier = "midnightsfx.animalwhisper";
            animalh.IncreaseStep = 0.1f;
            AnimalHandling = SkillManager.Instance.AddSkill(animalh);
        }

        [HarmonyPatch(typeof(Tameable), nameof(Tameable.DecreaseRemainingTime))]
        public static class IncreaseTamingSpeed
        {
            private static void Prefix(Tameable __instance, ref float time)
            {
                // Check if the player is close enough to the animal
                if (Player.m_localPlayer != null && Vector3.Distance(Player.m_localPlayer.transform.position, __instance.transform.position) <= 20f)
                {
                    float player_skill_factor = Player.m_localPlayer.GetSkillFactor(AnimalHandling);
                    time *= ((player_skill_factor * ValConfig.AnimalTamingSpeedFactor.Value)/100f + 1f);

                    // Gain a little XP for the skill
                    Player.m_localPlayer.RaiseSkill(AnimalHandling, ValConfig.AnimalTamingSkillGainRate.Value);
                }
            }
        }

        //[HarmonyPatch(typeof(Tameable), nameof(Tameable.DecreaseRemainingTime))]
        //public static class IncreaseTamingEatFrequency
        //{
        //    private static void Postfix()
        //    {

        //    }
        //}

        
        [HarmonyPatch(typeof(Tameable), nameof(Tameable.OnDeath))]
        public static class IncreaseTamedAnimalYield
        {
            private static void Postfix(Tameable __instance)
            {
                if (Player.m_localPlayer != null && Vector3.Distance(Player.m_localPlayer.transform.position, __instance.transform.position) <= 20f) {

                }
            }
        }
    }
}
