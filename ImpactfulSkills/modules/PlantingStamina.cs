using HarmonyLib;
using ImpactfulSkills.common;
using System;
using UnityEngine;

namespace ImpactfulSkills.patches
{
    /// <summary>
    /// Farming pays for the cultivator's stamina.
    ///
    /// Vanilla only ever hands back half of a placement's cost - GetBuildStamina subtracts
    /// attackStamina * 0.5 * skillFactor whenever the held piece table has a skill, and the
    /// cultivator's is Farming - so even a maxed farmer paid for every seed. PlantingCostStaminaReduction
    /// stands in for that 0.5, which at the default of 1 means planting costs full price at Farming 0
    /// and nothing at all at Farming 100.
    ///
    /// Multiplant asks this same method what one plant costs, so a grid of seeds costs exactly what
    /// placing those seeds one at a time would have. Before, its extra plants were priced off a
    /// hardcoded 10 stamina - double the cultivator's real 5 - which left multi-planting more
    /// expensive per crop than vanilla planting no matter how high Farming climbed.
    /// </summary>
    public static class PlantingStamina
    {
        /// <summary>The reduction the game applies on its own, and the one our factor replaces.</summary>
        private const float VanillaReduction = 0.5f;

        private static readonly Func<Player, float> BuildStaminaOf = BindBuildStamina();

        private static Func<Player, float> BindBuildStamina() {
            try {
                return AccessTools.MethodDelegate<Func<Player, float>>(AccessTools.Method(typeof(Player), "GetBuildStamina"));
            }
            catch (Exception ex) {
                Logger.LogWarning($"Unable to bind Player.GetBuildStamina, planting stamina costs will be estimated instead: {ex.Message}");
                return null;
            }
        }

        /// <summary>Is the player holding a tool whose pieces are governed by Farming (the cultivator)?</summary>
        internal static bool PlacementIsFarming(Player player) {
            if (player == null) { return false; }
            PieceTable buildTool = player.GetBuildTool();
            return buildTool != null && buildTool.m_skill == Skills.SkillType.Farming;
        }

        /// <summary>
        /// Reduction actually in force. Clamped to vanilla's own 0.5 so a low config value can never
        /// make planting more expensive than it is without the mod.
        /// </summary>
        private static float Reduction() {
            return Mathf.Clamp(ValConfig.PlantingCostStaminaReduction.Value, VanillaReduction, 1f);
        }

        /// <summary>
        /// What a single plant costs right now, this patch included. Read through the game's own method
        /// rather than recomputing it, so equipment and food stamina modifiers - and any other mod's
        /// patches - count exactly as they will for the plant vanilla charges for itself. The fallback
        /// repeats GetBuildStamina's own arithmetic, which loses only other mods' adjustments.
        /// </summary>
        internal static float PerPlantCost(Player player) {
            if (player == null) { return 0f; }
            if (BuildStaminaOf != null) {
                return Mathf.Max(0f, BuildStaminaOf(player));
            }

            ItemDrop.ItemData tool = player.GetRightItem();
            if (tool == null) { return 0f; }
            float fullCost = tool.m_shared.m_attack.m_attackStamina * (1f + player.GetEquipmentHomeItemModifier());
            player.GetSEMan().ModifyHomeItemStaminaUsage(fullCost, ref fullCost);
            return Mathf.Max(0f, fullCost * (1f - Reduction() * player.GetSkillFactor(Skills.SkillType.Farming)));
        }

        [HarmonyPatch(typeof(Player), "GetBuildStamina")]
        public static class FarmingReducesPlantingCost
        {
            private static void Postfix(Player __instance, ref float __result) {
                if (__result <= 0f || !PlacementIsFarming(__instance)) { return; }

                float reduction = Reduction();
                if (reduction <= VanillaReduction) { return; }

                float skillFactor = __instance.GetSkillFactor(Skills.SkillType.Farming);
                // __result already has vanilla's half-reduction baked in; undo it before applying ours so
                // the two do not stack. The divisor bottoms out at 0.5 (skillFactor is clamped to 1), so
                // it can never divide by zero or flip the sign.
                float fullCost = __result / (1f - VanillaReduction * skillFactor);
                __result = Mathf.Max(0f, fullCost * (1f - reduction * skillFactor));
            }
        }
    }
}
