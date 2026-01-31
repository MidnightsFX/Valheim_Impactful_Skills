using HarmonyLib;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Skills;

namespace ImpactfulSkills.patches
{
    public static class SharedKnowledge
    {
        private static float highest_skill_level = 0;
        private static float highest_skill_factor = 0;
        private static float time_since_start = 0;
        private static float last_skill_level_check = 0;
        private static bool setup_avoid_skills = false;
        private static List<Skills.SkillType> skill_types_to_avoid_shared_xp = new List<Skills.SkillType> { };

        public static void UnallowedSharedXPSkillTypesChanged(object s, EventArgs e)
        {
            SetupUnallowedSharedXPSkills();
        }

        public static void SetupUnallowedSharedXPSkills()
        {
            if (Player.m_localPlayer == null) { return; }
            List<Skills.SkillType> tunallowed = new List<Skills.SkillType>() { };
            List<Skills.SkillType> player_skills = Player.m_localPlayer.GetSkills().m_skillData.Keys.ToList();
            bool add_info_about_invalid_enum = false;
            if (ValConfig.SharedKnowledgeIgnoreList.Value != "")
            {
                foreach (var item in ValConfig.SharedKnowledgeIgnoreList.Value.Split(','))
                {
                    Logger.LogDebug($"Checking {item} as skill enum");

                    // Check Jotun for a registered skill, this covers all custom Jotunn skills
                    Skills.SkillDef sd_item = SkillManager.Instance.GetSkill(item);
                    if (sd_item != null) { tunallowed.Add(sd_item.m_skill); continue; }

                    // Mods which add skills using skill manager do not have a central location to check
                    // We are checking skills that the player already has, which means that we won't always get all skills here
                    // But without a central registry of skills, or skills adding their enums to the master list- it doesn't matter
                    try {
                        foreach(var pskill in player_skills)
                        {
                            if (pskill.ToString().Equals(item, StringComparison.OrdinalIgnoreCase))
                            {
                                if (!tunallowed.Contains(pskill))
                                {
                                    tunallowed.Add(pskill);
                                }
                                break;
                            }
                        }
                    } catch (Exception ex) {
                        Logger.LogError($"Error parsing {item} as skill enum: {ex}");
                    }
                }
            }

            if (tunallowed.Count > 0)
            {
                skill_types_to_avoid_shared_xp.Clear();
                skill_types_to_avoid_shared_xp.AddRange(tunallowed);
            }
            if (add_info_about_invalid_enum == true)
            {
                Logger.LogWarning($"Some of the skills you provided in the config are not valid skill types. Invalid skill types will be ignored. A comma seperated of valid skill names is recommended.");
                Logger.LogWarning($"Valid skill types are: {string.Join(", ", Skills.s_allSkills)}");
            }
            Logger.LogDebug($"Unallowed shared xp skills: {string.Join(", ", skill_types_to_avoid_shared_xp)}");
        }


        [HarmonyPatch(typeof(Player), nameof(Player.RaiseSkill))]
        public static class PatchSkillIncreaseHigherGainsForLowerSkills
        {

            private static void Prefix(Skills.SkillType skill, ref float value)
            {
                time_since_start += Time.deltaTime;
                if (ValConfig.EnableKnowledgeSharing.Value == true && Player.m_localPlayer != null && !skill_types_to_avoid_shared_xp.Contains(skill))
                {
                    // Set the current highest skill
                    if (time_since_start > last_skill_level_check || highest_skill_level == 0)
                    {
                        if (setup_avoid_skills == false)
                        {
                            SetupUnallowedSharedXPSkills();
                            setup_avoid_skills = true;
                        }
                        highest_skill_level = UpdateHighestSkillLevel(Player.m_localPlayer);
                        last_skill_level_check = time_since_start + (Time.deltaTime * 100);
                        Logger.LogDebug($"Setting highest skill level {highest_skill_level} factor {highest_skill_factor}");
                    }
                    float skill_level = Player.m_localPlayer.GetSkillLevel(skill);
                    //Logger.LogDebug($"Comparing skill levels {skill_level} < {highest_skill_level} {skill.ToString()}");
                    if (skill_level < highest_skill_level)
                    {
                        float bonus_xp_curved = Mathf.Lerp(0, highest_skill_level, highest_skill_factor) / 100f;
                        float skill_bonus = ValConfig.SharedKnowledgeSkillBonusRate.Value * bonus_xp_curved;
                        //Logger.LogDebug($"Skill factors {highest_skill_level} <= {skill_level} + {ValConfig.SharedKnowledgeCap.Value} for bonus ({bonus_xp_curved}) {skill_bonus}");
                        if (highest_skill_level <= (skill_level + ValConfig.SharedKnowledgeCap.Value)) { skill_bonus = 0f; }
                        Logger.LogDebug($"Bonus skill gain from Knowledge {skill_bonus} for {skill.ToString()}");
                        value += skill_bonus;
                    }
                }
                // Logger.LogDebug($"{skill.ToString()} increase value {value}");
            }
        }

        private static float UpdateHighestSkillLevel(Player player)
        {
            float high_skill_level = 0;
            foreach (var pskill in player.GetSkills().GetSkillList())
            {
                if (pskill == null) { continue; }
                // Logger.LogDebug($"Checking skill {pskill.m_info.m_skill} {pskill.m_level} > {high_skill_level}");
                if (pskill.m_level > high_skill_level)
                {
                    high_skill_level = pskill.m_level;
                    highest_skill_factor = player.GetSkillFactor(pskill.m_info.m_skill);
                }
            }
            return high_skill_level;
        }
    }
}
