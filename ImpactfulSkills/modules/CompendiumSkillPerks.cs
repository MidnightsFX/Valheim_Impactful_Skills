using HarmonyLib;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ImpactfulSkills.patches
{
    /// <summary>
    /// Adds a Skill Perks page to the compendium. It lists each skill this mod changes with the player's level in it,
    /// and under it every perk from SkillPerks with a short explanation, the ones not reached yet greyed out with the
    /// level they unlock at. Skills that are turned off, and perks that are, are left out.
    ///
    /// The compendium rebuilds its pages each time it opens, so the page always shows the current levels and settings.
    /// </summary>
    internal static class CompendiumSkillPerks
    {
        private const string LockedColor = "#8C8C8C";

        [HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.UpdateTextsList))]
        private static class AddSkillPerksPage
        {
            // Vanilla's active effects and log pages come first, this goes straight after them.
            private const int PageIndex = 2;

            private static void Postfix(TextsDialog __instance) {
                if (Player.m_localPlayer == null) { return; }
                // A throw here would stop the compendium from opening at all.
                try {
                    TextsDialog.TextInfo page = new TextsDialog.TextInfo(LocalizationManager.Instance.TryTranslate("$compendium_skill_perks"), BuildPage(Player.m_localPlayer));
                    __instance.m_texts.Insert(Mathf.Min(PageIndex, __instance.m_texts.Count), page);
                } catch (Exception ex) {
                    Logger.LogWarning($"Unable to add the skill perks page to the compendium: {ex.Message}");
                }
            }
        }

        private static string BuildPage(Player player) {
            List<(string name, Skills.SkillType skill, List<SkillPerks.Perk> perks)> sections = new();
            foreach (KeyValuePair<Skills.SkillType, List<SkillPerks.Perk>> entry in SkillPerks.BySkill) {
                if (DisabledSkills.IsHidden(entry.Key)) { continue; }
                List<SkillPerks.Perk> enabled = entry.Value.Where(perk => perk.Enabled()).ToList();
                if (enabled.Count == 0) { continue; }
                sections.Add((LocalizationManager.Instance.TryTranslate(ConsoleSkillNames.NameToken(entry.Key)), entry.Key, enabled));
            }
            // Alphabetical in the player's own language, like the rest of the compendium.
            sections.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.CurrentCultureIgnoreCase));

            StringBuilder sb = new StringBuilder();
            foreach ((string name, Skills.SkillType skill, List<SkillPerks.Perk> perks) in sections) {
                float level = player.GetSkillLevel(skill);
                float factor = player.GetSkillFactor(skill);
                string levelText = LocalizationManager.Instance.TryTranslate("$compendium_skill_level").Replace("{0}", Mathf.FloorToInt(level).ToString());
                sb.Append("<color=orange>").Append(name).Append("</color>  ").Append(levelText).Append('\n');

                // Stable, so perks unlocking at the same level keep the order SkillPerks lists them in.
                foreach (SkillPerks.Perk perk in perks.OrderBy(perk => perk.Level?.Invoke() ?? 0f)) {
                    string perkName = LocalizationManager.Instance.TryTranslate(perk.Name);
                    string description = LocalizationManager.Instance.TryTranslate(perk.Description);
                    if (perk.Reached(level, factor)) {
                        sb.Append("<color=yellow>").Append(perkName).Append("</color>: ").Append(description).Append('\n');
                    } else {
                        string unlocksAt = LocalizationManager.Instance.TryTranslate("$compendium_perk_unlocks_at").Replace("{0}", Mathf.CeilToInt(perk.Level()).ToString());
                        sb.Append("<color=").Append(LockedColor).Append('>').Append(perkName).Append(" (").Append(unlocksAt).Append("): ").Append(description).Append("</color>\n");
                    }
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
