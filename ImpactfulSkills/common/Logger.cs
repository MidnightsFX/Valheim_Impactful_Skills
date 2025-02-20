using BepInEx.Logging;
using ImpactfulSkills;
using System;


namespace ImpactfulSkills
{
    internal class Logger
    {
        public static LogLevel Level = LogLevel.Info;

        public static void enableDebugLogging(object sender, EventArgs e)
        {
            if (ValConfig.EnableDebugMode.Value)
            {
                Level = LogLevel.Debug;
            }
            else
            {
                Level = LogLevel.Info;
            }
            // set log level
        }

        public static void LogDebug(string message)
        {
            if (Level >= LogLevel.Debug)
            {
                ImpactfulSkills.Log.LogInfo(message);
            }
        }
        public static void LogInfo(string message)
        {
            if (Level >= LogLevel.Info)
            {
                ImpactfulSkills.Log.LogInfo(message);
            }
        }

        public static void LogWarning(string message)
        {
            if (Level >= LogLevel.Warning)
            {
                ImpactfulSkills.Log.LogWarning(message);
            }
        }

        public static void LogError(string message)
        {
            if (Level >= LogLevel.Error)
            {
                ImpactfulSkills.Log.LogError(message);
            }
        }
    }
}
