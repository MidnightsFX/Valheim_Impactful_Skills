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

        /// <summary>
        /// Grants hauling XP for travelling under a heavy load, so that carrying goods on foot is
        /// rewarded and not just carting them. Rather than running its own timer this piggybacks on
        /// the Run skill, which vanilla raises roughly once a second while actually running
        /// (Player.CheckRun). That inherits all of vanilla's conditions for free - moving, on the
        /// ground, not crouched, stamina remaining - so we only have to check distance and weight.
        /// Note that vanilla blocks running entirely while encumbered, so this stops paying out
        /// above 100% of your carry weight.
        /// </summary>
        [HarmonyPatch(typeof(Player))]
        private static class PlayerCarryWeightXPPatch {
            static Vector3 lastPosition = Vector3.zero;
            static bool hasLastPosition = false;
            static int runIncreases = 0;

            [HarmonyPatch(nameof(Player.RaiseSkill))]
            private static void Postfix(Player __instance, Skills.SkillType skill) {
                if (ValConfig.EnableHauling.Value == false || ValConfig.EnableHaulingCarryWeightXP.Value == false || Player.m_localPlayer == null) { return; }
                // Only counting run increases for the local player. This also stops the hauling
                // award below from recursing back into here, since it raises a different skill.
                if (skill != Skills.SkillType.Run || __instance != Player.m_localPlayer) { return; }

                if (hasLastPosition == false) {
                    lastPosition = __instance.transform.position;
                    hasLastPosition = true;
                    runIncreases = 0;
                    return;
                }

                runIncreases++;
                if (runIncreases < ValConfig.HaulingCarryWeightXPInterval.Value) { return; }
                runIncreases = 0;

                // If you haven't moved far enough, don't update the last position. That lets slow or
                // interrupted travel accumulate across checks instead of being discarded, and stops
                // running in circles from paying out.
                float distance = Vector3.Distance(lastPosition, __instance.transform.position);
                if (distance < ValConfig.HaulingCarryWeightXPMinDistance.Value) { return; }
                lastPosition = __instance.transform.position;

                // GetMaxCarryWeight already includes the hauling bonus applied by PlayerCarryWeightPatch
                // as well as any Megingjord effect, so this ratio matches the on-screen weight bar.
                float maxWeight = __instance.GetMaxCarryWeight();
                if (maxWeight <= 0f) { return; }
                float loadRatio = __instance.GetInventory().GetTotalWeight() / maxWeight;
                if (loadRatio < (ValConfig.HaulingCarryWeightXPThreshold.Value / 100f)) { return; }

                float xp = ValConfig.HaulingCarryWeightXPRate.Value * loadRatio;
                Logger.LogDebug($"Raising hauling skill from carried weight: {xp} = {loadRatio} load ratio * {ValConfig.HaulingCarryWeightXPRate.Value}");
                __instance.RaiseSkill(HaulingSkill, xp);
            }
        }
    }
}
