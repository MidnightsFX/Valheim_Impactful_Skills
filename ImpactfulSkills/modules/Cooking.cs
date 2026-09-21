using HarmonyLib;
using ImpactfulSkills.common;
using Jotunn.Managers;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;
using static Player;

namespace ImpactfulSkills.patches
{
    public static  class Cooking
    {
        // Cooking streak state. The two arming patches below mark the moment a food is actually produced, and the
        // Player.RaiseSkill prefix turns the vanilla xp grant for that moment into a streaked one. All of this is
        // local player only, the same way the other skill trackers in this mod keep their state.
        private static int CookingStreak = 0;
        private static float LastCookTime = 0f;
        private static long StreakOwner = 0;
        private static float LastShownBonus = -1f;
        private static bool CraftInProgress = false;
        private static int PendingCraftFoods = 0;
        private static bool StationCollectInProgress = false;

        [HarmonyPatch(typeof(Player))]
        public static class CookEnjoysFoodLongerPatch
        {
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(Player.UpdateFood))]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
            {
                var codeMatcher = new CodeMatcher(instructions);
                if (codeMatcher.TryMatchStartForward("Unable to patch Food degrading improvement.",
                    new CodeMatch(OpCodes.Div),
                    new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Mathf), nameof(Mathf.Clamp01)))
                )) {
                    codeMatcher.RemoveInstructions(2).InsertAndAdvance(
                        Transpilers.EmitDelegate(ClampFoodWithBonus)
                    );
                }

                return codeMatcher.Instructions();
            }

            public static float ClampFoodWithBonus(float food_time_remaining, float food_burn_time) {
                if (ValConfig.EnableCooking.Value == true && ValConfig.EnableCookingDegradeReduction.Value && Player.m_localPlayer != null) {
                    float cooking_bonus = ValConfig.CookingBurnReduction.Value * Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Cooking);
                    return Mathf.Clamp01((food_time_remaining / food_burn_time) + cooking_bonus);
                }
                // fallback to the default modification for the method
                return Mathf.Clamp01(food_time_remaining / food_burn_time);
            }
        }

        /// <summary>
        /// Arms the streak for cauldron style crafts. Vanilla only raises cooking inside DoCrafting once the item
        /// has actually been added to the inventory, so leaning on that grant rather than counting crafts here means
        /// a craft that failed - a full inventory, a missing dlc - never builds a streak. This is the same
        /// arm/disarm shape as Crafting.CraftedItemQualityPatch, and for the same reason: no IL assumptions.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
        public static class CookingStreakCraftPatch
        {
            [HarmonyPrefix]
            private static void Prefix(InventoryGui __instance) {
                CraftInProgress = false;
                PendingCraftFoods = 0;
                if (StreakEnabled() == false) { return; }

                Recipe recipe = __instance.m_craftRecipe;
                if (recipe == null || recipe.m_craftingStation == null || recipe.m_craftingStation.m_craftingSkill != Skills.SkillType.Cooking) { return; }

                // The same amount vanilla is about to hand to RaiseSkill, so a multicraft of ten counts as ten foods.
                PendingCraftFoods = Mathf.Max(1, __instance.m_multiCrafting ? __instance.m_multiCraftAmount : 1);
                CraftInProgress = true;
            }

            [HarmonyPostfix]
            private static void Postfix() {
                // Still armed means DoCrafting returned before anything was crafted.
                CraftInProgress = false;
                PendingCraftFoods = 0;
            }
        }

        /// <summary>
        /// Arms the streak for taking cooked food off a cooking station or oven. Placing raw food on one still earns
        /// its vanilla xp but neither builds nor breaks the streak.
        /// </summary>
        [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.OnInteract))]
        public static class CookingStreakStationPatch
        {
            [HarmonyPrefix]
            private static void Prefix(CookingStation __instance) {
                StationCollectInProgress = StreakEnabled() && CollectsCookedFood(__instance);
            }

            [HarmonyPostfix]
            private static void Postfix() {
                StationCollectInProgress = false;
            }
        }

        private static bool StreakEnabled() {
            return ValConfig.EnableCooking.Value && ValConfig.EnableCookingStreak.Value && Player.m_localPlayer != null;
        }

        /// <summary>
        /// Whether this interaction is about to hand over something cooked rather than something burnt.
        /// RPC_RemoveDoneItem always collects the first done slot, so that is the only slot worth looking at, and a
        /// lump of coal should not build a cooking streak.
        /// </summary>
        private static bool CollectsCookedFood(CookingStation station) {
            if (station.m_slots == null) { return false; }
            for (int slot = 0; slot < station.m_slots.Length; slot++) {
                station.GetSlot(slot, out string itemName, out float _, out CookingStation.Status _, out bool _);
                if (itemName == "" || !station.IsItemDone(itemName)) { continue; }
                return station.m_overCookedItem == null || itemName != station.m_overCookedItem.name;
            }
            return false;
        }

        /// <summary>
        /// Turns the vanilla cooking xp for a food that was just produced into a streaked amount. Each food cooked
        /// makes the next one worth more, up to the configured cap, and the streak only ends after going long enough
        /// without cooking anything - gathering ingredients or building in between does not break it.
        ///
        /// This runs before Skills.RaiseSkill, so the bonus is a true percentage of what the cook was worth and the
        /// configured cooking gain rate still applies on top of it.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.RaiseSkill))]
        public static class CookingStreakXPPatch
        {
            [HarmonyPrefix]
            private static void Prefix(Player __instance, Skills.SkillType skill, ref float value) {
                if (skill != Skills.SkillType.Cooking || (!CraftInProgress && !StationCollectInProgress)) { return; }

                // One shot. Whatever else raises cooking during this craft or interaction is not the food we armed for.
                int foods = CraftInProgress ? PendingCraftFoods : 1;
                CraftInProgress = false;
                StationCollectInProgress = false;

                if (StreakEnabled() == false || __instance != Player.m_localPlayer) { return; }

                float timeout = ValConfig.CookingStreakTimeout.Value;
                // Time.time rather than realtime, so a streak does not quietly expire behind a pause menu.
                if (timeout > 0f && Time.time - LastCookTime > timeout) { ResetStreak(); }
                long owner = __instance.GetPlayerID();
                if (owner != StreakOwner) {
                    ResetStreak();
                    StreakOwner = owner;
                }

                float bonus_per_food = ValConfig.CookingStreakBonusPerFood.Value;
                float max_bonus = ValConfig.CookingStreakMaxBonus.Value;
                float per_food_xp = value / foods;
                float bonus = 0f;
                float streak_bonus = 0f;
                for (int i = 0; i < foods; i++) {
                    CookingStreak++;
                    // The first food of a streak is worth its plain xp, the food after it is the one that benefits.
                    streak_bonus = Mathf.Min((CookingStreak - 1) * bonus_per_food, max_bonus);
                    bonus += per_food_xp * streak_bonus;
                }
                LastCookTime = Time.time;

                Logger.LogDebug($"Cooking streak {CookingStreak} ({foods} food) adds {bonus} xp to {value}.");
                value += bonus;
                ShowStreakBonus(streak_bonus);
            }
        }

        private static void ResetStreak() {
            CookingStreak = 0;
            LastShownBonus = -1f;
        }

        /// <summary>
        /// Shows the streak bonus while it is still climbing, then goes quiet once it has capped out.
        /// </summary>
        private static void ShowStreakBonus(float streak_bonus) {
            if (ValConfig.CookingStreakShowText.Value == false || streak_bonus <= 0f || streak_bonus == LastShownBonus) { return; }
            if (Player.m_localPlayer == null || DamageText.instance == null) { return; }

            LastShownBonus = streak_bonus;
            string label = LocalizationManager.Instance.TryTranslate("$cooking_streak");
            Vector3 playerUpPos = Player.m_localPlayer.transform.position + Vector3.up;
            DamageText.instance.ShowText(DamageText.TextType.Bonus, playerUpPos, label.Replace("{0}", Mathf.RoundToInt(streak_bonus * 100f).ToString()), true);
        }

        /// <summary>
        /// Eating trains cooking a little, scaled by how much the food actually provides. EatFood only returns true
        /// when the food was really eaten, so vanillas own rule about not topping up a food until it is half spent
        /// keeps this from being farmed.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.EatFood))]
        public static class CookingEatXPPatch
        {
            [HarmonyPostfix]
            private static void Postfix(Player __instance, ItemDrop.ItemData item, bool __result) {
                if (__result == false || ValConfig.EnableCooking.Value == false || ValConfig.EnableCookingEatXP.Value == false) { return; }
                if (__instance != Player.m_localPlayer || item == null) { return; }

                ItemDrop.ItemData.SharedData food = item.m_shared;
                float xp = (food.m_food + food.m_foodStamina + food.m_foodEitr) * ValConfig.CookingEatXPPerFoodStat.Value;
                if (xp <= 0f) { return; }

                Logger.LogDebug($"Eating {food.m_name} grants {xp} cooking xp.");
                // Not armed, so the streak prefix above leaves this grant alone.
                __instance.RaiseSkill(Skills.SkillType.Cooking, xp);
            }
        }
    }
}
