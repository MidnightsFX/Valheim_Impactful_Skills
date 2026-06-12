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
        public static void SetupSailingSkill()
        {
            SkillConfig voyage = new SkillConfig();
            voyage.Name = "$skill_Voyager";
            voyage.Description = "$skill_Voyager_description";
            voyage.Icon = ImpactfulSkills.EmbeddedResourceBundle.LoadAsset<Sprite>("Assets/Custom/Icons/skill_icons/voyager.png");
            voyage.Identifier = "midnightsfx.voyager";
            voyage.IncreaseStep = 0.15f;
            VoyagingSkill = SkillManager.Instance.AddSkill(voyage);
        }

        [HarmonyPatch(typeof(Ship), nameof(Ship.GetSailForce))]
        public static class VoyagerSpeedPatch
        {
            private static void Postfix(Ship __instance, ref Vector3 __result) {
                if (ValConfig.EnableVoyager.Value != true || Player.m_localPlayer == null) { return; }

                // NOTE: XP gain is handled in VoyagerXPPatch (Ship.UpdateSail). GetSailForce only
                // runs on the boat's ZDO owner (the driver), so awarding XP here skipped passengers.
                float player_skill = Player.m_localPlayer.GetSkillFactor(VoyagingSkill);
                if (player_skill > 0f) {
                    float bonus = 1f + (player_skill * ValConfig.VoyagerSailingSpeedFactor.Value);
                    // Logger.LogDebug($"Increasing player sailspeed: {bonus}");
                    __result *= bonus;
                }
                if (ValConfig.EnableFriendsRowSpeedBonus.Value && __instance.m_players.Count > 1) {
                    float rowingbonus = 1;
                    foreach (Player friend in __instance.m_players) {
                        if (friend == Player.m_localPlayer) { continue; }
                        rowingbonus += friend.GetSkillFactor(VoyagingSkill) * ValConfig.MaxFriendsRowSpeedBonus.Value;
                    }
                    __result *= rowingbonus;
                }
            }
        }

        // Awards Voyager XP to whoever is locally aboard a moving boat. Hooks Ship.UpdateSail,
        // which Ship.CustomFixedUpdate runs on every client BEFORE its m_nview.IsOwner() return,
        // so passengers (not just the ZDO-owning driver) gain XP. Mirrors Hauling.VagonXPPatch:
        // a throttled position-delta check, which is reliable on non-owner clients (where
        // rigidbody velocity is not).
        [HarmonyPatch(typeof(Ship))]
        public static class VoyagerXPPatch {
            static Vector3 lastPosition = Vector3.zero;
            static float lastTimer = 0f;

            [HarmonyPatch("UpdateSail")]
            private static void Postfix(Ship __instance) {
                if (ValConfig.EnableVoyager.Value == false || Player.m_localPlayer == null) { return; }
                // Only the boat the LOCAL player is actually aboard (driver or passenger).
                if (!__instance.IsPlayerInBoat(Player.m_localPlayer)) { return; }

                if (lastPosition == Vector3.zero || lastTimer == 0) {
                    lastPosition = __instance.transform.position;
                    lastTimer = Time.realtimeSinceStartup;
                }
                if (Time.realtimeSinceStartup > lastTimer + ValConfig.VoyagerSkillXPCheckFrequency.Value) {
                    lastTimer = Time.realtimeSinceStartup;
                    float distance = Vector3.Distance(lastPosition, __instance.transform.position);
                    Logger.LogDebug($"Checking voyager distance traveled: {distance}");
                    // Threshold avoids idle bob/drift granting XP on an anchored boat.
                    if (distance > 2f) {
                        Logger.LogDebug($"Raising player voyager skill.");
                        Player.m_localPlayer.RaiseSkill(VoyagingSkill, (ValConfig.VoyagerSkillGainRate.Value * 1f));
                        lastPosition = __instance.transform.position;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(WearNTear))]
        public static class ShipDamageReduction {
            [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.RPC_Damage))]
            private static void Prefix(WearNTear __instance, ref HitData hit) {
                if (__instance.m_materialType != WearNTear.MaterialType.Wood || ValConfig.EnableBoatDamageReduction.Value == false) { return; }
                if (Player.m_localPlayer == null || Player.m_localPlayer.GetSkillLevel(VoyagingSkill) < ValConfig.BoatDamageReductionLevel.Value) {  return; }
                Ship ship = __instance.GetComponent<Ship>();
                if (ship == null || !ship.m_players.Contains(Player.m_localPlayer)) { return; }

                float player_skill = Player.m_localPlayer.GetSkillFactor(VoyagingSkill);
                float dmg_reduction = player_skill * ValConfig.VoyagerDamageReductionAmount.Value;
                Logger.LogDebug($"Reducing Ship damage by {dmg_reduction * 100}%");
                hit.m_damage.Modify(1 - dmg_reduction);
            }
        }

        [HarmonyPatch(typeof(ImpactEffect))]
        public static class ShipDamageImpactReduction {
            [HarmonyPatch(typeof(ImpactEffect), nameof(ImpactEffect.Awake))]
            private static void Postfix(ImpactEffect __instance) {
                if (Player.m_localPlayer == null || Player.m_localPlayer.GetSkillLevel(VoyagingSkill) < ValConfig.VoyagerImpactResistanceLevel.Value) {
                    return;
                }
                __instance.m_damageToSelf = false;
            }
        }

        //[HarmonyEmitIL("./dumps")]
        //[HarmonyDebug]
        [HarmonyPatch(typeof(Ship))]
        public static class PaddlingIsFasterPatch
        {
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(Ship.CustomFixedUpdate))]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
            {
                var codeMatcher = new CodeMatcher(instructions);
                codeMatcher.MatchStartForward(
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Ship), nameof(Ship.m_body))),
                    new CodeMatch(OpCodes.Ldloc_S),
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Ship), nameof(Ship.m_body))),
                    new CodeMatch(OpCodes.Callvirt),
                    new CodeMatch(OpCodes.Ldarg_1)
                    ).Advance(3).InsertAndAdvance(
                    Transpilers.EmitDelegate(PaddleSpeedImprovement)
                    ).ThrowIfNotMatch("Unable to patch paddle speed improvement."); ;
                return codeMatcher.Instructions();
            }

            public static Vector3 PaddleSpeedImprovement(Vector3 ship_motion) {
                if (ValConfig.EnableVoyager.Value == true && Player.m_localPlayer != null) {
                    float player_skill = Player.m_localPlayer.GetSkillLevel(VoyagingSkill);
                    if (player_skill >= ValConfig.VoyagerPaddleSpeedBonusLevel.Value) {
                        Vector3 ship_modified_motion = ship_motion * (1 + ValConfig.VoyagerPaddleSpeedBonus.Value * (player_skill / 100));
                        //Logger.LogInfo($"Improving ship paddle speed: {ship_motion} -> {ship_modified_motion}");
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
                if (ValConfig.EnableVoyager.Value == true && Player.m_localPlayer != null && Player.m_localPlayer.m_attachedToShip == true && Player.m_localPlayer.IsAttached()) {
                    radius *= (Player.m_localPlayer.GetSkillFactor(VoyagingSkill) * ValConfig.VoyagerIncreaseExplorationRadius.Value) + 1f;
                }
            }
        }
    }
}
