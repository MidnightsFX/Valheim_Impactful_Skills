using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;
using static Player;

namespace ImpactfulSkills.patches
{
    public static  class Cooking
    {

        [HarmonyPatch(typeof(Player))]
        public static class CookEnjoysFoodLongerPatch
        {
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(Player.UpdateFood))]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
            {
                var codeMatcher = new CodeMatcher(instructions);
                codeMatcher.MatchStartForward(
                    new CodeMatch(OpCodes.Div),
                    new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Mathf), nameof(Mathf.Clamp01)))
                    ).RemoveInstructions(2).InsertAndAdvance(
                    Transpilers.EmitDelegate(ClampFoodWithBonus)
                    ).ThrowIfNotMatch("Unable to patch Food degrading improvement.");

                return codeMatcher.Instructions();
            }

            public static float ClampFoodWithBonus(float food_time_remaining, float food_burn_time) {
                if (ValConfig.EnableCooking.Value == true && Player.m_localPlayer != null) {
                    float cooking_bonus = ValConfig.CookingBurnReduction.Value * Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Cooking);
                    return Mathf.Clamp01((food_time_remaining / food_burn_time) + cooking_bonus);
                }
                // fallback to the default modification for the method
                return Mathf.Clamp01(food_time_remaining / food_burn_time);
            }
        }
    }
}
