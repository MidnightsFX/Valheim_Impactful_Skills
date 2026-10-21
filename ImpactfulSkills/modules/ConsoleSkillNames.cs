using HarmonyLib;
using Jotunn.Configs;
using System;
using System.Collections.Generic;
using System.Text;

namespace ImpactfulSkills.patches
{
    /// <summary>
    /// Lets the raiseskill and resetskill console commands reach this mod's skills when their name has a space in it,
    /// like Animal Handling, or Forging in Russian.
    ///
    /// The console splits what is typed on every space and has no quoting, so "raiseskill Animal Handling 50" reaches
    /// the command as the skill "Animal" with an amount of "Handling". Jotunn matches custom skills by their localized
    /// name, their raw $skill_ token or their identifier, so it never sees a name it knows. Here an underscore stands in
    /// for a space, the same convention blaxxun's SkillManager uses for its skills, and case, spaces and underscores are
    /// all ignored when comparing, so Animal_Handling, AnimalHandling and animal_handling all work. The name can be the
    /// one in the player's own language, or the one in the skill's token, which works in every language.
    ///
    /// SkillsReworked's attribute skill lists accept the same names (compatibility/SkillsReworked.cs).
    /// </summary>
    internal static class ConsoleSkillNames
    {
        private const string TokenPrefix = "$skill_";

        private static readonly List<SkillConfig> skills = new List<SkillConfig>();

        internal static void Register(SkillConfig skill) { skills.Add(skill); }

        internal static SkillConfig Find(string name) {
            if (string.IsNullOrEmpty(name) || Localization.instance == null) { return null; }

            string typed = Squash(name);
            if (typed.Length == 0) { return null; }
            foreach (SkillConfig skill in skills) {
                if (SameName(typed, Localization.instance.Localize(skill.Name)) || SameName(typed, TokenName(skill))) { return skill; }
            }
            return null;
        }

        // Ordinal rather than culture aware, so a Turkish system locale cannot stop "hauling" from matching "Hauling".
        private static bool SameName(string squashed, string candidate) {
            return string.Equals(squashed, Squash(candidate), StringComparison.OrdinalIgnoreCase);
        }

        // "$skill_AnimalHandling" -> "AnimalHandling"
        private static string TokenName(SkillConfig skill) {
            return skill.Name.StartsWith(TokenPrefix, StringComparison.OrdinalIgnoreCase) ? skill.Name.Substring(TokenPrefix.Length) : skill.Name;
        }

        /// <summary>
        /// The token a skill's name is localized from. Vanilla's skills panel builds it from the skill type, which only
        /// works for vanilla skills, since the ones added here have a hash for a type.
        /// </summary>
        internal static string NameToken(Skills.SkillType skillType) {
            foreach (SkillConfig skill in skills) {
                if (skill.UID == skillType) { return skill.Name; }
            }
            return TokenPrefix + skillType.ToString().ToLower();
        }

        private static string Squash(string name) {
            StringBuilder sb = new StringBuilder(name.Length);
            foreach (char c in name) {
                if (c == ' ' || c == '_') { continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Swaps a name we recognise for the skill's identifier, which Jotunn matches exactly, so Jotunn still does the
        /// raising and resetting. First, so the name has been swapped by the time Jotunn's own prefix compares it.
        /// </summary>
        [HarmonyPatch(typeof(Skills))]
        private static class ResolveConsoleSkillName
        {
            [HarmonyPatch(nameof(Skills.CheatRaiseSkill))]
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void RaiseSkill(ref string name) {
                SkillConfig skill = Find(name);
                if (skill != null) { name = skill.Identifier; }
            }

            [HarmonyPatch(nameof(Skills.CheatResetSkill))]
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void ResetSkill(ref string name) {
                SkillConfig skill = Find(name);
                if (skill != null) { name = skill.Identifier; }
            }
        }

        /// <summary>
        /// Jotunn offers custom skills to tab completion by their localized name, which for a name with a space in it
        /// completes to a command that cannot run. Offer ours with underscores instead. Done to the options the command
        /// hands out rather than by wrapping its fetcher, so it does not matter whether this runs before or after Jotunn
        /// adds its names. The command keeps the list it is given, so after the first call there is nothing left to fix.
        /// </summary>
        [HarmonyPatch(typeof(Terminal.ConsoleCommand), nameof(Terminal.ConsoleCommand.GetTabOptions))]
        private static class OfferUnderscoredSkillNames
        {
            private static void Postfix(Terminal.ConsoleCommand __instance, List<string> __result) {
                if (__result == null || (__instance.Command != "raiseskill" && __instance.Command != "resetskill")) { return; }

                for (int i = 0; i < __result.Count; i++) {
                    string option = __result[i];
                    if (option == null || option.IndexOf(' ') < 0 || Find(option) == null) { continue; }
                    __result[i] = option.Replace(' ', '_');
                }
            }
        }
    }
}
