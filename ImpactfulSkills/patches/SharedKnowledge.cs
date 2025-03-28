using HarmonyLib;
using UnityEngine;

namespace ImpactfulSkills.patches
{
    public static class SharedKnowledge {
        private static float highest_skill_level = 0;
        private static float highest_skill_factor = 0;
        private static float time_since_start = 0;
        private static float last_skill_level_check = 0;


        [HarmonyPatch(typeof(Player), nameof(Player.RaiseSkill))]
        public static class PatchSkillIncreaseHigherGainsForLowerSkills {

            private static void Prefix(Skills.SkillType skill,ref float value) {
                if (ValConfig.EnableKnowledgeSharing.Value != true || Player.m_localPlayer == null) { return; }
                time_since_start += Time.deltaTime;
                // Set the current highest skill
                if (time_since_start > last_skill_level_check || highest_skill_level == 0) {
                    highest_skill_level = UpdateHighestSkillLevel(Player.m_localPlayer);
                    last_skill_level_check = time_since_start + (Time.deltaTime * 100);
                    Logger.LogDebug($"Setting highest skill level {highest_skill_level} factor {highest_skill_factor}");
                }
                float skill_level = Player.m_localPlayer.GetSkillLevel(skill);
                //Logger.LogDebug($"Comparing skill levels {skill_level} < {highest_skill_level} {skill.ToString()}");
                if (skill_level < highest_skill_level) {
                    float bonus_xp_curved = Mathf.Lerp(0, highest_skill_level, highest_skill_factor) / 100f;
                    float skill_bonus = ValConfig.SharedKnowledgeSkillBonusRate.Value * bonus_xp_curved;
                    //Logger.LogDebug($"Skill factors {highest_skill_level} <= {skill_level} + {ValConfig.SharedKnowledgeCap.Value} for bonus ({bonus_xp_curved}) {skill_bonus}");
                    if (highest_skill_level <= (skill_level + ValConfig.SharedKnowledgeCap.Value)) { skill_bonus = 0f; }
                    Logger.LogDebug($"Bonus skill gain from Knowledge {skill_bonus} for {skill.ToString()}");
                    value = +skill_bonus;
                }
            }
        }

        private static float UpdateHighestSkillLevel(Player player) {
            float high_skill_level = 0;
            foreach (var pskill in player.GetSkills().GetSkillList()) {
                if (pskill == null) { continue; }
                // Logger.LogDebug($"Checking skill {pskill.m_info.m_skill} {pskill.m_level} > {high_skill_level}");
                if (pskill.m_level > high_skill_level) {
                    high_skill_level = pskill.m_level;
                    highest_skill_factor = player.GetSkillFactor(pskill.m_info.m_skill);
                }
            }
            return high_skill_level;
        }



    }
}
