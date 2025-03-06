using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ImpactfulSkills.patches;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System.Reflection;
using UnityEngine;

namespace ImpactfulSkills
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.ClientMustHaveMod, VersionStrictness.Minor)]
    internal class ImpactfulSkills : BaseUnityPlugin
    {
        public const string PluginGUID = "MidnightsFX.ImpactfulSkills";
        public const string PluginName = "ImpactfulSkills";
        public const string PluginVersion = "0.3.0";

        public ValConfig cfg;
        // Use this class to add your own localization to the game
        // https://valheim-modding.github.io/Jotunn/tutorials/localization.html
        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();
        internal static AssetBundle EmbeddedResourceBundle;
        public static ManualLogSource Log;

        private void Awake()
        {
            Log = this.Logger;
            cfg = new ValConfig(Config);
            EmbeddedResourceBundle = AssetUtils.LoadAssetBundleFromResources("ImpactfulSkills.AssetsEmbedded.impactfulskills", typeof(ImpactfulSkills).Assembly);

            Gathering.SetupGatherables();
            AnimalWhisper.SetupAnimalSkill();
            Voyaging.SetupSailingSkill();

            Assembly assembly = Assembly.GetExecutingAssembly();
            Harmony harmony = new(PluginGUID);
            harmony.PatchAll(assembly);
        }
    }
}