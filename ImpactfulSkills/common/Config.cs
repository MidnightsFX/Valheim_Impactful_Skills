using BepInEx.Configuration;

namespace ImpactfulSkills
{
    internal class ValConfig
    {
        public static ConfigFile cfg;
        public static ConfigEntry<bool> EnableDebugMode;
        public static ConfigEntry<float> WoodCuttingDmgMod;
        public static ConfigEntry<float> WoodCuttingLootFactor;
        public static ConfigEntry<float> MiningDmgMod;
        public static ConfigEntry<float> MiningLootFactor;
        public static ConfigEntry<float> MiningAOERangePerLevel;
        public static ConfigEntry<float> MiningAOELevel;
        public static ConfigEntry<float> SneakSpeedFactor;
        public static ConfigEntry<float> AnimalTamingSpeedFactor;
        public static ConfigEntry<float> GatheringLuckFactor;
        public static ConfigEntry<float> GatheringRangeFactor;
        public static ConfigEntry<int>  FarmingRangeRequiredLevel;
        public static ConfigEntry<string> GatheringLuckLevels;
        public static ConfigEntry<float> VoyagerReduceCuttingStart;
        public static ConfigEntry<float> VoyagerSailingSpeedFactor;
        public static ConfigEntry<float> VoyagerIncreaseExplorationRadius;

        public static ConfigEntry<float> AnimalTamingSkillGainRate;
        public static ConfigEntry<float> VoyagerSkillGainRate;

        public ValConfig(ConfigFile cf)
        {
            // ensure all the config values are created
            cfg = cf;
            cfg.SaveOnConfigSet = true;
            CreateConfigValues(cf);
            Logger.setDebugLogging(ValConfig.EnableDebugMode.Value);
        }

        private void CreateConfigValues(ConfigFile Config)
        {
            // Debugmode
            EnableDebugMode = Config.Bind("Client config", "EnableDebugMode", false,
                new ConfigDescription("Enables Debug logging.",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = true }));
            EnableDebugMode.SettingChanged += Logger.enableDebugLogging;
            WoodCuttingDmgMod = BindServerConfig("Woodcutting", "WoodCuttingDmgMod", 1.2f, "How much skill levels impact your chop damage.");
            WoodCuttingLootFactor = BindServerConfig("Woodcutting", "WoodCuttingLootFactor", 3f, "How much the woodcutting skill provides additional loot. 2 is 2x the loot at level 100.", false, 1f, 10f);
            MiningDmgMod = BindServerConfig("Mining", "MiningDmgMod", 1.2f, "How much your skill levels impact mining damage.");
            MiningLootFactor = BindServerConfig("Mining", "MiningLootFactor", 2f, "How much the mining skill provides additional loot. 2 is 2x the loot at level 100.");
            MiningAOERangePerLevel = BindServerConfig("Mining", "MiningAOERangePerLevel", 0.04f, "How far away the mining AOE is applied. How far away an AOE hit is applied.", false, 0.01f, 0.1f);
            MiningAOELevel = BindServerConfig("Mining", "MiningAOELevel", 50f, "The level that AOE mining requires to activate. What skill level Mining AOE is enabled at.", false, 0f, 100f);
            SneakSpeedFactor = BindServerConfig("Sneak", "SneakSpeedFactor", 0.03f, "How much sneak speed is increased based on your sneak level. Amount applied per level, 0.03 will make level 100 sneak give normal walkspeed while sneaking.", false, 0.001f, 0.06f);
            AnimalTamingSpeedFactor = BindServerConfig("AnimalHandling", "AnimalTamingSpeedFactor", 2f, "How much your animal handling skill impacts taming speed. Each level will apply this increase amount eg: 2% faster per level in taming", false, 0.1f, 5f);
            GatheringLuckFactor = BindServerConfig("Gathering", "GatheringLuckFactor", 0.5f, "How much luck impacts gathering. Each level gives you a small chance to get better loot.", false, 0.1f, 5f);
            GatheringRangeFactor = BindServerConfig("Farming", "GatheringRangeFactor", 0.02f, "How much gathering range is increased based on your gathering level. Amount applied per level, 0.5 will make level 100 gathering give 50% more range.", false, 0.001f, 1f);
            FarmingRangeRequiredLevel = BindServerConfig("Farming", "GatheringRangeRequiredLevel", 50, "The level that AOE gathering requires to activate.", false, 0, 100);
            GatheringLuckLevels = BindServerConfig("Farming", "GatheringLuckLevels", "20,40,60,80,100", "The Luck levels that you can roll additional loot at. These should be between 0-100. But can be assigned in any way or number- such as 0,10,10,10,100.");
            VoyagerReduceCuttingStart = BindServerConfig("Voyager", "VoyagerReduceCuttingStart", 50f, "The level that the player starts to reduce the penalty of not having the wind at your back.", false, 0f, 100f);
            VoyagerSailingSpeedFactor = BindServerConfig("Voyager", "VoyagerSailingSpeedFactor", 1f, "How much the sailing speed is increased based on your voyager level. Amount applied per level, 2 will make level 100 voyager give 100% faster sailing.", false, 1f, 20f);
            VoyagerIncreaseExplorationRadius = BindServerConfig("Voyager", "VoyagerIncreaseExplorationRadius", 1.5f, "How much the exploration radius is increased based on your voyager level. Amount applied per level, 1 will make level 100 voyager give 100% more exploration radius.", false, 0f, 20f);

