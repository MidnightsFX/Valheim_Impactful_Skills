using BepInEx.Configuration;
using UnityEngine;

namespace ImpactfulSkills
{
    /// <summary>
    /// Skill reads for anything that pays out in proportion to a skill. Every such setting is described as its value
    /// at level 100, and by default these hold the skill to that.
    ///
    /// Vanilla clamps Skills.GetSkillFactor to 1, but other mods add to it afterwards. EpicLoot's Add*Skill effects
    /// add their bonus levels after that clamp, and socket stacked gear has reached +534 Crafting - a factor of about
    /// 6, which turned a 15% refund chance worth 30% of an ingredient into 91% for 182%, so every craft paid for
    /// itself. Character.GetSkillLevel passes 100 the same way, and status effects lift it even in vanilla.
    ///
    /// Each skill has a *BonusesPastLevel100 setting, off by default, that lets its bonuses keep growing with the
    /// boosted skill instead. Bonuses that reduce something still stop at a 100% reduction while it is on, so a
    /// boosted skill can make a cost free but never turn it negative.
    ///
    /// Level requirements always read the boosted level. A bonus that unlocks a feature sooner is what those effects
    /// are for; it is only the size of the payout that the setting decides.
    /// </summary>
    internal static class SkillCaps
    {
        /// <summary>GetSkillFactor, held to 0 - 1 unless pastLevel100 is on.</summary>
        public static float GetBonusSkillFactor(this Character character, Skills.SkillType skill, ConfigEntry<bool> pastLevel100) {
            return BonusFactor(character.GetSkillFactor(skill), pastLevel100);
        }

        /// <summary>GetSkillLevel, held to 0 - 100 unless pastLevel100 is on.</summary>
        public static float GetBonusSkillLevel(this Character character, Skills.SkillType skill, ConfigEntry<bool> pastLevel100) {
            return BonusLevel(character.GetSkillLevel(skill), pastLevel100);
        }

        /// <summary>
        /// A skill factor already read - often by vanilla, and handed over - held to 0 - 1 unless pastLevel100 is on.
        /// Where a patch undoes vanilla's own use of that factor, the undo must still use the factor vanilla used.
        /// </summary>
        public static float BonusFactor(float skillFactor, ConfigEntry<bool> pastLevel100) {
            return pastLevel100.Value ? Mathf.Max(0f, skillFactor) : Mathf.Clamp01(skillFactor);
        }

        /// <summary>A skill level already read for a level requirement, held to 0 - 100 unless pastLevel100 is on.</summary>
        public static float BonusLevel(float skillLevel, ConfigEntry<bool> pastLevel100) {
            return pastLevel100.Value ? Mathf.Max(0f, skillLevel) : Mathf.Clamp(skillLevel, 0f, 100f);
        }
    }
}
