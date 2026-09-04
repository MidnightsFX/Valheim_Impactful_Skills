using HarmonyLib;
using ImpactfulSkills.common;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using System.Collections.Generic;
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

        public static void GiveNearbyBreedingXP(Vector3 position) {
            if (ValConfig.EnableAnimalWhisper.Value == false || ZNet.instance == null) { return; }
            float xp = ValConfig.AnimalBreedingXP.Value;
            if (xp <= 0f) { return; }

            DataObjects.XPIncreaseRequest xp_inc = new DataObjects.XPIncreaseRequest();
            xp_inc.Location = position;
            xp_inc.Skill = AnimalHandling;
            xp_inc.Range = ValConfig.AnimalBreedingXPRange.Value;
            xp_inc.Amount = xp;
            ValConfig.SendXPForSkillInArea(xp_inc);
        }

        [HarmonyPatch(typeof(Procreation), nameof(Procreation.MakePregnant))]
        public static class AnimalPregnancyXP
        {
            private static void Postfix(Procreation __instance)
            {
                if (ValConfig.EnableAnimalWhisper.Value == true) {
                    GiveNearbyBreedingXP(__instance.transform.position);
                }
            }
        }

        [HarmonyPatch(typeof(Procreation), nameof(Procreation.Procreate))]
        public static class AnimalBirthXP
        {
            // Birth is inlined in Procreate: when pregnant & due it calls ResetPregnancy() (clearing
            // the pregnant flag) then spawns the offspring. A pregnant -> not-pregnant transition across
            // the call therefore means a birth occurred. The MakePregnant path is a separate call's
            // else-branch, so the two never collide.
            private static void Prefix(Procreation __instance, out bool __state)
            {
                __state = ValConfig.EnableAnimalWhisper.Value == true && __instance.IsPregnant();
            }

            private static void Postfix(Procreation __instance, bool __state)
            {
                if (__state && __instance.IsPregnant() == false) {
                    GiveNearbyBreedingXP(__instance.transform.position);
                }
            }
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

        [HarmonyPatch(typeof(Beehive), nameof(Beehive.RPC_Extract))]
        public static class BetterBeeProduction {
            public static void Prefix(Beehive __instance) {
                if (ValConfig.EnableBeeBonuses.Value && Player.m_localPlayer != null) {
                    int honey = __instance.GetHoneyLevel();
                    if (honey > 0) {
                        Player.m_localPlayer.RaiseSkill(AnimalHandling, honey * ValConfig.BeeHarvestXP.Value);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Beehive), nameof(Beehive.GetHoneyLevel))]
        public static class BeeHivesMoreProductionBySkill {
            public static void Postfix(ref int __result) {
                if (Player.m_localPlayer != null && __result > 0 && ValConfig.EnableBeeBonuses.Value
                    && Player.m_localPlayer.GetSkillLevel(AnimalHandling) >= ValConfig.BetterBeesLevel.Value) {
                    float increase = ValConfig.BeeHoneyOutputIncreaseBySkill.Value * Player.m_localPlayer.GetSkillFactor(AnimalHandling);
                    __result = Mathf.RoundToInt(__result + (increase * __result));
                }
            }
        }

        [HarmonyPatch(typeof(Beehive), nameof(Beehive.CheckBiome))]
        public static class BehivesInAnyBiome {
            public static bool Prefix(Beehive __instance, ref bool __result) {
                if (Player.m_localPlayer == null || ValConfig.EnableBeeBiomeUnrestricted.Value == false) { return true; }

                if (__instance.m_nview.GetZDO() != null && __instance.m_nview.GetZDO().GetBool("IS_BHIVE", false)) {
                    __result = true;
                    return false;
                }
                if (__instance.m_nview.GetZDO() != null && Player.m_localPlayer.GetSkillLevel(AnimalHandling) >= ValConfig.BeeBiomeUnrestrictedLevel.Value) {
                    __instance.m_nview.GetZDO().Set("IS_BHIVE", true);
                    __result = true;
                    return false;
                }
                return true;
            }
        }


        /// <summary>
        /// Rolls a fractional bonus amount into a whole number of items. With
        /// AnimalHandlingFractionalDropsAsChance enabled the fraction becomes the chance of one more
        /// item, so a bonus of 0.4 pays out a single item 40% of the time rather than rounding away to
        /// nothing and leaving low skill levels with no bonus at all on small drops.
        /// </summary>
        private static int RollBonusAmount(float bonus)
        {
            if (bonus <= 0f) { return 0; }
            if (ValConfig.AnimalHandlingFractionalDropsAsChance.Value == false) {
                return Mathf.RoundToInt(bonus);
            }

            int whole = Mathf.FloorToInt(bonus);
            float remainder = bonus - whole;
            if (remainder > 0f && UnityEngine.Random.value < remainder) { whole += 1; }
            return whole;
        }

        /// <summary>
        /// Scales the loot of a tamed creature by the animal handling skill.
        ///
        /// This runs against the drop list vanilla has already finished calculating, so the bonus
        /// inherits the creature's star level, the world resource rate and anything other loot mods
        /// have added or rescaled, instead of re-deriving it from the raw CharacterDrop.m_drops
        /// amounts. GenerateDropList is the single point both drop paths go through: the ragdoll one
        /// (Character.OnDeath -> Ragdoll.Setup -> SaveLootList), which is what nearly every creature
        /// actually uses, and the CharacterDrop.OnDeath fallback for creatures without a ragdoll.
        /// Priority.Last so we are the final postfix to see the list.
        /// </summary>
        [HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))]
        public static class ScaleTamedAnimalLoot
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(CharacterDrop __instance, List<KeyValuePair<GameObject, int>> __result)
            {
                if (ValConfig.EnableAnimalWhisper.Value == false) { return; }
                if (__instance == null || __result == null || __result.Count == 0) { return; }
                if (Player.m_localPlayer == null) { return; }

                // Only increase drops if the character is also tamed
                Character character = __instance.GetComponent<Character>();
                if (character == null || character.IsTamed() == false) { return; }

                float distance = Vector3.Distance(Player.m_localPlayer.transform.position, __instance.transform.position);
                if (distance > ValConfig.AnimalHandlingLootRange.Value) {
                    Logger.LogDebug($"Player too far away from the tamed creature for increased loot: {distance}");
                    return;
                }

                // (F - 1) so the factor is a true total multiplier: the amount vanilla calculated stays
                // in the list and we add (F - 1) x it on top, scaled by skill. F = 1 -> vanilla,
                // F = 3 -> 3x at level 100.
                float bonus_factor = (ValConfig.TamedAnimalLootIncreaseFactor.Value - 1f) * Player.m_localPlayer.GetSkillFactor(AnimalHandling);
                if (bonus_factor <= 0f) { return; }

                for (int i = 0; i < __result.Count; i++) {
                    KeyValuePair<GameObject, int> entry = __result[i];
                    if (entry.Key == null || entry.Value <= 0) { continue; }

                    int bonus = RollBonusAmount(entry.Value * bonus_factor);
                    if (bonus <= 0) { continue; }

                    // Vanilla caps each entry at 100 items, stay within that, but never reduce an
                    // amount another mod deliberately set above it.
                    int total = Mathf.Min(entry.Value + bonus, Mathf.Max(100, entry.Value));
                    Logger.LogDebug($"AnimalWhisper loot scaling {entry.Key.name}: {entry.Value} -> {total}");
                    __result[i] = new KeyValuePair<GameObject, int>(entry.Key, total);
                }
            }
        }

        [HarmonyPatch(typeof(Tameable), nameof(Tameable.OnDeath))]
        public static class TamedAnimalSlaughterXP
        {
            private static void Postfix(Tameable __instance)
            {
                if (ValConfig.EnableAnimalWhisper.Value == false || Player.m_localPlayer == null || __instance == null) { return; }
                // Slaughtering your own livestock teaches animal handling, killing a wild boar does not
                if (__instance.gameObject.GetComponent<Character>()?.IsTamed() != true) { return; }
                if (Vector3.Distance(Player.m_localPlayer.transform.position, __instance.transform.position) > ValConfig.AnimalHandlingLootRange.Value) { return; }

                Player.m_localPlayer.RaiseSkill(AnimalHandling, ValConfig.AnimalTamingSkillGainRate.Value);
            }
        }
    }
}