            AnimalTamingSkillGainRate = BindServerConfig("SkillRates", "AnimalTamingSkillGainRate", 1f, "How fast the skill is gained.", false, 1f, 10f);
            VoyagerSkillGainRate = BindServerConfig("SkillRates", "VoyagerSkillGainRate", 1f, "How fast the skill is gained.", false, 1f, 10f);
        }

        /// <summary>
        /// Helper to bind configs for float types
        /// </summary>
        /// <param name="config_file"></param>
        /// <param name="catagory"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="description"></param>
        /// <param name="advanced"></param>
        /// <param name="valmin"></param>
        /// <param name="valmax"></param>
        /// <returns></returns>
        public static ConfigEntry<float[]> BindServerConfig(string catagory, string key, float[] value, string description, bool advanced = false, float valmin = 0, float valmax = 150)
        {
            return cfg.Bind(catagory, key, value,
                new ConfigDescription(description,
                new AcceptableValueRange<float>(valmin, valmax),
                new ConfigurationManagerAttributes { IsAdminOnly = true, IsAdvanced = advanced })
                );
        }

        /// <summary>
        ///  Helper to bind configs for bool types
        /// </summary>
        /// <param name="config_file"></param>
        /// <param name="catagory"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="description"></param>
        /// <param name="acceptableValues"></param>>
        /// <param name="advanced"></param>
        /// <returns></returns>
        public static ConfigEntry<bool> BindServerConfig(string catagory, string key, bool value, string description, AcceptableValueBase acceptableValues = null, bool advanced = false)
        {
            return cfg.Bind(catagory, key, value,
                new ConfigDescription(description,
                    acceptableValues,
                new ConfigurationManagerAttributes { IsAdminOnly = true, IsAdvanced = advanced })
                );
        }

        /// <summary>
        /// Helper to bind configs for int types
        /// </summary>
        /// <param name="config_file"></param>
        /// <param name="catagory"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="description"></param>
        /// <param name="advanced"></param>
        /// <param name="valmin"></param>
        /// <param name="valmax"></param>
        /// <returns></returns>
        public static ConfigEntry<int> BindServerConfig(string catagory, string key, int value, string description, bool advanced = false, int valmin = 0, int valmax = 150)
        {
            return cfg.Bind(catagory, key, value,
                new ConfigDescription(description,
                new AcceptableValueRange<int>(valmin, valmax),
                new ConfigurationManagerAttributes { IsAdminOnly = true, IsAdvanced = advanced })
                );
        }

        /// <summary>
        /// Helper to bind configs for float types
        /// </summary>
        /// <param name="config_file"></param>
        /// <param name="catagory"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="description"></param>
        /// <param name="advanced"></param>
        /// <param name="valmin"></param>
        /// <param name="valmax"></param>
        /// <returns></returns>
        public static ConfigEntry<float> BindServerConfig(string catagory, string key, float value, string description, bool advanced = false, float valmin = 0, float valmax = 150)
        {
            return cfg.Bind(catagory, key, value,
                new ConfigDescription(description,
                new AcceptableValueRange<float>(valmin, valmax),
                new ConfigurationManagerAttributes { IsAdminOnly = true, IsAdvanced = advanced })
                );
        }

        /// <summary>
        /// Helper to bind configs for strings
        /// </summary>
        /// <param name="config_file"></param>
        /// <param name="catagory"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="description"></param>
        /// <param name="advanced"></param>
        /// <returns></returns>
        public static ConfigEntry<string> BindServerConfig(string catagory, string key, string value, string description, AcceptableValueList<string> acceptableValues = null, bool advanced = false)
        {
            return cfg.Bind(catagory, key, value,
                new ConfigDescription(
                    description,
                    acceptableValues,
                new ConfigurationManagerAttributes { IsAdminOnly = true, IsAdvanced = advanced })
                );
        }
    }
}
