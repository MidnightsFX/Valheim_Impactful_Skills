using HarmonyLib;
using ImpactfulSkills.common;
using Jotunn.Managers;
using System;
using UnityEngine;

namespace ImpactfulSkills.patches
{
    /// <summary>
    /// Past AnimalBonusStarRequiredLevel, a creature that is tamed or born has a chance to gain one extra star.
    /// Only those two moments roll. Hatching and growing up carry the level over the way vanilla does, so a star
    /// gained at birth is kept but never rolled for a second time.
    ///
    /// Taming and birth both run on the creature's owner, which in multiplayer is often not the player who did the
    /// taming or breeding, and a player's skill is only known on their own client. So the owner picks the player
    /// closest to the creature (the one vanilla tells about a tame) and asks their client to roll (RollRPC). That
    /// client rolls against its own skill and, if the creature earns a star, tells the owner (GrantRPC), who sets
    /// the level. When the owner is that player, all of it happens in place during the same call.
    ///
    /// The creature is named by ZDOID in global RPCs rather than through RPCs on its own ZNetView: a newborn's ZDO
    /// has not reached the other peers yet when the roll goes out, and a ZDO targeted RPC to a peer that has not
    /// loaded the object is dropped. The owner always has it loaded, since it spawned or tamed it.
    ///
    /// Vanilla only reads a creature's level from its ZDO when the creature loads, so the owner also tells every
    /// peer that already has it loaded about the new level (LevelChangedRPC). That matters most for a tame, which
    /// everyone nearby has loaded already. Eggs do not need it, ItemDrop reloads its data on hover and pickup.
    /// </summary>
    public static class AnimalBonusStar
    {
        // Internal level 3 is two stars, the most a vanilla creature can have.
        private const int MaxVanillaLevel = 3;
        // How far vanilla looks for the player to tell about a tame (Tameable.Tame), and the range taming speed uses.
        internal const float TameRange = 30f;

        private const string RollRPC = "ISKILL_BonusStarRoll";
        private const string GrantRPC = "ISKILL_BonusStarGrant";
        private const string LevelChangedRPC = "ISKILL_BonusStarLevelChanged";

        private static bool Enabled => ValConfig.EnableAnimalWhisper.Value && ValConfig.EnableAnimalBonusStar.Value;

        [HarmonyPatch(typeof(Game), nameof(Game.Start))]
        public static class RegisterBonusStarRPCs
        {
            // ZRoutedRpc is recreated with ZNet every session, and Game.Start is where vanilla registers its own.
            private static void Postfix() {
                ZRoutedRpc.instance.Register<ZDOID, string>(RollRPC, RPC_Roll);
                ZRoutedRpc.instance.Register<ZDOID>(GrantRPC, RPC_Grant);
                ZRoutedRpc.instance.Register<ZDOID, int>(LevelChangedRPC, RPC_LevelChanged);
            }
        }

        [HarmonyPatch(typeof(Tameable), nameof(Tameable.Tame))]
        public static class BonusStarOnTame
        {
            // Tame is also called for creatures that are already tame (TameAllInArea), and only tames on the owner.
            private static void Prefix(Tameable __instance, out bool __state) {
                __state = Enabled && __instance.m_nview != null && __instance.m_nview.IsValid() && __instance.m_nview.IsOwner() && __instance.IsTamed() == false;
            }

            private static void Postfix(Tameable __instance, bool __state) {
                if (__state && __instance.IsTamed()) {
                    OfferBonusStar(__instance.m_nview, __instance.transform.position, TameRange);
                }
            }
        }

        // The parent whose birth is in progress, and the offspring that birth spawned.
        private static Procreation birthing_parent;
        private static ZNetView newborn;

        /// <summary>
        /// The offspring is spawned inline in Procreate, with nothing that hands it out, and other mods (Star Level
        /// System) replace the vanilla code that sets it up. So rather than look for it in there, it is caught as it
        /// spawns, see CatchNewborn. It is a creature for live births and an egg for hens and asksvin.
        /// </summary>
        [HarmonyPatch(typeof(Procreation), nameof(Procreation.Procreate))]
        public static class BonusStarOnBirth
        {
            private static void Prefix(Procreation __instance) {
                newborn = null;
                birthing_parent = null;
                // Procreate only gives birth on the owner, and only from a pregnancy.
                if (Enabled && __instance.m_nview != null && __instance.m_nview.IsValid() && __instance.m_nview.IsOwner() && __instance.IsPregnant()) {
                    birthing_parent = __instance;
                }
            }

            private static void Postfix(Procreation __instance) {
                if (birthing_parent != __instance || newborn == null) { return; }
                OfferBonusStar(newborn, __instance.transform.position, ValConfig.AnimalBreedingXPRange.Value);
            }

            // Runs after the postfix, and also when Procreate throws, so nothing spawned later is mistaken for a newborn.
            private static Exception Finalizer(Exception __exception) {
                birthing_parent = null;
                newborn = null;
                return __exception;
            }
        }

        [HarmonyPatch(typeof(ZNetView), nameof(ZNetView.Awake))]
        public static class CatchNewborn
        {
            private static void Postfix(ZNetView __instance) {
                if (birthing_parent == null || newborn != null) { return; }
                if (__instance.GetComponent<Character>() == null && __instance.GetComponent<ItemDrop>() == null) { return; }
                newborn = __instance;
            }
        }

