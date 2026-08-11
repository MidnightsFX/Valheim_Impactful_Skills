using BepInEx.Configuration;
using HarmonyLib;
using ImpactfulSkills.patches;
using Jotunn.Configs;
using Jotunn.Managers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ImpactfulSkills.modules {
    internal static class SkillRates {
        public class SkillRateConfig {
            public string SkillName { get; set; }
            public string DisplayName { get; set; }
            public ConfigEntry<float> ConfigEntry { get; set; }
        }

        private const string ConfigSection = "SkillRates";
        private const string ConfigKeySuffix = "SkillGainRate";
        // SkillManager is merged into each mod that uses it, so every one of them has its own copy of this registry.
        private const string SkillManagerTypeName = "SkillManager.Skill";
        private const string SkillManagerRegistryField = "skillByName";

        static Dictionary<Skills.SkillType, SkillRateConfig> SkillRateConfigs = new Dictionary<Skills.SkillType, SkillRateConfig>();
        // The name each mod added skill is known by, which is the jotunn identifier or the SkillManager skill name.
        static Dictionary<Skills.SkillType, string> DiscoveredSkillNames = new Dictionary<Skills.SkillType, string>();
        // Config keys already handed out, so two mods naming their skills the same thing don't fight over one entry.
        static Dictionary<string, Skills.SkillType> UsedConfigKeys = new Dictionary<string, Skills.SkillType>(StringComparer.OrdinalIgnoreCase);
        // Skills that will never get an entry, so the raise prefix stops reconsidering them on every xp tick.
        static HashSet<Skills.SkillType> SkipSkills = new HashSet<Skills.SkillType>();

        internal static void SetupSkillRateConfigs() {
            int created = 0;
            ValConfig.BeginBatchBind();
            foreach (Skills.SkillType skill in Enum.GetValues(typeof(Skills.SkillType))) {
                if (EnsureSkillRateConfig(skill)) { created++; }
            }
            ValConfig.EndBatchBind(created > 0);
        }

        /// <summary>
        /// Mod added skills are not part of the SkillType enum, so the only way to find them is to ask whichever skill
        /// framework registered them. BepInEx runs every plugins Awake in one pass, so waiting a single frame is enough
        /// for all of them to have registered. This also runs on a dedicated server, where no player ever spawns.
        /// </summary>
        internal static IEnumerator DiscoverModdedSkillsDeferred() {
            yield return null;
            DiscoverModdedSkills();
        }

        internal static void DiscoverModdedSkills() {
            CollectJotunnSkills();
            CollectSkillManagerSkills();
            CollectAdditionalSkillNames();
            BindDiscoveredSkills(true);
        }

        public static void AdditionalSkillNamesChanged(object sender, EventArgs e) {
            try {
                CollectAdditionalSkillNames();
                // Deliberately not written out here, since this also fires while the config file is being reloaded.
                // The new settings work immediately either way, and are saved with the next change to any setting.
                BindDiscoveredSkills(false);
            } catch (Exception ex) {
                Logger.LogWarning($"Error handling the updated AdditionalSkillNames setting: {ex.Message}");
            }
        }

        private static void BindDiscoveredSkills(bool save) {
            int created = 0;
            ValConfig.BeginBatchBind();
            foreach (Skills.SkillType skill in DiscoveredSkillNames.Keys.ToArray()) {
                if (EnsureSkillRateConfig(skill)) { created++; }
            }
            ValConfig.EndBatchBind(save && created > 0);
            if (created > 0) {
                Logger.LogInfo($"Added skill gain rate settings for {created} skill(s) added by other mods.");
            }
        }

        /// <summary>
        /// Jotunn keeps every custom skill any mod registered in one shared registry, which is internal to it.
        /// </summary>
        private static void CollectJotunnSkills() {
            try {
                FieldInfo custom_skills_field = AccessTools.Field(typeof(SkillManager), "CustomSkills");
                if (custom_skills_field == null || !(custom_skills_field.GetValue(SkillManager.Instance) is IDictionary custom_skills)) {
                    Logger.LogWarning("Could not read Jotunns custom skill registry. Skills added through Jotunn will only get a gain rate setting once they first grant xp.");
                    return;
                }
                foreach (DictionaryEntry entry in custom_skills) {
                    RecordDiscoveredSkill((Skills.SkillType)entry.Key, (entry.Value as SkillConfig)?.Identifier, "Jotunn");
                }
            } catch (Exception ex) {
                Logger.LogWarning($"Error reading Jotunns custom skill registry: {ex.Message}");
            }
        }

        /// <summary>
        /// SkillManager (https://github.com/blaxxun-boop/SkillManager) is merged into each mod that uses it rather than
        /// shipped as a shared assembly, so every one of those mods carries its own registry that has to be found and
        /// read separately. Only the keys are needed - they are the skill names, which both frameworks hash into the
        /// skill type the same way.
        /// </summary>
        private static void CollectSkillManagerSkills() {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                try {
                    Type skill_type = assembly.GetType(SkillManagerTypeName, false);
                    if (skill_type == null) { continue; }

                    FieldInfo registry = skill_type.GetField(SkillManagerRegistryField, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    if (registry == null || !(registry.GetValue(null) is IDictionary skills_by_name)) {
                        Logger.LogWarning($"{assembly.GetName().Name} uses SkillManager, but its skill registry could not be read. Its skills will only get a gain rate setting once they first grant xp.");
                        continue;
                    }
                    foreach (DictionaryEntry entry in skills_by_name) {
                        string skill_name = entry.Key as string;
                        if (string.IsNullOrEmpty(skill_name)) { continue; }
                        RecordDiscoveredSkill(NameToSkillType(skill_name), skill_name, $"SkillManager in {assembly.GetName().Name}");
                    }
                } catch (Exception ex) {
                    Logger.LogDebug($"Skipped {assembly.GetName().Name} while looking for SkillManager skills: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Escape hatch for anything the automatic detection misses, since a mod could register its skills later than
        /// we look or use a framework we don't know about.
        /// </summary>
        private static void CollectAdditionalSkillNames() {
            if (string.IsNullOrEmpty(ValConfig.AdditionalSkillNames.Value)) { return; }
            foreach (string entry in ValConfig.AdditionalSkillNames.Value.Split(',')) {
                string skill_name = entry.Trim();
                if (skill_name.Length == 0) { continue; }

                Skills.SkillType skill = TryResolveSkill(skill_name, out Skills.SkillType known) ? known : NameToSkillType(skill_name);
                // Both frameworks make IsSkillValid return true for their own skills, so this covers all of them.
                // Binding a rate for a skill that doesn't exist would be harmless, but it would also hide a typo.
                if (!Skills.IsSkillValid(skill)) {
                    Logger.LogWarning($"AdditionalSkillNames entry '{skill_name}' does not match any registered skill, ignoring it. It needs to be the skill name or identifier the mod that adds the skill uses.");
                    continue;
                }
                RecordDiscoveredSkill(skill, skill_name, "AdditionalSkillNames");
            }
        }

        private static void RecordDiscoveredSkill(Skills.SkillType skill, string skill_name, string source) {
            skill = NormalizeSkillType(skill);
            if (skill == Skills.SkillType.None || string.IsNullOrEmpty(skill_name) || DiscoveredSkillNames.ContainsKey(skill)) { return; }
            DiscoveredSkillNames[skill] = skill_name;
            Logger.LogDebug($"Found mod added skill '{skill_name}' ({(int)skill}) via {source}.");
        }

        /// <summary>
        /// Creates the gain rate setting for a skill if it doesn't have one yet. Safe to call repeatedly, and every
        /// call leaves the skill either configured or skipped so callers never have to reconsider it.
        /// </summary>
        private static bool EnsureSkillRateConfig(Skills.SkillType skill) {
            skill = NormalizeSkillType(skill);
            if (SkillRateConfigs.ContainsKey(skill) || SkipSkills.Contains(skill)) { return false; }

            // None and All are not skills, and our own skills set their xp amount where they award it instead.
            if (skill == Skills.SkillType.None || skill == Skills.SkillType.All
                || skill == Voyaging.VoyagingSkill || skill == Hauling.HaulingSkill || skill == AnimalWhisper.AnimalHandling) {
                SkipSkills.Add(skill);
                return false;
            }

            string skill_name = ResolveConfigName(skill);
            string display_name = LocalizedSkillName(skill) ?? skill_name;
            try {
                ConfigEntry<float> config_entry = ValConfig.BindServerConfig(ConfigSection, UniqueConfigKey(skill, skill_name), 1f,
                    $"How fast the {display_name} skill is gained. 1 is the default rate, below 1 slows it down and 0 stops it being gained at all.", false, 0f, 50f);
                SkillRateConfigs[skill] = new SkillRateConfig { SkillName = skill_name, DisplayName = display_name, ConfigEntry = config_entry };
                return true;
            } catch (Exception ex) {
                Logger.LogWarning($"Could not create a skill gain rate setting for '{skill_name}' ({(int)skill}): {ex.Message}");
                SkipSkills.Add(skill);
                return false;
            }
        }

        /// <summary>
        /// Picks the name a skills setting is keyed on. Vanilla skills keep their enum name so existing configurations
        /// are left untouched, mod added skills use whatever their own framework knows them as.
        /// </summary>
        private static string ResolveConfigName(Skills.SkillType skill) {
            if (DiscoveredSkillNames.TryGetValue(skill, out string discovered)) { return Sanitize(discovered); }
            if (Enum.IsDefined(typeof(Skills.SkillType), skill)) { return skill.ToString(); }

            string localized = LocalizedSkillName(skill);
            if (localized != null) { return Sanitize(localized); }

            return ((int)skill).ToString();
        }

        /// <summary>
        /// Two mods can name their skills the same thing, and a mod skill can share a vanilla skills name. Whichever
        /// skill claims the readable key first keeps it, the rest are told apart by their skill id.
        /// </summary>
        private static string UniqueConfigKey(Skills.SkillType skill, string skill_name) {
            string config_key = $"{skill_name}{ConfigKeySuffix}";
            if (UsedConfigKeys.TryGetValue(config_key, out Skills.SkillType existing) && existing != skill) {
                config_key = $"{skill_name}_{(int)skill}{ConfigKeySuffix}";
                Logger.LogInfo($"More than one skill is named '{skill_name}', using '{config_key}' for the one with id {(int)skill}.");
            }
            UsedConfigKeys[config_key] = skill;
            return config_key;
        }

        /// <summary>
        /// The game builds a skills display token from its numeric type, and both frameworks register their names under
        /// that same token. Localization hands back "[token]" when it has no translation for it.
        /// </summary>
        private static string LocalizedSkillName(Skills.SkillType skill) {
            if (Localization.instance == null) { return null; }
            string localized = Localization.instance.Localize($"$skill_{skill.ToString().ToLower()}");
            if (string.IsNullOrEmpty(localized) || localized.StartsWith("[")) { return null; }
            return localized;
        }

        /// <summary>
        /// Config keys can't contain everything a skill name can, so trim them down to what is safe in the config file.
        /// </summary>
        private static string Sanitize(string skill_name) {
            StringBuilder sanitized = new StringBuilder(skill_name.Length);
            foreach (char character in skill_name) {
                if ((character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z')
                    || (character >= '0' && character <= '9') || character == '_' || character == '.' || character == '-') {
                    sanitized.Append(character);
                }
            }
            return sanitized.Length > 0 ? sanitized.ToString() : Math.Abs(skill_name.GetStableHashCode()).ToString();
        }

        /// <summary>
        /// Both Jotunn and SkillManager turn a name into a skill type with Math.Abs(name.GetStableHashCode()) - jotunn
        /// hashes the mods identifier, SkillManager hashes the english skill name - so one conversion covers both.
        /// </summary>
        internal static Skills.SkillType NameToSkillType(string skill_name) {
            return NormalizeSkillType((Skills.SkillType)skill_name.GetStableHashCode());
        }

        /// <summary>
        /// Resolves a configured skill name to its type. Accepts vanilla skill names, jotunn identifiers, SkillManager
        /// skill names and the in game display name of any mod added skill we have found.
        /// </summary>
        internal static bool TryResolveSkill(string skill_name, out Skills.SkillType skill) {
            skill = Skills.SkillType.None;
            if (string.IsNullOrEmpty(skill_name)) { return false; }
            skill_name = skill_name.Trim();

            foreach (Skills.SkillType vanilla in Enum.GetValues(typeof(Skills.SkillType))) {
                if (vanilla == Skills.SkillType.None || vanilla == Skills.SkillType.All) { continue; }
                if (vanilla.ToString().Equals(skill_name, StringComparison.OrdinalIgnoreCase)) {
                    skill = vanilla;
                    return true;
                }
            }

            Skills.SkillType hashed = NameToSkillType(skill_name);
            if (DiscoveredSkillNames.ContainsKey(hashed) || SkillRateConfigs.ContainsKey(hashed)) {
                skill = hashed;
                return true;
            }

            // The display name is all a player may know a skill by, and it often isn't what the mod named it.
            foreach (KeyValuePair<Skills.SkillType, string> discovered in DiscoveredSkillNames) {
                string display_name = LocalizedSkillName(discovered.Key);
                if (display_name != null && display_name.Equals(skill_name, StringComparison.OrdinalIgnoreCase)) {
                    skill = discovered.Key;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Skills registered before jotunn existed can have negative ids, which both frameworks normalize with Math.Abs.
        /// </summary>
        private static Skills.SkillType NormalizeSkillType(Skills.SkillType skill) {
            int skill_id = (int)skill;
            return skill_id < 0 && skill_id != int.MinValue ? (Skills.SkillType)Math.Abs(skill_id) : skill;
        }

        /// <summary>
        /// Last resort for a skill registered after everything else has run. Binding here misses the servers config
        /// sync, so the setting only takes effect for everyone after a restart.
        /// </summary>
        private static void EnsureSkillRateConfigLate(Skills.SkillType skill) {
            ValConfig.BeginBatchBind();
            bool created = EnsureSkillRateConfig(skill);
            ValConfig.EndBatchBind(created);
            if (created) {
                Logger.LogInfo($"Created a skill gain rate setting for '{SkillRateConfigs[NormalizeSkillType(skill)].DisplayName}' the first time it granted xp. Restart for it to be synchronized to clients.");
            }
        }

        /// <summary>
        /// Catches skills registered after our startup scan, plus anything that adds its definition to the skill list
        /// directly rather than through a framework we know about.
        /// </summary>
        [HarmonyPatch(typeof(Skills), nameof(Skills.Awake))]
        public static class PatchSkillsAwakeFindSkills {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(Skills __instance) {
                if (__instance.m_skills == null) { return; }

                int created = 0;
                ValConfig.BeginBatchBind();
                foreach (Skills.SkillDef skill_def in __instance.m_skills) {
                    if (skill_def == null) { continue; }
                    if (EnsureSkillRateConfig(skill_def.m_skill)) { created++; }
                }
                ValConfig.EndBatchBind(created > 0);
                if (created > 0) {
                    Logger.LogInfo($"Added skill gain rate settings for {created} skill(s) that registered after startup.");
                }
            }
        }

        /// <summary>
        /// Player.RaiseSkill delegates here, and mods raising skills through SkillManagers Skills extension call this
        /// directly, so it is the one place every skill gain passes through.
        /// </summary>
        [HarmonyPatch(typeof(Skills), nameof(Skills.RaiseSkill))]
        public static class PatchSkillIncreaseHigherGainsForLowerSkills {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.HigherThanNormal)]
            private static void Prefix(Skills.SkillType skillType, ref float factor) {
                Skills.SkillType skill = NormalizeSkillType(skillType);
                if (!SkillRateConfigs.TryGetValue(skill, out SkillRateConfig skill_rate)) {
                    if (SkipSkills.Contains(skill)) { return; }
                    EnsureSkillRateConfigLate(skill);
                    if (!SkillRateConfigs.TryGetValue(skill, out skill_rate)) { return; }
                }
                factor *= skill_rate.ConfigEntry.Value;
            }
        }
    }
}
