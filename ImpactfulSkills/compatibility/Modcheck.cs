using Jotunn.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ImpactfulSkills.compatibility {
    internal static class Modcheck {
        // Mod flags
        public static bool IsSNEAKerEnabled = false;
        public static bool IsMagicPluginEnabled = false;
        public static bool IsCrystalMagicalEnabled = false;

        // Plant grid compatibility checks
        public static bool IsPlantEasilyEnabled = false;
        public static bool IsGathilFarmGridEnabled = false;
        public static bool IsVentureFarmGridEnabled = false;
        public static bool IsXeioMassFarmingEnabled = false;
        public static bool IsComfyMassFarmingEnabled = false;

        internal static void CheckModCompat() {
            try {
                Dictionary<string, BepInEx.BaseUnityPlugin> plugins = BepInExUtils.GetPlugins();
                if (plugins == null) { return; }

                if (plugins.Keys.Contains("blacks7ar.SNEAKer")) {
                    IsSNEAKerEnabled = true;
                }
                if (plugins.Keys.Contains("blacks7ar.MagicPlugin")) {
                    IsMagicPluginEnabled = true;
                }
                if (plugins.Keys.Contains("dev.crystal.magical")) {
                    IsCrystalMagicalEnabled = true;
                }
                if (plugins.Keys.Contains("advize.PlantEasily")) {
                    IsPlantEasilyEnabled = true;
                }
                if (plugins.Keys.Contains("fr.galathil.FarmGrid")) {
                    IsGathilFarmGridEnabled = true;
                }
                if (plugins.Keys.Contains("com.orianaventure.mod.VentureFarmGrid")) {
                    IsVentureFarmGridEnabled = true;
                }
                if (plugins.Keys.Contains("xeio.MassFarming")) {
                    IsXeioMassFarmingEnabled = true;
                }
                if (plugins.Keys.Contains("EardwulfDoesMods.Comfy.MassFarming")) {
                    IsComfyMassFarmingEnabled = true;
                }
            } catch {
                Logger.LogWarning("Unable to check mod compatibility. Ensure that Bepinex can load.");
            }
        }

        internal static bool OtherFarmingGridModPresent() {
            if (IsPlantEasilyEnabled || IsGathilFarmGridEnabled || IsVentureFarmGridEnabled || IsXeioMassFarmingEnabled || IsComfyMassFarmingEnabled) {
                return true;
            }

            return false;
        }
    }
}
