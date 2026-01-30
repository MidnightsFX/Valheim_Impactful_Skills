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
        public static void SetupSailingSkill() {
            SkillConfig hauling = new SkillConfig();
            hauling.Name = "$skill_Hauling";
            hauling.Description = "$skill_Hauling_description";
            hauling.Icon = ImpactfulSkills.EmbeddedResourceBundle.LoadAsset<Sprite>("Assets/Custom/Icons/skill_icons/hauling.png");
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
    }
}
