using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Managers;
using System;
using UnityEngine;

namespace ImpactfulSkills.patches
{
    public static class AnimalWhisper
    {
        public static Skills.SkillType AnimalHandling = 0;
        public static void SetupAnimalSkill()
        {
            SkillConfig animalh = new SkillConfig();
            animalh.Name = "$skill_AnimalHandling";
            animalh.Description = "$skill_AnimalHandling_description";
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
                if (ValConfig.EnableAnimalWhisper.Value == true && Player.m_localPlayer != null && Vector3.Distance(Player.m_localPlayer.transform.position, __instance.transform.position) <= 30f)
                {
                    float player_skill_factor = Player.m_localPlayer.GetSkillFactor(AnimalHandling);
                    float modified_time = time * ((player_skill_factor * ValConfig.AnimalTamingSpeedFactor.Value) + 1f);
                    Logger.LogDebug($"animal taming remaining time {time}, modified: {modified_time}");
                    time = modified_time;
                    // Gain a little XP for the skill
                    Player.m_localPlayer.RaiseSkill(AnimalHandling, ValConfig.AnimalTamingSkillGainRate.Value);
                }
            }
        }

        // Should we patch the UI to display more precise information on taming time remaining? or leave that to other mods?

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
                if (ValConfig.EnableAnimalWhisper.Value == true && Player.m_localPlayer != null && Vector3.Distance(Player.m_localPlayer.transform.position, __instance.transform.position) <= 20f) {
                    // Only increase drops of the character is also tamed
                    if (__instance.gameObject.GetComponent<Character>()?.m_tamed != true) { return; }
                    CharacterDrop tamechardrop = __instance.gameObject.GetComponent<CharacterDrop>();
                    if (tamechardrop != null) {
                        float player_skill_factor = Player.m_localPlayer.GetSkillFactor(AnimalHandling);
                        foreach (var drop in tamechardrop.m_drops){
                            int drop_amount = 0;
                            float min_drop = drop.m_amountMin * (ValConfig.TamedAnimalLootIncreaseFactor.Value * (player_skill_factor * 100f)) / 100f;
                            float max_drop = drop.m_amountMax * (ValConfig.TamedAnimalLootIncreaseFactor.Value * (player_skill_factor * 100f)) / 100f;
                            if (min_drop > 0 && max_drop > 0 && min_drop != max_drop) {
                                drop_amount = UnityEngine.Random.Range((int)min_drop, (int)max_drop);
                            } else if (min_drop == max_drop) {
                                drop_amount = (int)Math.Round(min_drop, 0);
                            }
                            if (drop.m_chance != 1 && UnityEngine.Random.value > drop.m_chance) {
                                // This drop failed its chance to spawn
                                continue;
                            }

                            Logger.LogDebug($"AnimalWhisper extra drops {drop_amount} {drop.m_prefab.name}");
                            Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0, 360), 0f);
                            for (int i = 0; i < drop_amount; i++) {
                                UnityEngine.Object.Instantiate(drop.m_prefab, __instance.transform.position, rotation);
                            }
                        }
                    }
                }
            }
        }
    }
}
