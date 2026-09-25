using HarmonyLib;
using System.Collections.Generic;

namespace ImpactfulSkills.patches
{
    /// <summary>
    /// Takes this mod's own skills out of the skills panel while they are turned off, without touching the level the
    /// player has in them. The check is made each time the panel is built, so a skill that the server turns back on
    /// shows up again the next time the panel is opened.
    ///
    /// A hidden skill is frozen rather than just unlisted: it gains no xp, so it cannot level up and announce a skill the
    /// player cannot see, and it loses nothing on death, so it comes back at the level it was hidden at.
    /// </summary>
    internal static class DisabledSkills
    {
        // Only set while SkillsDialog.Setup runs. GetSkillList also feeds SharedKnowledge and other mods' own logic,
        // which should keep seeing every skill the player has.
        private static bool buildingSkillsPanel = false;

        internal static bool IsHidden(Skills.SkillType skill) {
            if (skill == Skills.SkillType.None) { return false; }
            if (skill == AnimalWhisper.AnimalHandling) { return ValConfig.EnableAnimalWhisper.Value == false; }
            if (skill == Voyaging.VoyagingSkill) { return ValConfig.EnableVoyager.Value == false; }
            if (skill == Hauling.HaulingSkill) { return ValConfig.EnableHauling.Value == false; }
            if (skill == Forging.ForgingSkill) { return ValConfig.EnableForging.Value == false; }
            return false;
        }

        [HarmonyPatch(typeof(SkillsDialog), nameof(SkillsDialog.Setup))]
        private static class HideDisabledSkillsInPanel
        {
            private static void Prefix() { buildingSkillsPanel = true; }

            // A finalizer, so a throw part way through the panel cannot leave every later GetSkillList filtered.
            private static void Finalizer() { buildingSkillsPanel = false; }
        }

        [HarmonyPatch(typeof(Skills), nameof(Skills.GetSkillList))]
        private static class LeaveHiddenSkillsOffTheList
        {
            private static void Postfix(List<Skills.Skill> __result) {
                if (buildingSkillsPanel == false || __result == null) { return; }
                __result.RemoveAll(skill => skill?.m_info != null && IsHidden(skill.m_info.m_skill));
            }
        }

        /// <summary>
        /// The total shown at the bottom of the panel, which would otherwise still count the hidden levels.
        /// </summary>
        [HarmonyPatch(typeof(Skills), nameof(Skills.GetTotalSkill))]
        private static class LeaveHiddenSkillsOutOfTheTotal
        {
            private static void Postfix(Skills __instance, ref float __result) {
                if (buildingSkillsPanel == false) { return; }
                foreach (KeyValuePair<Skills.SkillType, Skills.Skill> entry in __instance.m_skillData) {
                    if (IsHidden(entry.Key)) { __result -= entry.Value.m_level; }
                }
            }
        }

        /// <summary>
        /// Every skill gain passes through here (see SkillRates). Most of a disabled skill's xp sources already check its
        /// toggle, this also covers the ones that don't - honey harvests are gated on EnableBeeBonuses alone.
        /// </summary>
        [HarmonyPatch(typeof(Skills), nameof(Skills.RaiseSkill))]
        private static class HiddenSkillsGainNoXP
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(Skills.SkillType skillType) {
                return IsHidden(skillType) == false;
            }
        }

        private class FrozenSkill
        {
            public Skills.Skill Skill;
            public float Level;
            public float Accumulator;
        }

        [HarmonyPatch(typeof(Skills), nameof(Skills.LowerAllSkills))]
        private static class HiddenSkillsKeepTheirLevelOnDeath
        {
            private static void Prefix(Skills __instance, out List<FrozenSkill> __state) {
                __state = new List<FrozenSkill>();
                foreach (KeyValuePair<Skills.SkillType, Skills.Skill> entry in __instance.m_skillData) {
                    if (IsHidden(entry.Key) == false) { continue; }
                    __state.Add(new FrozenSkill { Skill = entry.Value, Level = entry.Value.m_level, Accumulator = entry.Value.m_accumulator });
                }
            }

            private static void Postfix(List<FrozenSkill> __state) {
                foreach (FrozenSkill frozen in __state) {
                    frozen.Skill.m_level = frozen.Level;
                    frozen.Skill.m_accumulator = frozen.Accumulator;
                }
            }
        }
    }
}
