using HarmonyLib;
using ImpactfulSkills.patches;
using System;
using System.Reflection;
using UnityEngine;

namespace ImpactfulSkills.compatibility {

    /// <summary>
    /// ZenBeehive replaces the beehive's one shot extract with a container UI: it transpiles the
    /// Beehive.Extract() call out of Beehive.Interact and opens a virtual inventory instead, so
    /// RPC_Extract never runs and the honey is moved out a stack at a time (or eaten straight out of
    /// the hive). The harvest XP and the honey bonus both have to be rebuilt around that.
    ///
    /// The bonus honey is shown in the hive, the same as it is without ZenBeehive, but it cannot be
    /// handed over the same way. Without ZenBeehive the harvester spawns it themselves when the hive
    /// owner reports what came out. ZenBeehive's container instead reads the honey level to fill itself
    /// and then writes whatever is left back into the hive with ResetLevel + IncreseLevel, so a bonus
    /// added to that read would be stored as real honey and the container would reload from the inflated
    /// number again - a hive would refill itself every time one honey was dragged out.
    ///
    /// So the bonus is shown, but never stored. AnimalWhisper only ever adds it to a read that has been
    /// armed for display (the hover text, and the container load below), and the write back subtracts it
    /// again so the hive ZDO only ever holds real honey. Bonus honey is taken last, so every item the
    /// player pulls out costs the hive one real honey until it is empty and the rest of what they get is
    /// the bonus. Each hive's rolled bonus is remembered against the honey level it was rolled for, which
    /// keeps the hover text, the container and the arithmetic the player can see (it had 6, I took 2, it
    /// has 4) all agreeing with each other.
    /// </summary>
    internal static class ZenBeehive {
        internal const string PluginGUID = "ZenDragon.ZenBeehive";

        private const string InventoryType = "ZenBeehive.BeehiveInventory";
        private const string LoadInventoryMethod = "LoadInventory";
        private const string SetHoneyLevelMethod = "SetHoneyLevel";
        private const string UpdateHoneyMethod = "UpdateHoneyFromInventory";

        /// <summary>
        /// True once ZenBeehive is handling extraction, whether or not we managed to hook it. The
        /// unscoped honey bonus has to stay off either way, since it is the container reload that turns
        /// it into duplication.
        /// </summary>
        internal static bool HandlesExtraction => Modcheck.IsZenBeehiveEnabled;

        private static bool? bonus_supported;

        /// <summary>
        /// Showing the bonus in the hive is all or nothing: without the write back that takes it out
        /// again, putting it in the container would let ZenBeehive store it as real honey. If either
        /// half is missing we fall back to granting the harvest XP and nothing else.
        /// </summary>
        internal static bool BonusSupported {
            get {
                if (bonus_supported == null) {
                    bonus_supported = Modcheck.IsZenBeehiveEnabled
                        && ZenMethod(LoadInventoryMethod) != null
                        && ZenMethod(SetHoneyLevelMethod) != null;
                }
                return bonus_supported.Value;
            }
        }

        private static MethodBase ZenMethod(string name) {
            Type inventory = AccessTools.TypeByName(InventoryType);
            if (inventory == null) { return null; }
            return AccessTools.Method(inventory, name);
        }

        /// <summary>The hive whose container was last loaded, for the capacity text.</summary>
        private static Beehive open_hive;

        /// <summary>
        /// ZenBeehive's container is a single slot, so the bonus cannot push a hive past one stack of
        /// whatever it produces without Inventory.AddItem dropping the overflow.
        /// </summary>
        internal static int StackRoom(Beehive hive, int level) {
            if (hive.m_honeyItem == null) { return 0; }
            return Mathf.Max(0, hive.m_honeyItem.m_itemData.m_shared.m_maxStackSize - level);
        }

        /// <summary>
        /// BeehiveInventory is a component ZenBeehive adds to the hive prefab, so the hive is just the
        /// Beehive on the same object. Typed as object because we never reference their assembly.
        /// </summary>
        private static Beehive GetHive(object beehive_inventory) {
            Component component = beehive_inventory as Component;
            if (component == null) { return null; }
            return component.GetComponent<Beehive>();
        }

        /// <summary>
        /// Puts the bonus in the container, the same bonus AnimalWhisper already shows in the hover text,
        /// so a hive reads the same before it is opened as it does once the container is up. ZenBeehive
        /// fills it from a single GetHoneyLevel call, so arming that call is enough, and letting their own
        /// code do the adding matters: VirtualInventory rejects items added to the container from anywhere
        /// else.
        /// </summary>
        [HarmonyPatch]
        internal static class ContainerShowsBonus {
            private static bool Prepare() { return BonusSupported; }

            private static MethodBase TargetMethod() { return ZenMethod(LoadInventoryMethod); }

            private static void Prefix(object __instance) {
                open_hive = GetHive(__instance);
                AnimalWhisper.armed_hive = open_hive;
            }

            private static void Postfix() { AnimalWhisper.armed_hive = null; }
        }

