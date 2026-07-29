using Jotunn.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ImpactfulSkills
{
    /// <summary>
    /// Loads all localizations embedded within the Localizations directory, and mirrors each one to
    /// BepInEx/config/ImpactfulSkills/Localizations so players can edit or add translations without rebuilding.
    ///
    /// Localizations should be plain JSON objects with each of the two required entries being separate eg:
    /// "item_sword": "sword-name-here",
    /// "item_sword_description": "sword-description-here",
    /// the localization file itself should be a case matched language as defined by one of the "folder" language
    /// names from here: https://valheim-modding.github.io/Jotunn/data/localization/language-list.html
    /// </summary>
    internal static class LocalizationLoader
    {
        internal const string LocalizationFolder = "Localizations";

        // Only whole comment lines are stripped, so a translation containing "//" (eg a url) survives intact.
        private static readonly Regex CommentLine = new Regex(@"^\s*//.*$", RegexOptions.Multiline);

        internal static void AddLocalizations()
        {
            CustomLocalization Localization = ImpactfulSkills.Localization;

            // Ensure localization folder exists
            string translationFolder = Path.Combine(BepInEx.Paths.ConfigPath, ValConfig.cfgFolder, LocalizationFolder);
            Directory.CreateDirectory(translationFolder);

            Logger.LogInfo("Loading Localizations.");
            foreach (string embeddedResource in typeof(ImpactfulSkills).Assembly.GetManifestResourceNames())
            {
                if (!embeddedResource.Contains(LocalizationFolder)) { continue; }

                string language = LanguageNameFromResource(embeddedResource);
                if (language == null)
                {
                    Logger.LogWarning($"Could not determine a language name for '{embeddedResource}', skipping it.");
                    continue;
                }

                // A single broken user file should never take down the rest of the languages.
                try
                {
                    AddLocalization(Localization, embeddedResource, language, Path.Combine(translationFolder, $"{language}.json"));
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to load localization '{language}': {ex.Message}");
                }
            }
        }

        private static void AddLocalization(CustomLocalization Localization, string embeddedResource, string language, string translationFile)
        {
            // since I use comments in the localization that are not valid JSON those need to be stripped
            string cleaned_localization = CommentLine.Replace(ReadEmbeddedResourceFile(embeddedResource), "");
            Dictionary<string, string> internal_localization = SimpleJson.SimpleJson.DeserializeObject<Dictionary<string, string>>(cleaned_localization);
            // The embedded file is the source of truth for which tokens exist, and for the order they are written in.
            List<string> token_order = internal_localization.Keys.ToList();

            Dictionary<string, string> localization = internal_localization;
            bool needs_write = true;
            if (File.Exists(translationFile))
            {
                try
                {
                    localization = SimpleJson.SimpleJson.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(translationFile));
                    needs_write = UpdateLocalizationWithMissingKeys(internal_localization, localization);
                }
                catch
                {
                    // The players file is not valid JSON, fall back to the embedded copy and overwrite it.
                    Logger.LogWarning($"'{translationFile}' could not be parsed, resetting it to the built in {language} localization.");
                    localization = internal_localization;
                    needs_write = true;
                }
            }

            string translations = SerializePretty(localization, token_order);
            // Only touch the file when something actually changed, so a players formatting and key order survive a restart.
            if (needs_write)
            {
                File.WriteAllText(translationFile, translations, new UTF8Encoding(false));
                Logger.LogDebug($"Wrote {translationFile}");
            }

            Localization.AddJsonFile(language, translations);
            Logger.LogDebug($"Added localization: '{language}'");
        }

        /// <summary>
        /// Brings the players localization in line with the embedded one - adds tokens the mod gained, removes
        /// tokens the mod dropped. Returns true when anything changed.
        /// </summary>
        private static bool UpdateLocalizationWithMissingKeys(Dictionary<string, string> internal_localization, Dictionary<string, string> cached_localization)
        {
            bool changed = false;

            List<string> extra_keys = cached_localization.Keys.Where(key => !internal_localization.ContainsKey(key)).ToList();
            if (extra_keys.Count > 0)
            {
                Logger.LogDebug($"Removing extra keys {string.Join(",", extra_keys)}.");
                foreach (string key in extra_keys) { cached_localization.Remove(key); }
                changed = true;
            }

            foreach (KeyValuePair<string, string> entry in internal_localization)
            {
                if (!cached_localization.ContainsKey(entry.Key))
                {
                    Logger.LogDebug($"Adding missing localization key {entry.Key}");
                    cached_localization.Add(entry.Key, entry.Value);
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>
        /// SimpleJson serializes to a single line, which is miserable to hand edit. These files exist to be edited,
        /// so write them out one token per line in the embedded files order.
        /// </summary>
        private static string SerializePretty(Dictionary<string, string> localization, List<string> token_order)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\n");
            for (int i = 0; i < token_order.Count; i++)
            {
                string token = token_order[i];
                sb.Append("  \"").Append(EscapeJsonString(token)).Append("\": \"").Append(EscapeJsonString(localization[token])).Append('"');
                if (i < token_order.Count - 1) { sb.Append(','); }
                sb.Append('\n');
            }
            sb.Append("}\n");
            return sb.ToString();
        }

        // Non-ascii is deliberately passed through as-is, these files are written as UTF-8 rather than escaped.
        private static string EscapeJsonString(string value)
        {
            StringBuilder sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') { sb.Append("\\u").Append(((int)c).ToString("x4")); }
                        else { sb.Append(c); }
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Resource names look like 'ImpactfulSkills.Localizations.English.json', the language is whatever segment
        /// follows the localization folder. Derived rather than index based so nesting the folder cannot break it.
        /// </summary>
        private static string LanguageNameFromResource(string embeddedResource)
        {
            string[] segments = embeddedResource.Split('.');
            int folder = Array.IndexOf(segments, LocalizationFolder);
            if (folder < 0 || folder + 2 > segments.Length - 1) { return null; }
            return segments[folder + 1];
        }

        /// <summary>
        /// This reads an embedded file resource name, these are all resources packed into the DLL
        /// </summary>
        private static string ReadEmbeddedResourceFile(string filename)
        {
            using (var stream = typeof(ImpactfulSkills).Assembly.GetManifestResourceStream(filename))
            {
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
