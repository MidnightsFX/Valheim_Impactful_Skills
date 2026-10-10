using HarmonyLib;
using ImpactfulSkills.patches;
using Jotunn.Configs;
using System;
using System.Reflection;

namespace ImpactfulSkills.compatibility {

    /// <summary>
    /// SkillsReworked groups skills under attributes from config lists of skill names. It finds a modded skill by
    /// hashing the name as typed and looking for a skill with that ID, which only works when the skill was registered
    /// under exactly that name. Ours are registered under identifiers like midnightsfx.forging, so "Forging" in one of
    /// its lists matched nothing, was dropped with a "could not resolve skill name" warning, and the skill stayed
    /// ungrouped. Its own way around that is the "Forging (256186231)" text its sr_skills command prints.
    ///
    /// When it finds nothing, the name is tried against ours the way the raiseskill command does (ConsoleSkillNames),
    /// so the name in the player's language, the token name like AnimalHandling, and underscores for spaces all work.
    /// Its level XP skill list and the lists its admins rewrite by dragging skills in the skills window go through the
    /// same lookup. SeneaL UI asks SkillsReworked which attribute a skill belongs to, so its skills window follows.
    /// </summary>
    internal static class SkillsReworked {
        internal const string PluginGUID = "M2Valheim.SkillsReworked";

        private const string MappingType = "SkillsReworked.Systems.Attributes.AttributeMappingService";
        private const string ResolveMethod = "ResolveEntry";

        private static MethodBase ResolveEntry() {
            Type mapping = AccessTools.TypeByName(MappingType);
            return mapping != null ? AccessTools.Method(mapping, ResolveMethod, new Type[] { typeof(string) }) : null;
        }

        [HarmonyPatch]
        private static class ResolveSkillNames {
            private static bool Prepare(MethodBase original) {
                if (Modcheck.IsSkillsReworkedEnabled == false) { return false; }

                bool found = ResolveEntry() != null;
                if (original == null && found == false) {
                    Logger.LogWarning($"SkillsReworked is installed but {MappingType}.{ResolveMethod} was not found, so this mod's skills can only be added to its attributes in the 'Name (id)' form its sr_skills command lists. This mod may need an update to match the installed SkillsReworked version.");
                }
                return found;
            }

            private static MethodBase TargetMethod() { return ResolveEntry(); }

            // Only when SkillsReworked found nothing, so its own matches always win.
            private static void Postfix(string entry, ref Skills.SkillType? __result) {
                if (__result.HasValue) { return; }

                SkillConfig skill = ConsoleSkillNames.Find(entry?.Trim());
                if (skill != null) { __result = skill.UID; }
            }
        }
    }
}
