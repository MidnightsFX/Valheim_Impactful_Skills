using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ImpactfulSkills.patches {
    internal class Hauling {
        public static Skills.SkillType HaulingSkill = 0;
        public static void SetupHaulingSkill() {
            SkillConfig hauling = new SkillConfig();
            hauling.Name = "$skill_Hauling";
            hauling.Description = "$skill_Hauling_description";
            hauling.Icon = ImpactfulSkills.EmbeddedResourceBundle.LoadAsset<Sprite>("Assets/Custom/Icons/skill_icons/hauling_skill.png");
            hauling.Identifier = "midnightsfx.hauling";
            hauling.IncreaseStep = 0.1f;
            HaulingSkill = SkillManager.Instance.AddSkill(hauling);
        }

        [HarmonyPatch(typeof(Player))]
        private static class PlayerCarryWeightPatch {
            [HarmonyPatch(nameof(Player.GetMaxCarryWeight))]
            private static void Postfix(Player __instance, ref float __result) {
                if (ValConfig.EnableHauling.Value == false || ValConfig.EnableCarryWeightBonus.Value == false) { return; }

                __result += (__instance.GetSkillFactor(HaulingSkill) * ValConfig.HaulingMaxWeightBonus.Value);
            }
        }

        [HarmonyPatch(typeof(Vagon))]
        private static class VagonMassPatch {
            [HarmonyPatch(nameof(Vagon.SetMass))]
            private static void Prefix(ref float mass) {
                // This is only called by the znet view owner of the cart
                if (ValConfig.EnableHauling.Value == false || ValConfig.EnableHaulingCartMassReduction.Value == false || Player.m_localPlayer == null) { return; }
                    
                mass *= (1 - (Player.m_localPlayer.GetSkillFactor(HaulingSkill) * ValConfig.HaulingCartMassReduction.Value));
            }
        }

        [HarmonyPatch(typeof(Vagon))]
        private static class VagonXPPatch {
            static Vector3 lastPosition = Vector3.zero;
            static float lastTimer = 0f;

            [HarmonyPatch(nameof(Vagon.LateUpdate))]
            private static void Postfix(Vagon __instance) {
                if (ValConfig.EnableHauling.Value == false || Player.m_localPlayer == null) { return; }

                // Only applies to the attached/local player
                if (__instance.IsAttached(Player.m_localPlayer)) {
                    if (lastPosition == Vector3.zero || lastTimer == 0) {
                        lastPosition = __instance.transform.position;
                        lastTimer = Time.realtimeSinceStartup;
                    }
                    //Logger.LogDebug($"Checking {Time.realtimeSinceStartup} > {lastTimer + 15f}");
                    if (Time.realtimeSinceStartup > lastTimer + ValConfig.HaulingXPCheckInterval.Value) {
                        lastTimer = Time.realtimeSinceStartup;
                        float distance = Vector3.Distance(lastPosition, __instance.transform.position);
                        // If you haven't moved far enough, don't update the last distance check
                        Logger.LogDebug($"Checking distanced traveled: {distance}");
                        if (distance > 1f) {
                            float totalmass = 0f;
                            foreach (var entry in __instance.m_bodies) {
                                totalmass += entry.mass;
                            }
                            Logger.LogDebug($"Raising hauling skill: {ValConfig.HaulingXPRate.Value * (totalmass * 0.3f)} = {totalmass} * 0.3 * {ValConfig.HaulingXPRate.Value}");
                            Player.m_localPlayer.RaiseSkill(HaulingSkill, ValConfig.HaulingXPRate.Value * (totalmass * 0.3f));
                            lastPosition = __instance.transform.position;
                        }
                    }
                }
            }
        }
    }
}
