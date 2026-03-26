using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ImpactfulSkills.modules;

namespace ImpactfulSkills.compatibility {
    internal static class PlantEasily {

        internal static class API {
            // Register a placement ghost limit
            // Allows rows and columns to be controlled by user of PlantEasily
            static Type apiType = AccessTools.TypeByName("PlantEasily.GhostLimitAPI");
            static MethodInfo register = apiType.GetMethod("RegisterGhostLimitProvider", BindingFlags.Public | BindingFlags.Static);

            public static bool IsAvailable => apiType != null;

            internal static void RegisterPlantEasilyLimit() {
                register.Invoke(null, new object[] {
                "ImpactfulSkills.modules.PlantGrid",
                new Func<int>(() => PlantGrid.MaxToPlantAtOnce())
            });
            }
        }
    }
}
