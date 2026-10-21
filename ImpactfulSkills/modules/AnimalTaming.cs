using HarmonyLib;
using ImpactfulSkills.common;
using Jotunn.Managers;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ImpactfulSkills.patches
{
    /// <summary>
    /// Active taming: a creature being tamed stays calm around skilled players (past TamingCalmLevel), and food can be
    /// fed to it by hand for a share of its taming time, once per TamingHandFeedCooldown.
    ///
    /// Creature AI and taming both run on the creature's owner, which is often not the player near it, and a player's
    /// skill is only known on their own client. So every client writes its own Animal Handling level onto its player's
    /// ZDO, which the owner reads for whichever player its AI is looking at, and for the player closest to it when
    /// speeding up taming (AnimalWhisper.IncreaseTamingSpeed). Hand feeding is decided on the feeder's
    /// client with their own skill, and the owner is told how much progress to add (HandFeedRPC).
    /// </summary>
    public static class AnimalTaming
    {
        private const string HandFeedRPC = "ISKILL_HandFeed";
        // On each player's ZDO, written by that player's own client.
        private static readonly int AnimalHandlingLevelKey = "ISKILL_AnimalHandlingLevel".GetStableHashCode();
        // On the creature's ZDO, written by its owner. Shared, so a creature can only be hand fed once per cooldown by anyone.
        internal static readonly int HandFedTimeKey = "ISKILL_HandFedTime".GetStableHashCode();

        private const float FoodCheckInterval = 2f;

        private static bool CalmEnabled => ValConfig.EnableAnimalWhisper.Value && ValConfig.EnableTamingCalm.Value;
        internal static bool HandFeedingEnabled => ValConfig.EnableAnimalWhisper.Value && ValConfig.EnableTamingHandFeeding.Value;

        // Only kept on the owner, so it starts over when the creature changes hands or reloads.
        private class CalmState {
            public float lastAttacked = float.NegativeInfinity;
            public float nextFoodCheck;
            public bool foodNearby;
        }
        private static readonly ConditionalWeakTable<MonsterAI, CalmState> calmStates = new ConditionalWeakTable<MonsterAI, CalmState>();
        private static readonly Collider[] foodColliders = new Collider[128];
        private static int itemMask;

        /// <summary>
        /// A creature someone has started taming: not tame yet, and either fed or already partway there. A wild animal
        /// nobody has fed never counts.
        /// </summary>
        internal static bool IsBeingTamed(Tameable tameable) {
            if (tameable == null || tameable.m_nview == null || !tameable.m_nview.IsValid() || tameable.IsTamed()) { return false; }
            return tameable.GetTameness() > 0 || !tameable.IsHungry();
        }

        /// <summary>
        /// Writes the local player's level onto their ZDO. Only done when they spawn, since every spawn is a new ZDO,
        /// and when the skill gains a level, so a boost from gear or a status effect is only picked up at the next of
        /// those.
        /// </summary>
        private static void PublishLevel(Player player) {
            if (player != Player.m_localPlayer || player.m_nview == null || !player.m_nview.IsValid()) { return; }
            player.m_nview.GetZDO().Set(AnimalHandlingLevelKey, Mathf.FloorToInt(player.GetSkillLevel(AnimalWhisper.AnimalHandling)));
        }

        // Skills are loaded just before this, on the first spawn and on every respawn.
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        public static class PublishLevelOnSpawn
        {
            private static void Postfix(Player __instance) {
                PublishLevel(__instance);
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnSkillLevelup))]
        public static class PublishLevelOnLevelUp
        {
            private static void Postfix(Player __instance, Skills.SkillType skill) {
                if (skill == AnimalWhisper.AnimalHandling) { PublishLevel(__instance); }
            }
        }

        private static int PublishedLevel(Player player) {
            if (player.m_nview == null || !player.m_nview.IsValid()) { return 0; }
            return player.m_nview.GetZDO().GetInt(AnimalHandlingLevelKey, 0);
        }

        /// <summary>
        /// A player's skill factor for bonuses, from the level their own client published, so a creature's owner can
        /// use the skill of whoever is near it.
        /// </summary>
        internal static float PublishedSkillFactor(Player player) {
            return SkillCaps.BonusFactor(PublishedLevel(player) / 100f, ValConfig.AnimalHandlingBonusesPastLevel100);
        }

        /// <summary>
        /// On the creature's owner: whether it should ignore this player. Only while it is being tamed, which here means
        /// it is still fed or has food it can eat nearby, and only when it has not been attacked recently. Runs every
        /// frame for creatures with a target, so the cheap checks go first.
        /// </summary>
        internal static bool ShouldStayCalm(MonsterAI ai, Character target) {
            if (!(target is Player player) || !CalmEnabled) { return false; }
            Tameable tameable = ai.m_tamable;
            if (tameable == null || ai.m_character == null || ai.m_character.IsTamed() || ai.HuntPlayer()) { return false; }
            if (PublishedLevel(player) < ValConfig.TamingCalmLevel.Value) { return false; }

            CalmState state = calmStates.GetOrCreateValue(ai);
            if (Time.time - state.lastAttacked < ValConfig.TamingCalmAttackedCooldown.Value) { return false; }
            return !tameable.IsHungry() || HasFoodNearby(ai, state);
        }

        /// <summary>
        /// Food the creature eats within the range it searches for food. Unlike vanilla this does not check for a path
        /// to it, being nearby is enough.
        /// </summary>
        private static bool HasFoodNearby(MonsterAI ai, CalmState state) {
            if (ai.m_consumeTarget != null) { return true; }
            if (Time.time < state.nextFoodCheck) { return state.foodNearby; }
            state.nextFoodCheck = Time.time + FoodCheckInterval;
            state.foodNearby = false;

            if (itemMask == 0) { itemMask = LayerMask.GetMask("item"); }
            int count = Physics.OverlapSphereNonAlloc(ai.transform.position, ai.m_consumeSearchRange, foodColliders, itemMask);
            for (int i = 0; i < count; i++) {
                Rigidbody body = foodColliders[i].attachedRigidbody;
                ItemDrop drop = body != null ? body.GetComponent<ItemDrop>() : null;
                if (drop != null && drop.m_itemData != null && ai.CanConsume(drop.m_itemData)) {
                    state.foodNearby = true;
                    break;
                }
            }
            return state.foodNearby;
        }

        private static void MarkAttacked(MonsterAI ai) {
            if (ai.m_tamable == null || ai.m_character == null || ai.m_character.IsTamed()) { return; }
            calmStates.GetOrCreateValue(ai).lastAttacked = Time.time;
        }

        /// <summary>
        /// Every way a creature turns on a player goes through its target: it is alerted by seeing its target up close,
        /// boars flee from their target, and the rest stalk it. So a skilled player is simply never picked as one.
        /// Other alerts, like fleeing fire, are left alone.
        /// </summary>
        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.FindEnemy))]
        public static class CalmCreaturesIgnoreSkilledPlayers
        {
            private static void Postfix(BaseAI __instance, ref Character __result) {
                if (__result != null && __instance is MonsterAI ai && ShouldStayCalm(ai, __result)) {
                    __result = null;
                }
            }
        }

        /// <summary>
        /// Drops a skilled player the creature was already targeting, such as one it found before it was fed, and calms
        /// it so taming, which stops while a creature is frightened, carries on straight away.
        /// </summary>
        [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateTarget))]
        public static class CalmCreaturesDropSkilledPlayers
        {
            private static void Postfix(MonsterAI __instance, ref bool canHearTarget, ref bool canSeeTarget) {
                if (__instance.m_targetCreature == null || !ShouldStayCalm(__instance, __instance.m_targetCreature)) { return; }
                __instance.m_targetCreature = null;
                canHearTarget = false;
                canSeeTarget = false;
                // Vanilla already marked it as having a target this frame, which shows on its health bar.
                __instance.SetTargetInfo(ZDOID.None);
                if (__instance.IsAlerted()) {
                    __instance.SetAlerted(false);
                }
            }
        }

        // Damage is only applied on the owner. Damage without an attacker, like falling, is not an attack.
        [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.OnDamaged))]
        public static class CalmEndsWhenAttacked
        {
            private static void Postfix(MonsterAI __instance, Character attacker) {
                if (attacker != null) { MarkAttacked(__instance); }
            }
        }

        // A projectile landing close by alerts the creature in vanilla, so it counts as an attack too.
        [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.RPC_OnNearProjectileHit))]
        public static class CalmEndsOnNearMiss
        {
            private static void Postfix(MonsterAI __instance) {
                if (!__instance.m_nview.IsOwner() || ZoneSystem.instance.GetGlobalKey(GlobalKeys.PassiveMobs)) { return; }
                MarkAttacked(__instance);
            }
        }

        [HarmonyPatch(typeof(Tameable), nameof(Tameable.Awake))]
        public static class RegisterHandFeedRPC
        {
            private static void Postfix(Tameable __instance) {
                if (__instance.m_nview == null || !__instance.m_nview.IsValid()) { return; }
                Tameable tameable = __instance;
                __instance.m_nview.Register<float>(HandFeedRPC, (sender, fraction) => RPC_HandFeed(tameable, fraction));
            }
        }

        /// <summary>
        /// Seconds until the creature can be hand fed again. Read from its ZDO, so it is the same on every client.
        /// </summary>
        internal static float HandFeedCooldownLeft(Tameable tameable) {
            if (ZNet.instance == null || tameable.m_nview == null || !tameable.m_nview.IsValid()) { return 0f; }
            long fedAt = tameable.m_nview.GetZDO().GetLong(HandFedTimeKey, 0L);
            if (fedAt == 0L) { return 0f; }
            float cooldown = ValConfig.TamingHandFeedCooldown.Value;
            float since = (float)(ZNet.instance.GetTime() - new DateTime(fedAt)).TotalSeconds;
            return Mathf.Clamp(cooldown - since, 0f, cooldown);
        }

        /// <summary>
        /// On the feeder's client: using food the creature eats on it, from the hotbar while looking at it, feeds it by
        /// hand. Vanilla only takes saddles here and lets food fall through to being eaten by the player, so any food
        /// the creature eats is claimed, even when the creature refuses it.
        /// </summary>
        [HarmonyPatch(typeof(Tameable), nameof(Tameable.UseItem))]
        public static class HandFeed
        {
            private static void Postfix(Tameable __instance, Humanoid user, ItemDrop.ItemData item, ref bool __result) {
                if (__result || !HandFeedingEnabled || item == null || user == null || user != Player.m_localPlayer) { return; }
                MonsterAI ai = __instance.m_monsterAI;
                if (ai == null || __instance.m_character == null || __instance.IsTamed() || !__instance.m_nview.IsValid() || !ai.CanConsume(item)) { return; }
                __result = true;

                Player player = Player.m_localPlayer;
                string name = LocalizationManager.Instance.TryTranslate(__instance.m_character.m_name);
                // Vanilla taming also stops while a creature is frightened.
                if (ai.IsAlerted()) {
                    player.Message(MessageHud.MessageType.Center, LocalizationManager.Instance.TryTranslate("$taming_feed_frightened").Replace("{0}", name));
                    return;
                }
                float wait = HandFeedCooldownLeft(__instance);
                if (wait > 0f) {
                    string message = LocalizationManager.Instance.TryTranslate("$taming_feed_not_ready").Replace("{0}", name).Replace("{1}", Mathf.CeilToInt(wait).ToString());
                    player.Message(MessageHud.MessageType.Center, message);
                    return;
                }
                if (!player.GetInventory().RemoveOneItem(item)) { return; }

                float factor = player.GetBonusSkillFactor(AnimalWhisper.AnimalHandling, ValConfig.AnimalHandlingBonusesPastLevel100);
                float percent = Mathf.Max(0f, Mathf.LerpUnclamped(ValConfig.TamingHandFeedMinPercent.Value, ValConfig.TamingHandFeedMaxPercent.Value, factor));
                Logger.LogDebug($"Hand feeding {__instance.m_character.m_name} for {percent}% taming progress");
                __instance.m_nview.InvokeRPC(HandFeedRPC, percent / 100f);

                // The owner does not show this, it is only for the feeder.
                __instance.m_sootheEffect.Create(__instance.m_character.GetCenterPoint(), Quaternion.identity);
                player.RaiseSkill(AnimalWhisper.AnimalHandling, ValConfig.TamingHandFeedXP.Value);
                string fed = LocalizationManager.Instance.TryTranslate("$taming_fed").Replace("{0}", name).Replace("{1}", Mathf.RoundToInt(percent).ToString());
                player.Message(MessageHud.MessageType.TopLeft, fed);
            }
        }

        /// <summary>
        /// On the creature's owner: feeds it and adds the progress the feeder's skill earned. This sets the remaining
        /// time directly rather than through DecreaseRemainingTime, so the feeding is the share of the taming time the
        /// settings give and is not multiplied again by taming speed. Two feedings that cross on the way here are rare,
        /// the second is dropped.
        /// </summary>
        private static void RPC_HandFeed(Tameable tameable, float fraction) {
            if (!HandFeedingEnabled || tameable == null || !tameable.m_nview.IsValid() || !tameable.m_nview.IsOwner() || tameable.IsTamed()) { return; }
            if (HandFeedCooldownLeft(tameable) > 0f) { return; }

            ZDO zdo = tameable.m_nview.GetZDO();
            zdo.Set(HandFedTimeKey, ZNet.instance.GetTime().Ticks);
            tameable.ResetFeedingTimer();
            // What vanilla does for a creature whose taming moves on, so it stays around.
            if (tameable.m_monsterAI != null) {
                tameable.m_monsterAI.SetDespawnInDay(false);
                tameable.m_monsterAI.SetEventCreature(false);
            }

            float remaining = Mathf.Max(0f, tameable.GetRemainingTime() - Mathf.Clamp01(fraction) * tameable.m_tamingTime);
            zdo.Set(ZDOVars.s_tameTimeLeft, remaining);
            if (remaining <= 0f) {
                tameable.Tame();
            }
        }
    }
}
