using HarmonyLib;
using Jotunn.Managers;
using System.Collections.Generic;

namespace ImpactfulSkills.patches
{
    /// <summary>
    /// Tells the local player in the middle of the screen when a skill level-up unlocks one of this mod's features,
    /// eg "Rockbreaker unlocked!". Features unlocked by the same level-up share one message, since a centre message
    /// replaces the one before it.
    ///
    /// The features are the perks in SkillPerks that have a level, each announced only while it is enabled. The checks
    /// read the boosted level (see SkillCaps), so a feature that gear or a status effect unlocks early is announced on
    /// the level-up that crosses it with that boost, and not again later.
    ///
    /// Only Skills.RaiseSkill is watched, which raises a skill at most one level per call. The raiseskill console
    /// command sets the level directly and announces nothing.
    /// </summary>
    internal static class UnlockMessages
    {
        [HarmonyPatch(typeof(Skills), nameof(Skills.RaiseSkill))]
        public static class AnnounceUnlocks
        {
            private static void Prefix(Skills __instance, Skills.SkillType skillType, out (float level, float factor)? __state) {
                __state = null;
                if (!ValConfig.EnableUnlockMessages.Value || !ValConfig.ShowUnlockMessages.Value) { return; }
                if (__instance.m_player == null || __instance.m_player != Player.m_localPlayer || !SkillPerks.BySkill.ContainsKey(skillType)) { return; }
                __state = Read(__instance.m_player, skillType);
            }

            private static void Postfix(Skills __instance, Skills.SkillType skillType, (float level, float factor)? __state) {
                if (__state == null || __instance.m_player == null) { return; }
                (float level, float factor) before = __state.Value;
                (float level, float factor) after = Read(__instance.m_player, skillType);
                if (after == before) { return; }

                List<string> lines = new();
                foreach (SkillPerks.Perk perk in SkillPerks.BySkill[skillType]) {
                    // Perks without a level work from level 0, so there is nothing to unlock.
                    if (perk.Level == null || !perk.Enabled() || perk.Reached(before.level, before.factor) || !perk.Reached(after.level, after.factor)) { continue; }
                    lines.Add(LocalizationManager.Instance.TryTranslate("$unlock_message").Replace("{0}", LocalizationManager.Instance.TryTranslate(perk.Name)));
                }
                if (lines.Count > 0) {
                    __instance.m_player.Message(MessageHud.MessageType.Center, string.Join("\n", lines));
                }
            }

            private static (float level, float factor) Read(Player player, Skills.SkillType skillType) {
                return (player.GetSkillLevel(skillType), player.GetSkillFactor(skillType));
            }
        }
    }
}
