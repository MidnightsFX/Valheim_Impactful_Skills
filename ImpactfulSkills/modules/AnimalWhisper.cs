using HarmonyLib;
using ImpactfulSkills.common;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

        /// <summary>
        /// XP for honey pulled out of a hive. Called from the vanilla extract and, when ZenBeehive has
        /// turned hives into containers, from whatever leaves its container instead. This raises the local
        /// player's skill, so it must only ever run on the harvester's own client. See HoneyHarvestedRPC.
        /// </summary>
        public static void GrantHoneyHarvestXP(int honey) {
            if (ValConfig.EnableBeeBonuses.Value == false || Player.m_localPlayer == null || honey <= 0) { return; }

            Player.m_localPlayer.RaiseSkill(AnimalHandling, honey * ValConfig.BeeHarvestXP.Value);
        }

        /// <summary>
        /// The extra honey a harvest of this size is worth at the player's current skill. Always the local
        /// player's skill: both harvest paths hand the bonus over on the client of whoever took the honey.
        /// </summary>
        public static int HoneyHarvestBonus(int honey) {
            if (ValConfig.EnableBeeBonuses.Value == false || Player.m_localPlayer == null || honey <= 0) { return 0; }
            if (Player.m_localPlayer.GetSkillLevel(AnimalHandling) < ValConfig.BetterBeesLevel.Value) { return 0; }

            return RollBonusAmount(honey * ValConfig.BeeHoneyOutputIncreaseBySkill.Value * Player.m_localPlayer.GetSkillFactor(AnimalHandling));
        }

        /// <summary>
        /// Hives show and pay the honey bonus unless ZenBeehive is installed without the internals the
        /// bonus needs, in which case harvesting only grants XP. See ImpactfulSkills.compatibility.ZenBeehive.
        /// </summary>
        private static bool HoneyBonusActive => compatibility.ZenBeehive.HandlesExtraction == false || compatibility.ZenBeehive.BonusSupported;

        private class HiveBonus {
            public int level;
            public int bonus;
        }

        // Weak keys, so a hive that gets torn down takes its entry with it.
        private static readonly ConditionalWeakTable<Beehive, HiveBonus> hive_bonuses = new ConditionalWeakTable<Beehive, HiveBonus>();

        /// <summary>
        /// The one hive whose next GetHoneyLevel is allowed to include the bonus. Arming is single use, so
        /// a read that is not a display call cannot pick the bonus up even if something throws in between.
        /// </summary>
        internal static Beehive armed_hive;

        /// <summary>
        /// The bonus this hive is showing on top of its real honey, for the local player. Rolled once per
        /// honey level and remembered, because HoneyHarvestBonus can round a fraction up or down at random
        /// and the hover text asks for it every frame, and the harvest has to pay out what was shown.
        /// </summary>
        internal static int ShownHoneyBonus(Beehive hive, int level) {
            if (hive_bonuses.TryGetValue(hive, out HiveBonus remembered) && remembered.level == level) {
                return remembered.bonus;
            }

            int bonus = HoneyHarvestBonus(level);
            if (compatibility.ZenBeehive.HandlesExtraction) {
                bonus = Mathf.Clamp(bonus, 0, compatibility.ZenBeehive.StackRoom(hive, level));
            }
            RememberShownHoneyBonus(hive, level, bonus);
            return bonus;
        }

        internal static void RememberShownHoneyBonus(Beehive hive, int level, int bonus) {
            if (hive_bonuses.TryGetValue(hive, out HiveBonus remembered)) {
                remembered.level = level;
                remembered.bonus = bonus;
                return;
            }
            hive_bonuses.Add(hive, new HiveBonus { level = level, bonus = bonus });
        }

        /// <summary>
        /// Beehive.Extract sends RPC_Extract to whichever client owns the hive, and in multiplayer that is
        /// often not the player who pressed use - it is whoever was in the area first. Anything done there
        /// with Player.m_localPlayer lands on the owner: they got the harvest XP, and their skill decided
        /// the bonus honey. So the owner only reports how much real honey came out, back to the caller
        /// vanilla hands RPC_Extract, and the harvester's own client does the rest in HarvesterCollects.
        /// </summary>
        private const string HoneyHarvestedRPC = "ISKILL_HoneyHarvested";

        [HarmonyPatch(typeof(Beehive), nameof(Beehive.Awake))]
        public static class RegisterHoneyHarvestedRPC {
            public static void Postfix(Beehive __instance) {
                // Vanilla only registers its own RPC when the hive has a ZDO.
                if (__instance.m_nview == null || __instance.m_nview.GetZDO() == null) { return; }
                __instance.m_nview.Register<int>(HoneyHarvestedRPC, (sender, honey) => HarvesterCollects(__instance, honey));
            }
        }

        /// <summary>
        /// The vanilla extract, on the hive owner's client. ZenBeehive transpiles the Extract() call out of
        /// Beehive.Interact, so with that mod installed this only runs on its fallback path, when the
        /// container could not be opened.
        /// </summary>
        [HarmonyPatch(typeof(Beehive), nameof(Beehive.RPC_Extract))]
        public static class BetterBeeProduction {
            public static void Prefix(Beehive __instance, long caller) {
                if (ValConfig.EnableBeeBonuses.Value == false) { return; }
                // Nothing is armed here, so this is the real honey vanilla is about to spawn.
                int honey = __instance.GetHoneyLevel();
                if (honey <= 0) { return; }

                // Handled immediately when the owner is the one harvesting, routed to them otherwise.
                __instance.m_nview.InvokeRPC(caller, HoneyHarvestedRPC, honey);
            }
        }

        /// <summary>
        /// The harvester's half of the vanilla extract. The owner has already spawned the real honey, so
        /// only the bonus is dropped here, stacked above it the same way vanilla stacks its own.
        /// </summary>
        private static void HarvesterCollects(Beehive hive, int honey) {
            if (ValConfig.EnableBeeBonuses.Value == false || Player.m_localPlayer == null || hive == null) { return; }
            // Whoever sent this, a hive never holds more than its max.
            honey = Mathf.Clamp(honey, 0, hive.m_maxHoney);
            if (honey <= 0) { return; }

            int bonus = 0;
            if (HoneyBonusActive) {
                bonus = ShownHoneyBonus(hive, honey);
                // Forget the roll, so the next time the hive fills up to this level it rolls again.
                hive_bonuses.Remove(hive);
            }

            if (bonus > 0 && hive.m_honeyItem != null && hive.m_spawnPoint != null) {
                for (int i = 0; i < bonus; i++) {
                    Vector2 offset = UnityEngine.Random.insideUnitCircle * 0.5f;
                    Vector3 position = hive.m_spawnPoint.position + new Vector3(offset.x, 0.25f * (honey + i), offset.y);
                    ItemDrop drop = UnityEngine.Object.Instantiate(hive.m_honeyItem, position, Quaternion.identity);
                    drop.SetStack(Game.instance.ScaleDrops(hive.m_honeyItem.m_itemData, 1));
                }
                // Beehive.Interact counted the real honey on this client already, only the bonus is missing.
                Game.instance.IncrementPlayerStat(PlayerStatType.BeesHarvested, bonus);
            }

            GrantHoneyHarvestXP(honey + bonus);
        }

        /// <summary>
        /// Puts the bonus in the hover text. Only the displayed read is inflated: the hive's own reads have
        /// to see its real honey, IncreseLevel writes what it reads back into the ZDO, and on the owner's
        /// client an inflated read would be the owner's skill rather than the harvester's.
        /// </summary>
        [HarmonyPatch(typeof(Beehive), nameof(Beehive.GetHoverText))]
        public static class HiveHoverShowsBonus {
            private static bool Prepare() { return HoneyBonusActive; }

            private static void Prefix(Beehive __instance) { armed_hive = __instance; }

            private static void Postfix() { armed_hive = null; }
        }

        /// <summary>
        /// Adds the bonus to the one read that was armed for display, either the hover text above or
        /// ZenBeehive's container load.
        /// </summary>
        [HarmonyPatch(typeof(Beehive), nameof(Beehive.GetHoneyLevel))]
        public static class BeeHivesMoreProductionBySkill {
            public static void Postfix(Beehive __instance, ref int __result) {
                if (armed_hive == null || __instance != armed_hive) { return; }

                armed_hive = null;
                if (__result <= 0) { return; }
                __result += ShownHoneyBonus(__instance, __result);
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
