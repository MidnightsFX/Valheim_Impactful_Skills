using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ImpactfulSkills.compatibility;
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
    [SynchronizationMode(AdminOnlyStrictness.IfOnServer)]
    [NetworkCompatibility(CompatibilityLevel.ClientMustHaveMod, VersionStrictness.Minor)]
    [BepInDependency("blacks7ar.SNEAKer", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("blacks7ar.MagicPlugin", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("dev.crystal.magical", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("advize.PlantEasily", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("fr.galathil.FarmGrid", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.orianaventure.mod.VentureFarmGrid", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("xeio.MassFarming", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("EardwulfDoesMods.Comfy.MassFarming", BepInDependency.DependencyFlags.SoftDependency)]
    internal class ImpactfulSkills : BaseUnityPlugin
    {
        public const string PluginGUID = "MidnightsFX.ImpactfulSkills";
        public const string PluginName = "ImpactfulSkills";
        public const string PluginVersion = "0.9.7";

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
            LocalizationLoader.AddLocalizations();
            Gathering.SetupGatherables();
            Mining.SetupMining();
            AnimalWhisper.SetupAnimalSkill();
            Voyaging.SetupSailingSkill();
            Hauling.SetupHaulingSkill();
            HaulingXPTracker.Create();

            Modcheck.CheckModCompat();

            Assembly assembly = Assembly.GetExecutingAssembly();
            Harmony harmony = new(PluginGUID);
            harmony.PatchAll(assembly);
        }
    }
}