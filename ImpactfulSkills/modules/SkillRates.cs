using BepInEx.Configuration;
using HarmonyLib;
using ImpactfulSkills.patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImpactfulSkills.modules {
    internal static class SkillRates {
        public class SkillRateConfig {
            public string SkillName { get; set; }
            public ConfigEntry<float> ConfigEntry { get; set; }
        }

        static Dictionary<Skills.SkillType, SkillRateConfig> SkillRateConfigs = new Dictionary<Skills.SkillType, SkillRateConfig>();

        internal static void SetupSkillRateConfigs() {
            foreach(Skills.SkillType skill in Enum.GetValues(typeof(Skills.SkillType))) {
                // Skip existing skills and our custom skills, since they are handled elsewhere
                if (SkillRateConfigs.ContainsKey(skill) || skill == Voyaging.VoyagingSkill || skill == Hauling.HaulingSkill || skill == AnimalWhisper.AnimalHandling || skill == Skills.SkillType.None) { continue; }

                string skillName = skill.ToString();
                ConfigEntry<float> configEntry = ValConfig.BindServerConfig("SkillRates", $"{skillName}SkillGainRate", 1f, $"How fast the {skillName} skill is gained.", false, 1f, 50f);
                SkillRateConfigs[skill] = new SkillRateConfig { SkillName = skillName, ConfigEntry = configEntry };
            }
        }


        [HarmonyPatch(typeof(Player), nameof(Player.RaiseSkill))]
        
        public static class PatchSkillIncreaseHigherGainsForLowerSkills {
            [HarmonyPrefix]
            private static void Prefix(Skills.SkillType skill, ref float value) {
                if (SkillRateConfigs.ContainsKey(skill)) {
                    value *= SkillRateConfigs[skill].ConfigEntry.Value;
                }
                // This is late binding setup, which is not super ideal, it would still read existing config entries
                //else {
                //    // Skill doesn't exist, add it
                //    SetupSkillRateConfigs();
                //}
            }
        }
    }
}
