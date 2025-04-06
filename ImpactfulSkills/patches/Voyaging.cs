using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Managers;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace ImpactfulSkills.patches
{
    public static class Voyaging
    {
        public static Skills.SkillType VoyagingSkill = 0;
        private static float update_timer = 0f;
        private static float current_update_time = 0f;
        public static void SetupSailingSkill()
        {
            SkillConfig voyage = new SkillConfig();
            voyage.Name = "$skill_Voyager";
            voyage.Description = "$skill_Voyager_description";
            voyage.Icon = ImpactfulSkills.EmbeddedResourceBundle.LoadAsset<Sprite>("Assets/Custom/Icons/skill_icons/voyager.png");
            voyage.Identifier = "midnightsfx.voyager";
            voyage.IncreaseStep = 0.1f;
            VoyagingSkill = SkillManager.Instance.AddSkill(voyage);
        }

        [HarmonyPatch(typeof(Ship), nameof(Ship.GetSailForce))]
        public static class VoyagerSpeedPatch
        {
            private static void Postfix(ref Vector3 __result, float dt) {
                if (ValConfig.EnableVoyager.Value != true || Player.m_localPlayer == null) { return; }

                current_update_time += dt;
                // Logger.LogInfo($"Checking to raise voyager: {update_timer} <= {current_update_time}");
                if (Player.m_localPlayer.IsAttachedToShip() && update_timer <= current_update_time) {
                    Logger.LogDebug($"Raising player voyager skill: {update_timer} <= {current_update_time}");
                    update_timer += (dt * ValConfig.VoyagerSkillXPCheckFrequency.Value);
                    Player.m_localPlayer.RaiseSkill(VoyagingSkill, (ValConfig.VoyagerSkillGainRate.Value * 1f));
                }
                float player_skill = Player.m_localPlayer.GetSkillFactor(VoyagingSkill);
                if (player_skill > 0f) {
                    float bonus = 1f + (player_skill * ValConfig.VoyagerSailingSpeedFactor.Value);
                    // Logger.LogDebug($"Increasing player sailspeed: {bonus}");
                    __result *= bonus;
                }
            }
        }

        [HarmonyPatch(typeof(Ship))]
        public static class PaddlingIsFasterPatch
        {
            //[HarmonyDebug]
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(Ship.CustomFixedUpdate))]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
            {
                var codeMatcher = new CodeMatcher(instructions);
                codeMatcher.MatchStartForward(
                    new CodeMatch(OpCodes.Ldloc_S),
                    new CodeMatch(OpCodes.Ldarg_1),
                    new CodeMatch(OpCodes.Call), // , AccessTools.DeclaredPropertyGetter(typeof(Vector3), "op_multiply")
                    new CodeMatch(OpCodes.Ldloc_S),
                    new CodeMatch(OpCodes.Ldc_I4_2),
                    new CodeMatch(OpCodes.Callvirt), // , AccessTools.Method(typeof(Rigidbody), nameof(Rigidbody.AddForceAtPosition))
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Ldarg_1),
                    new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Ship), nameof(Ship.ApplyEdgeForce)))
                    ).Advance(1).InsertAndAdvance(
                    Transpilers.EmitDelegate(PaddleSpeedImprovement)
                    )
                    .ThrowIfNotMatch("Unable to patch ship paddle speed improvement.");

                return codeMatcher.Instructions();
            }

            public static Vector3 PaddleSpeedImprovement(Vector3 ship_motion) {
                if (ValConfig.EnableVoyager.Value == true && Player.m_localPlayer != null) {
                    float player_skill = Player.m_localPlayer.GetSkillLevel(VoyagingSkill);
                    if (player_skill >= ValConfig.VoyagerPaddleSpeedBonusLevel.Value) {
                        Vector3 ship_modified_motion = ship_motion * (ValConfig.VoyagerPaddleSpeedBonus.Value * (1 + (player_skill/100)));
                        //Logger.LogDebug($"Improving ship paddle speed: {ship_motion} -> {ship_modified_motion}");
                        return ship_modified_motion;
                    }
                }
                // fallback to the default modification for the method
                return ship_motion;
            }
        }


        [HarmonyPatch(typeof(Ship), nameof(Ship.GetWindAngleFactor))]
        public static class VoyagerAnglePatch
        {
            private static void Postfix(ref float __result)
            {
                if (ValConfig.EnableVoyager.Value != true || Player.m_localPlayer == null) { return; }

                    float player_skill = Player.m_localPlayer.GetSkillLevel(VoyagingSkill);
                    if (player_skill >= ValConfig.VoyagerReduceCuttingStart.Value) {
                    // Reduce the penalty of not having the wind at your back
                    if (__result < 1f) {
                        float max_skill_increase = player_skill * 0.02f;
                        float sailingAngleFactor = Mathf.Clamp((__result + max_skill_increase), __result, 1f);
                        // Logger.LogDebug($"Improving sail angle due to skill: ({__result}) vs {sailingAngleFactor}");
                        __result = sailingAngleFactor;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Minimap), nameof(Minimap.Explore), typeof(Vector3), typeof(float))]
        private class VoyagerNotSoBlindWhileSailingPatch {
            private static void Prefix(ref float radius) {
                if (ValConfig.EnableVoyager.Value == true && Player.m_localPlayer != null && Player.m_localPlayer.m_attachedToShip == true) {
                    radius *= (Player.m_localPlayer.GetSkillFactor(VoyagingSkill) * ValConfig.VoyagerIncreaseExplorationRadius.Value) + 1f;
                }
            }
        }
    }
}