        /// <summary>
        /// On the creature's owner: asks the player closest to it to roll for a bonus star, if it can still take one.
        /// </summary>
        private static void OfferBonusStar(ZNetView creature, Vector3 position, float range) {
            if (creature == null || creature.IsValid() == false) { return; }
            if (TryGetLevel(creature.gameObject, out int level, out string name) == false || BelowCap(level) == false) { return; }

            Player closest = Player.GetClosestPlayer(position, range);
            if (closest == null || closest.m_nview == null || closest.m_nview.IsValid() == false) { return; }

            // A player's own ZDO is owned by their client, so that is where their skill can be read.
            ZRoutedRpc.instance.InvokeRoutedRPC(closest.m_nview.GetZDO().GetOwner(), RollRPC, creature.GetZDO().m_uid, name);
        }

        /// <summary>
        /// On the closest player's client: rolls against their own skill, and tells the owner when the creature earns
        /// a star. The owner has already checked it is below the cap, so the message can go out now.
        /// </summary>
        private static void RPC_Roll(long owner, ZDOID creature, string name) {
            if (Enabled == false || Player.m_localPlayer == null) { return; }

            float skill_factor = Player.m_localPlayer.GetSkillFactor(AnimalWhisper.AnimalHandling);
            if (skill_factor * 100f < ValConfig.AnimalBonusStarRequiredLevel.Value) { return; }

            float chance = SkillCaps.BonusFactor(skill_factor, ValConfig.AnimalHandlingBonusesPastLevel100) * ValConfig.AnimalBonusStarChance.Value;
            float roll = UnityEngine.Random.value;
            Logger.LogDebug($"Bonus star roll for {name}: {roll} < {chance}");
            if (roll >= chance) { return; }

            ZRoutedRpc.instance.InvokeRoutedRPC(owner, GrantRPC, creature);
            string message = LocalizationManager.Instance.TryTranslate("$animal_bonus_star").Replace("{0}", LocalizationManager.Instance.TryTranslate(name));
            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, message);
        }

        /// <summary>
        /// On the creature's owner: adds the star that was rolled.
        /// </summary>
        private static void RPC_Grant(long sender, ZDOID creature) {
            if (Enabled == false || ZNetScene.instance == null) { return; }

            GameObject target = ZNetScene.instance.FindInstance(creature);
            ZNetView nview = target != null ? target.GetComponent<ZNetView>() : null;
            // Ownership can change hands while the roll is out. Whoever owns it now never asked, so it is dropped.
            if (nview == null || nview.IsValid() == false || nview.IsOwner() == false) { return; }
            if (TryGetLevel(target, out int level, out _) == false || BelowCap(level) == false) { return; }

            level += 1;
            Character chara = target.GetComponent<Character>();
            if (chara != null) {
                if (compatibility.StarLevelSystem.SetLevel(chara, level) == false) {
                    chara.SetLevel(level);
                }
                Logger.LogDebug($"{chara.m_name} gained a bonus star, now level {level}");
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, LevelChangedRPC, creature, level);
                return;
            }

            ItemDrop egg = target.GetComponent<ItemDrop>();
            egg.SetQuality(level);
            // SetQuality only changes this copy. Vanilla saves it when the egg starts, which a roll answered by
            // another player's client arrives after.
            egg.Save();
            Logger.LogDebug($"{egg.m_itemData.m_shared.m_name} gained a bonus star, now quality {level}");
        }

        /// <summary>
        /// On every peer: shows the new level on a creature that is already loaded here.
        /// </summary>
        private static void RPC_LevelChanged(long sender, ZDOID creature, int level) {
            if (ZNetScene.instance == null || level < 1) { return; }

            GameObject target = ZNetScene.instance.FindInstance(creature);
            Character chara = target != null ? target.GetComponent<Character>() : null;
            // The owner has set it already.
            if (chara == null || chara.m_nview == null || chara.m_nview.IsValid() == false || chara.m_nview.IsOwner()) { return; }

            chara.m_level = level;
            // What vanilla's SetLevel does for the visuals on the owner. Star Level System applies its own instead.
            if (compatibility.StarLevelSystem.Installed == false) {
                chara.m_onLevelSet?.Invoke(level);
            }
        }

        /// <summary>
        /// The level of a creature, or of an egg as it is laid. An egg carries its level as its quality, which vanilla
        /// gives the creature that hatches from it. A stack of eggs is not one creature's level (Star Level System can
        /// lay eggs as a stack of loot instead), so a stack is left alone.
        /// </summary>
        private static bool TryGetLevel(GameObject target, out int level, out string name) {
            Character chara = target.GetComponent<Character>();
            if (chara != null) {
                // The ZDO first: Star Level System writes a newborn's level there during the birth, but only sets the
                // creature's own copy once its setup runs a moment later.
                level = chara.m_nview.GetZDO().GetInt(ZDOVars.s_level, chara.GetLevel());
                name = chara.m_name;
                return true;
            }

            ItemDrop egg = target.GetComponent<ItemDrop>();
            if (egg != null && egg.m_itemData.m_stack == 1) {
                level = egg.m_itemData.m_quality;
                name = egg.m_itemData.m_shared.m_name;
                return true;
            }

            level = 0;
            name = null;
            return false;
        }

        // Without Star Level System, vanilla's two stars. With it there is no cap here, its own settings decide.
        private static bool BelowCap(int level) {
            return compatibility.StarLevelSystem.Installed || level < MaxVanillaLevel;
        }
    }
}
