using HarmonyLib;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ImpactfulSkills.patches
{
    /// <summary>
    /// Masterwork and lightweight equipment.
    ///
    /// A weapon, armor piece or shield crafted or upgraded by a skilled enough smith is marked in its m_customData,
    /// which the game saves with the item and carries through containers, drops and the network. The marks only
    /// say which perks the item earned; how strong they are comes from the (server synced) config, so tuning or
    /// disabling a perk applies to items that already have it.
    ///
    /// Upgrading is the one place m_customData does not survive: DoCrafting removes the old item and adds a new
    /// one, at a workbench and at the Forge of Potential alike. The marks are read off the old item before the
    /// craft and written onto the new one as it is added.
    /// </summary>
    internal static class ForgedItems
    {
        internal const string MasterworkKey = "MidnightsFX.ImpactfulSkills.Masterwork";
        internal const string LightweightKey = "MidnightsFX.ImpactfulSkills.Lightweight";

        private static readonly int MovementModifierIndex = Array.IndexOf(Player.s_equipmentModifierSources, "m_movementModifier");

        /// <summary>
        /// Everything DoCrafting is about to do, and what it did. Armed by the DoCrafting prefix, filled in by the
        /// AddItem postfix, and read (then cleared) by the DoCrafting postfix.
        /// </summary>
        internal class CraftContext
        {
            public Recipe Recipe;
            public string PrefabName;
            public int TargetQuality;
            public int Multiplier;
            public bool IsUpgrade;
            public bool AtUpgrader;
            public bool Forgeable;
            public CraftingStation Station;
            public ItemDrop.ItemData UpgradeItem;
            public bool HadMasterwork;
            public bool HadLightweight;
            public bool IdolFound;

            // Results
            public bool AwaitingItem = true;
            public ItemDrop.ItemData Produced;
            public bool GainedMasterwork;
            public bool GainedLightweight;
            public bool RefineAttempted;
            public bool RefineSucceeded;
        }

        private static CraftContext PendingCraft = null;

        internal static bool IsForgeable(ItemDrop.ItemData.SharedData shared) {
            if (shared == null || shared.m_maxStackSize > 1) { return false; }
            switch (shared.m_itemType) {
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                case ItemDrop.ItemData.ItemType.Bow:
                case ItemDrop.ItemData.ItemType.Shield:
                case ItemDrop.ItemData.ItemType.Helmet:
                case ItemDrop.ItemData.ItemType.Chest:
                case ItemDrop.ItemData.ItemType.Legs:
                case ItemDrop.ItemData.ItemType.Shoulder:
                    return true;
                default:
                    return false;
            }
        }

        private static bool HasMark(ItemDrop.ItemData item, string key) {
            return item != null && item.m_customData != null && item.m_customData.Count > 0 && item.m_customData.ContainsKey(key);
        }

        internal static bool MasterworkActive(ItemDrop.ItemData item) {
            return ValConfig.EnableForging.Value && ValConfig.EnableMasterwork.Value && HasMark(item, MasterworkKey);
        }

        internal static bool LightweightActive(ItemDrop.ItemData item) {
            return ValConfig.EnableForging.Value && ValConfig.EnableLightweight.Value && HasMark(item, LightweightKey);
        }

        internal static bool QualifiesForMasterwork(Player player) {
            return ValConfig.EnableForging.Value && ValConfig.EnableMasterwork.Value && player != null && Forging.ForgingLevel(player) >= ValConfig.MasterworkLevel.Value;
        }

        internal static bool QualifiesForLightweight(Player player) {
            return ValConfig.EnableForging.Value && ValConfig.EnableLightweight.Value && player != null && Forging.ForgingLevel(player) >= ValConfig.LightweightLevel.Value;
        }

        /// <summary>
        /// The multiplier on the item's main stat: damage for weapons, armor for armor, block power for shields.
        /// </summary>
        internal static float StatMultiplier(ItemDrop.ItemData item) {
            if (item.m_customData == null || item.m_customData.Count == 0) { return 1f; }

            float multiplier = 1f;
            if (MasterworkActive(item)) { multiplier += ValConfig.MasterworkStatBonus.Value; }
            if (LightweightActive(item) && item.m_shared.m_movementModifier >= 0f) { multiplier += ValConfig.LightweightNoPenaltyStatBonus.Value; }
            return multiplier;
        }

        /// <summary>
        /// How much of this item's movement speed penalty lightweight takes back while it is worn. Positive, since
        /// the penalty itself is negative.
        /// </summary>
        private static float MovementPenaltyRefund(ItemDrop.ItemData item) {
            if (item == null || item.m_shared.m_movementModifier >= 0f || LightweightActive(item) == false) { return 0f; }
            return -item.m_shared.m_movementModifier * ValConfig.LightweightMovementPenaltyReduction.Value;
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
        private static class ForgedCraftPatch
        {
            [HarmonyPrefix]
            private static void Prefix(InventoryGui __instance) {
                PendingCraft = null;
                if (ValConfig.EnableForging.Value == false || __instance.m_craftRecipe == null || __instance.m_craftRecipe.m_item == null || Player.m_localPlayer == null) { return; }

                Recipe recipe = __instance.m_craftRecipe;
                ItemDrop.ItemData upgradeItem = __instance.m_craftUpgradeItem;
                CraftingStation station = Player.m_localPlayer.GetCurrentCraftingStation();
                PendingCraft = new CraftContext {
                    Recipe = recipe,
                    PrefabName = recipe.m_item.gameObject.name,
                    TargetQuality = upgradeItem == null ? 1 : upgradeItem.m_quality + 1,
                    Multiplier = __instance.m_multiCrafting ? __instance.m_multiCraftAmount : 1,
                    IsUpgrade = upgradeItem != null,
                    AtUpgrader = station != null && station.m_upgrader,
                    Forgeable = IsForgeable(recipe.m_item.m_itemData.m_shared),
                    Station = station,
                    UpgradeItem = upgradeItem,
                    HadMasterwork = HasMark(upgradeItem, MasterworkKey),
                    HadLightweight = HasMark(upgradeItem, LightweightKey),
                    IdolFound = ForgingRefinement.FindUpgraderResource(recipe) != null,
                };
            }

            [HarmonyPostfix]
            private static void Postfix() {
                CraftContext craft = PendingCraft;
                PendingCraft = null;
                if (craft == null || Player.m_localPlayer == null) { return; }

                if (craft.AtUpgrader) {
                    // Vanilla removes the item being refined just before its roll, and every return that comes after
                    // that point still makes the roll - so the item being gone means the attempt was made.
                    craft.RefineAttempted = craft.IdolFound && craft.UpgradeItem != null && Player.m_localPlayer.GetInventory().ContainsItem(craft.UpgradeItem) == false;
                    craft.RefineSucceeded = craft.RefineAttempted && craft.Produced != null && craft.Produced.m_quality >= craft.TargetQuality;
                }
                Forging.GrantCraftXP(Player.m_localPlayer, craft);
                ShowGainedPerks(craft);
            }

            [HarmonyFinalizer]
            private static void Finalizer() {
                PendingCraft = null;
            }
        }

        private static void ShowGainedPerks(CraftContext craft) {
            if ((craft.GainedMasterwork || craft.GainedLightweight) == false || DamageText.instance == null) { return; }

            List<string> perks = new List<string>();
            if (craft.GainedMasterwork) { perks.Add(LocalizationManager.Instance.TryTranslate("$forging_masterwork")); }
            if (craft.GainedLightweight) { perks.Add(LocalizationManager.Instance.TryTranslate("$forging_lightweight")); }
            // Raised a little so it does not land on top of the crafting skill's own popups.
            Vector3 position = Player.m_localPlayer.transform.position + Vector3.up * 1.5f;
            DamageText.instance.ShowText(DamageText.TextType.Bonus, position, string.Join(", ", perks), true);
        }

        /// <summary>
        /// Marks the item DoCrafting creates. Matching on the recipe's prefab name keeps this off the ingredients a
        /// broken refinement hands back, which are added through the same overload during the same call. Crafting's
        /// quality prefix on this overload has already run, so the quality seen here is final.
        ///
        /// AddItem only returns the last item it creates, and Crafting's bonus crafts can ask it for more than one
        /// of an item that does not stack - so every new copy is found by comparing against what the inventory
        /// held beforehand, rather than trusting the return value.
        /// </summary>
        // string name, int stack, int quality, int variant, long crafterID, string crafterName, Vector2i position, bool cheated, bool pickedUp = false, bool dropIfFullInv = true
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new Type[] { typeof(string), typeof(int), typeof(int), typeof(int), typeof(long), typeof(string), typeof(Vector2i), typeof(bool), typeof(bool), typeof(bool) })]
        private static class ForgedCraftAddItemPatch
        {
            private static HashSet<ItemDrop.ItemData> ItemsBefore = null;

            [HarmonyPrefix]
            private static void Prefix(Inventory __instance, string name, ref int quality) {
                ItemsBefore = null;
                CraftContext craft = PendingCraft;
                if (craft == null || craft.AwaitingItem == false || name != craft.PrefabName) { return; }

                // A failed refinement drops the item a level, and vanilla does that to a quality 1 item too - leaving
                // it at quality 0. Only reachable once forging stops failures from destroying the item.
                if (craft.AtUpgrader && quality < 1) { quality = 1; }
                if (craft.Forgeable) { ItemsBefore = new HashSet<ItemDrop.ItemData>(__instance.GetAllItems()); }
            }

            [HarmonyPostfix]
            private static void Postfix(Inventory __instance, string name, ItemDrop.ItemData __result) {
                CraftContext craft = PendingCraft;
                HashSet<ItemDrop.ItemData> itemsBefore = ItemsBefore;
                ItemsBefore = null;
                if (craft == null || craft.AwaitingItem == false || name != craft.PrefabName) { return; }

                // One shot: anything else this craft adds is left alone.
                craft.AwaitingItem = false;
                craft.Produced = __result;
                if (__result == null || itemsBefore == null) { return; }

                bool masterwork = craft.HadMasterwork || QualifiesForMasterwork(Player.m_localPlayer);
                bool lightweight = craft.HadLightweight || QualifiesForLightweight(Player.m_localPlayer);
                if (masterwork == false && lightweight == false) { return; }

                foreach (ItemDrop.ItemData item in __instance.GetAllItems()) {
                    if (itemsBefore.Contains(item) || item.m_shared.m_name != __result.m_shared.m_name || item.m_customData == null) { continue; }
                    if (masterwork) { item.m_customData[MasterworkKey] = "1"; }
                    if (lightweight) { item.m_customData[LightweightKey] = "1"; }
                }
                craft.GainedMasterwork = masterwork && craft.HadMasterwork == false;
                craft.GainedLightweight = lightweight && craft.HadLightweight == false;
                // AddItem already totalled the inventory's weight, before the item was lightened.
                __instance.UpdateTotalWeight();
                Logger.LogDebug($"Forged {name} (masterwork: {masterwork}, lightweight: {lightweight}).");
            }
        }

        [HarmonyPatch(typeof(ItemDrop.ItemData))]
        private static class ForgedItemStatPatches
        {
            [HarmonyPostfix]
            [HarmonyPatch(nameof(ItemDrop.ItemData.GetDamage), new Type[] { typeof(int), typeof(float) })]
            private static void DamagePostfix(ItemDrop.ItemData __instance, ref HitData.DamageTypes __result) {
                if (__instance.m_customData == null || __instance.m_customData.Count == 0 || __instance.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield) { return; }
                float multiplier = StatMultiplier(__instance);
                if (multiplier != 1f) { __result.Modify(multiplier); }
            }

            [HarmonyPostfix]
            [HarmonyPatch(nameof(ItemDrop.ItemData.GetArmor), new Type[] { typeof(int), typeof(float) })]
            private static void ArmorPostfix(ItemDrop.ItemData __instance, ref float __result) {
                if (__instance.m_customData == null || __instance.m_customData.Count == 0) { return; }
                __result *= StatMultiplier(__instance);
            }

            // Shields only - a weapon's block power is left alone, masterwork already raises its damage.
            [HarmonyPostfix]
            [HarmonyPatch(nameof(ItemDrop.ItemData.GetBaseBlockPower), new Type[] { typeof(int) })]
            private static void BlockPowerPostfix(ItemDrop.ItemData __instance, ref float __result) {
                if (__instance.m_customData == null || __instance.m_customData.Count == 0 || __instance.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Shield) { return; }
                __result *= StatMultiplier(__instance);
            }

            [HarmonyPostfix]
            [HarmonyPatch(nameof(ItemDrop.ItemData.GetWeight))]
            private static void WeightPostfix(ItemDrop.ItemData __instance, ref float __result) {
                if (__instance.m_customData == null || __instance.m_customData.Count == 0 || LightweightActive(__instance) == false) { return; }
                __result *= 1f - ValConfig.LightweightWeightReduction.Value;
            }

            [HarmonyPostfix]
            [HarmonyPatch(nameof(ItemDrop.ItemData.GetNonStackedWeight))]
            private static void NonStackedWeightPostfix(ItemDrop.ItemData __instance, ref float __result) {
                if (__instance.m_customData == null || __instance.m_customData.Count == 0 || LightweightActive(__instance) == false) { return; }
                __result *= 1f - ValConfig.LightweightWeightReduction.Value;
            }
        }

        /// <summary>
        /// UpdateModifiers adds up m_movementModifier from every equipped item's shared data, which is common to all
        /// copies of that item, so the reduction is taken off the total afterwards instead.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.UpdateModifiers))]
        private static class LightweightMovementPatch
        {
            private static void Postfix(Player __instance) {
                if (MovementModifierIndex < 0 || __instance.m_equipmentModifierValues == null || MovementModifierIndex >= __instance.m_equipmentModifierValues.Length) { return; }
                if (ValConfig.EnableForging.Value == false || ValConfig.EnableLightweight.Value == false) { return; }

                float refund = MovementPenaltyRefund(__instance.m_rightItem)
                    + MovementPenaltyRefund(__instance.m_leftItem)
                    + MovementPenaltyRefund(__instance.m_chestItem)
                    + MovementPenaltyRefund(__instance.m_legItem)
                    + MovementPenaltyRefund(__instance.m_helmetItem)
                    + MovementPenaltyRefund(__instance.m_shoulderItem)
                    + MovementPenaltyRefund(__instance.m_utilityItem)
                    + MovementPenaltyRefund(__instance.m_trinketItem);
                __instance.m_equipmentModifierValues[MovementModifierIndex] += refund;
            }
        }

        /// <summary>
        /// Lists a forged item's perks in its tooltip. Damage, armor, block power and weight already show their
        /// improved values through the patches above; the movement speed line reads the shared data and does not,
        /// so the reduced penalty is spelled out here.
        /// </summary>
        // ItemData item, int qualityLevel, bool crafting, float worldLevel, int stackOverride = -1, bool appending = false
        [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip), new Type[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool) })]
        private static class ForgedItemTooltipPatch
        {
            private static void Postfix(ItemDrop.ItemData item, ref string __result) {
                if (item == null || item.m_customData == null || item.m_customData.Count == 0) { return; }

                bool masterwork = MasterworkActive(item);
                bool lightweight = LightweightActive(item);
                if (masterwork == false && lightweight == false) { return; }

                StringBuilder tooltip = new StringBuilder(__result);
                tooltip.Append('\n');
                if (masterwork) {
                    tooltip.Append($"\n<color=#E8B04A>{Translate("$forging_masterwork")}</color>: {StatBonusText(item, ValConfig.MasterworkStatBonus.Value)}");
                }
                if (lightweight) {
                    List<string> perks = new List<string>();
                    perks.Add(Translate("$forging_bonus_weight").Replace("{0}", Percent(ValConfig.LightweightWeightReduction.Value)));
                    float movement = item.m_shared.m_movementModifier;
                    if (movement < 0f) {
                        float reduced = movement * (1f - ValConfig.LightweightMovementPenaltyReduction.Value);
                        perks.Add(Translate("$forging_bonus_movement").Replace("{0}", (reduced * 100f).ToString("+0.#;-0.#;0")));
                    } else {
                        perks.Add(StatBonusText(item, ValConfig.LightweightNoPenaltyStatBonus.Value));
                    }
                    tooltip.Append($"\n<color=#9FD3E8>{Translate("$forging_lightweight")}</color>: {string.Join(", ", perks)}");
                }
                __result = tooltip.ToString();
            }

            private static string StatBonusText(ItemDrop.ItemData item, float bonus) {
                string token;
                switch (item.m_shared.m_itemType) {
                    case ItemDrop.ItemData.ItemType.Shield:
                        token = "$forging_bonus_block";
                        break;
                    case ItemDrop.ItemData.ItemType.Helmet:
                    case ItemDrop.ItemData.ItemType.Chest:
                    case ItemDrop.ItemData.ItemType.Legs:
                    case ItemDrop.ItemData.ItemType.Shoulder:
                        token = "$forging_bonus_armor";
                        break;
                    default:
                        token = "$forging_bonus_damage";
                        break;
                }
                return Translate(token).Replace("{0}", Percent(bonus));
            }
        }

        /// <summary>
        /// The recipe panel describes the item from its prefab, which never carries any marks, so name the perks the
        /// craft will produce next to the recipe title the way Crafting shows an earned quality.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateRecipe))]
        private static class ForgedPerkPreviewPatch
        {
            private static void Postfix(InventoryGui __instance, Player player) {
                if (ValConfig.EnableForging.Value == false || player == null) { return; }

                Recipe recipe = __instance.m_selectedRecipe.Recipe;
                if (recipe == null || recipe.m_item == null || IsForgeable(recipe.m_item.m_itemData.m_shared) == false) { return; }

                ItemDrop.ItemData upgradeItem = __instance.m_selectedRecipe.ItemData;
                bool masterwork = QualifiesForMasterwork(player) || MasterworkActive(upgradeItem);
                bool lightweight = QualifiesForLightweight(player) || LightweightActive(upgradeItem);
                if (masterwork == false && lightweight == false) { return; }

                List<string> perks = new List<string>();
                if (masterwork) { perks.Add(Translate("$forging_masterwork")); }
                if (lightweight) { perks.Add(Translate("$forging_lightweight")); }
                __instance.m_recipeName.text = $"{__instance.m_recipeName.text} <color=#E8B04A>({string.Join(", ", perks)})</color>";
            }
        }

        private static string Translate(string token) {
            return LocalizationManager.Instance.TryTranslate(token);
        }

        private static string Percent(float value) {
            return (value * 100f).ToString("0.#");
        }
    }
}
