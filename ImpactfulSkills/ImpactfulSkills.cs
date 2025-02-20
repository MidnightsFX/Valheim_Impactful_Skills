using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System.Reflection;
using UnityEngine;

namespace ImpactfulSkills
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    //[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class ImpactfulSkills : BaseUnityPlugin
    {
        public const string PluginGUID = "com.jotunn.jotunnmodstub";
        public const string PluginName = "ImpactfulSkills";
        public const string PluginVersion = "0.0.1";

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

            Assembly assembly = Assembly.GetExecutingAssembly();
            Harmony harmony = new(PluginGUID);
            harmony.PatchAll(assembly);
        }
    }
}