using Jotunn.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ImpactfulSkills.common {
    internal static class Compatibility {
        // Mod flags
        public static bool IsSNEAKerEnabled = false;
        public static bool IsMagicPluginEnabled = false;
        public static bool IsCrystalMagicalEnabled = false;

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
            } catch {
                Logger.LogWarning("Unable to check mod compatibility. Ensure that Bepinex can load.");
            }

        }
    }
}
