using HarmonyLib;
using ImpactfulSkills.modules;
using System;
using System.Collections.Generic;
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
            List<Skills.SkillType> tunallowed = new List<Skills.SkillType>() { };
            bool add_info_about_invalid_enum = false;
            if (ValConfig.SharedKnowledgeIgnoreList.Value != "")
            {
                foreach (var item in ValConfig.SharedKnowledgeIgnoreList.Value.Split(','))
                {
                    if (item.Trim().Length == 0) { continue; }
                    Logger.LogDebug($"Checking {item} as skill enum");

                    // Covers vanilla skills as well as skills added by other mods, whether or not this character has
                    // ever raised them, which is why this no longer looks at the players own skill list.
                    if (SkillRates.TryResolveSkill(item, out Skills.SkillType skill))
                    {
                        if (!tunallowed.Contains(skill)) { tunallowed.Add(skill); }
                        continue;
                    }
                    add_info_about_invalid_enum = true;
                }
            }

            skill_types_to_avoid_shared_xp.Clear();
            skill_types_to_avoid_shared_xp.AddRange(tunallowed);
            if (add_info_about_invalid_enum == true)
            {
                Logger.LogWarning($"Some of the skills you provided in the config are not valid skill types. Invalid skill types will be ignored. A comma seperated of valid skill names is recommended.");
                Logger.LogWarning($"Valid skill types are: {string.Join(", ", Skills.s_allSkills)}, plus the name or identifier of any skill added by another mod.");
            }
            Logger.LogDebug($"Unallowed shared xp skills: {string.Join(", ", skill_types_to_avoid_shared_xp)}");
        }


        // Runs after the skill gain rate multiplier, so the catch up bonus is an absolute amount rather than something
        // the configured rate scales up as well. A gain that has already been zeroed (a skill rate of 0) gets no bonus,
        // otherwise the bonus alone would keep raising a skill that was configured to stop.
        [HarmonyPatch(typeof(Skills), nameof(Skills.RaiseSkill))]
        public static class PatchSkillIncreaseHigherGainsForLowerSkills
        {
            [HarmonyPriority(Priority.LowerThanNormal)]
            private static void Prefix(Skills __instance, Skills.SkillType skillType, ref float factor)
            {
                time_since_start += Time.deltaTime;
                if (factor <= 0f) { return; }
                if (ValConfig.EnableKnowledgeSharing.Value == true && Player.m_localPlayer != null
                    && __instance.m_player == Player.m_localPlayer && !skill_types_to_avoid_shared_xp.Contains(skillType))
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
                    float skill_level = Player.m_localPlayer.GetSkillLevel(skillType);
                    //Logger.LogDebug($"Comparing skill levels {skill_level} < {highest_skill_level} {skillType.ToString()}");
                    if (skill_level < highest_skill_level)
                    {
                        float bonus_xp_curved = Mathf.Lerp(0, highest_skill_level, highest_skill_factor) / 100f;
                        float skill_bonus = ValConfig.SharedKnowledgeSkillBonusRate.Value * bonus_xp_curved;
                        //Logger.LogDebug($"Skill factors {highest_skill_level} <= {skill_level} + {ValConfig.SharedKnowledgeCap.Value} for bonus ({bonus_xp_curved}) {skill_bonus}");
                        if (highest_skill_level <= (skill_level + ValConfig.SharedKnowledgeCap.Value)) { skill_bonus = 0f; }
                        Logger.LogDebug($"Bonus skill gain from Knowledge {skill_bonus} for {skillType.ToString()}");
                        factor += skill_bonus;
                    }
                }
                // Logger.LogDebug($"{skillType.ToString()} increase value {factor}");
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