        /// <summary>
        /// The write back, and the only place the honey that left the hive can be counted. ZenBeehive
        /// funnels take all, dragging a stack out, eating honey straight from the container and dropping
        /// it on the ground into UpdateHoneyFromInventory, which hands the container's remaining
        /// contents to SetHoneyLevel. That count still has the bonus in it, so it comes back out here
        /// before the hive stores it.
        /// </summary>
        [HarmonyPatch]
        internal static class ContainerHarvest {
            private static bool Prepare(MethodBase original) {
                if (original == null && BonusSupported) {
                    Logger.LogInfo("ZenBeehive detected, bee bonuses will be shown in its container rather than applied to the extract.");
                }
                return BonusSupported;
            }

            private static MethodBase TargetMethod() { return ZenMethod(SetHoneyLevelMethod); }

            private static void Prefix(object __instance, ref int level) {
                Beehive hive = GetHive(__instance);
                if (hive == null) { return; }

                // Nothing is armed here, so this is the hive's real honey rather than what it displays.
                int real = hive.GetHoneyLevel();
                int bonus = AnimalWhisper.ShownHoneyBonus(hive, real);
                int in_container = level;

                int taken = Mathf.Max(0, (real + bonus) - in_container);

                // What the player leaves behind, minus the part of it that was only ever on display.
                // Clamped to what the hive holds as well, because the container cannot legitimately be
                // carrying more real honey than that however it came to be holding it.
                int real_left = Mathf.Clamp(in_container - bonus, 0, real);
                AnimalWhisper.RememberShownHoneyBonus(hive, real_left, in_container - real_left);
                level = real_left;

                if (taken <= 0) { return; }

                Logger.LogDebug($"ZenBeehive container harvest: {taken} from {hive.name} ({real} honey + {bonus} bonus, {real_left} left)");
                AnimalWhisper.GrantHoneyHarvestXP(taken);

                // Beehive.Interact already counted the hive's real honey when the container was opened,
                // so only the bonus the player actually walked away with is still missing.
                int bonus_taken = taken - (real - real_left);
                if (bonus_taken > 0 && Game.instance != null) {
                    Game.instance.IncrementPlayerStat(PlayerStatType.BeesHarvested, bonus_taken);
                }
            }
        }

        /// <summary>
        /// ZenBeehive's amount label reads "held/max", and the bonus deliberately puts a hive over the
        /// max it would normally hold. Runs after theirs.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
        internal static class ContainerCapacityText {
            private static bool Prepare() { return BonusSupported; }

            [HarmonyPriority(Priority.Last)]
            private static void Postfix(InventoryGrid __instance) {
                if (open_hive == null || __instance == null || __instance.m_elements.Count == 0) { return; }
                if (InventoryGui.instance == null || __instance != InventoryGui.instance.ContainerGrid) { return; }

                // The hive's container is the only single slot inventory named after the hive's piece, so
                // this cannot land on a chest that happens to be open instead.
                Inventory inventory = __instance.GetInventory();
                if (inventory == null || inventory.m_width != 1 || inventory.m_height != 1) { return; }
                if (open_hive.m_piece == null || inventory.GetName() != open_hive.m_piece.m_name) { return; }

                ItemDrop.ItemData honey = inventory.GetItem(0);
                if (honey == null || honey.m_stack <= open_hive.m_maxHoney) { return; }

                __instance.m_elements[0].m_amount.text = $"{honey.m_stack}/{honey.m_stack}";
            }
        }

        /// <summary>
        /// Harvest XP on its own, for a future ZenBeehive that has moved the internals the bonus needs.
        /// The hive only ever holds real honey in that case, so the drop in its level is what was taken.
        /// </summary>
        [HarmonyPatch]
        internal static class ContainerHarvestXPOnly {
            private static bool Prepare(MethodBase original) {
                if (Modcheck.IsZenBeehiveEnabled == false || BonusSupported) { return false; }

                bool found = ZenMethod(UpdateHoneyMethod) != null;
                if (original == null) {
                    Logger.LogWarning(found
                        ? $"ZenBeehive is installed but {InventoryType}.{SetHoneyLevelMethod} was not found, so hives will not show bonus honey. Harvesting one still grants Animal Handling XP."
                        : $"ZenBeehive is installed but {InventoryType} does not look the way this mod expects, so honey taken from a hive will not grant Animal Handling XP or a skill bonus. This mod may need an update to match the installed ZenBeehive version.");
                }
                return found;
            }

            private static MethodBase TargetMethod() { return ZenMethod(UpdateHoneyMethod); }

            private static void Prefix(object __instance, out int __state) { __state = HoneyLevel(__instance); }

            private static void Postfix(object __instance, int __state) {
                int taken = __state - HoneyLevel(__instance);
                if (taken <= 0) { return; }

                AnimalWhisper.GrantHoneyHarvestXP(taken);
            }

            private static int HoneyLevel(object beehive_inventory) {
                Beehive hive = GetHive(beehive_inventory);
                if (hive == null) { return 0; }
                return hive.GetHoneyLevel();
            }
        }
    }
}
