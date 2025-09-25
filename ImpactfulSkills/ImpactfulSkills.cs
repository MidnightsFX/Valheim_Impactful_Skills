using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ImpactfulSkills.patches;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ImpactfulSkills
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInIncompatibility("blacks7ar.SNEAKer")]
    [NetworkCompatibility(CompatibilityLevel.ClientMustHaveMod, VersionStrictness.Minor)]
    internal class ImpactfulSkills : BaseUnityPlugin
    {
        public const string PluginGUID = "MidnightsFX.ImpactfulSkills";
        public const string PluginName = "ImpactfulSkills";
        public const string PluginVersion = "0.5.8";

        public ValConfig cfg;
        // Use this class to add your own localization to the game
        // https://valheim-modding.github.io/Jotunn/tutorials/localization.html
        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();
        internal static AssetBundle EmbeddedResourceBundle;
        public static ManualLogSource Log;

        public void Awake()
        {
            Log = this.Logger;
            cfg = new ValConfig(Config);
            EmbeddedResourceBundle = AssetUtils.LoadAssetBundleFromResources("ImpactfulSkills.AssetsEmbedded.impactfulskills", typeof(ImpactfulSkills).Assembly);
            AddLocalizations();
            Gathering.SetupGatherables();
            AnimalWhisper.SetupAnimalSkill();
            Voyaging.SetupSailingSkill();

            Assembly assembly = Assembly.GetExecutingAssembly();
            Harmony harmony = new(PluginGUID);
            harmony.PatchAll(assembly);
        }

        private void AddLocalizations()
        {
            // Use this class to add your own localization to the game
            // https://valheim-modding.github.io/Jotunn/tutorials/localization.html
            CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();
            // ValheimFortress.localizations.English.json
            // load all localization files within the localizations directory
            Logger.LogInfo("Loading Localizations.");
            foreach (string embeddedResouce in typeof(ImpactfulSkills).Assembly.GetManifestResourceNames())
            {
                if (!embeddedResouce.Contains("Localizations")) { continue; }
                // Read the localization file
                string localization = ReadEmbeddedResourceFile(embeddedResouce);
                // since I use comments in the localization that are not valid JSON those need to be stripped
                string cleaned_localization = Regex.Replace(localization, @"\/\/.*", "");
                // Just the localization name
                var localization_name = embeddedResouce.Split('.');
                Logger.LogDebug($"Adding localization: {localization_name[2]}");
                Localization.AddJsonFile(localization_name[2], cleaned_localization);
            }
        }

        /// <summary>
        /// This reads an embedded file resouce name, these are all resouces packed into the DLL
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        internal static string ReadEmbeddedResourceFile(string filename)
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