using BepInEx.Configuration;
using ImpactfulSkills.patches;

namespace ImpactfulSkills
{
    internal class ValConfig
    {
        public static ConfigFile cfg;
        public static ConfigEntry<bool> EnableDebugMode;

        public static ConfigEntry<bool> EnableWoodcutting;
        public static ConfigEntry<float> WoodCuttingDmgMod;
        public static ConfigEntry<float> WoodCuttingLootFactor;

        public static ConfigEntry<bool> EnableMining;
        public static ConfigEntry<float> MiningDmgMod;
        public static ConfigEntry<float> MiningLootFactor;
        public static ConfigEntry<float> MiningAOERange;
        public static ConfigEntry<float> MiningAOELevel;
        public static ConfigEntry<bool> EnableMiningAOE;
        public static ConfigEntry<bool> EnableMiningRockBreaker;
        public static ConfigEntry<float> RockBreakerMaxChance;
        public static ConfigEntry<int> RockBreakerRequiredLevel;
        public static ConfigEntry<float> RockBreakerDamage;
        public static ConfigEntry<int> MinehitsPerInterval;

        public static ConfigEntry<bool> EnableStealth;
        public static ConfigEntry<float> SneakSpeedFactor;
        public static ConfigEntry<float> SneakNoiseReductionLevel;
        public static ConfigEntry<float> SneakNoiseReductionFactor;

        public static ConfigEntry<bool> EnableRun;
        public static ConfigEntry<float> RunSpeedFactor;

        public static ConfigEntry<bool> EnableAnimalWhisper;
        public static ConfigEntry<float> AnimalTamingSpeedFactor;
        public static ConfigEntry<float> TamedAnimalLootIncreaseFactor;
        public static ConfigEntry<int> BetterBeesLevel;
        public static ConfigEntry<bool> EnableBeeBonuses;
        public static ConfigEntry<int> BeeBiomeUnrestrictedLevel;
        public static ConfigEntry<bool> EnableBeeBiomeUnrestricted;
        public static ConfigEntry<float> BeeHoneyIncreaseDropChance;

        public static ConfigEntry<bool> EnableGathering;
        public static ConfigEntry<float> GatheringLuckFactor;
        public static ConfigEntry<float> GatheringRangeFactor;
        public static ConfigEntry<int>  FarmingRangeRequiredLevel;
        public static ConfigEntry<string> GatheringLuckLevels;
        public static ConfigEntry<string> GatheringDisallowedItems;

        public static ConfigEntry<bool> EnableVoyager;
        public static ConfigEntry<int> VoyagerSkillXPCheckFrequency;
        public static ConfigEntry<float> VoyagerReduceCuttingStart;
        public static ConfigEntry<float> VoyagerSailingSpeedFactor;
        public static ConfigEntry<float> VoyagerIncreaseExplorationRadius;
        public static ConfigEntry<float> VoyagerPaddleSpeedBonus;
        public static ConfigEntry<float> VoyagerPaddleSpeedBonusLevel;

        public static ConfigEntry<bool> EnableWeaponSkill;
        public static ConfigEntry<float> WeaponSkillStaminaReduction;
        public static ConfigEntry<float> WeaponSkillBowDrawStaminaCostReduction;
        public static ConfigEntry<float> WeaponSkillParryBonus;

        public static ConfigEntry<bool> EnableCooking;
        public static ConfigEntry<float> CookingBurnReduction;

        public static ConfigEntry<bool> EnableBloodMagic;
        public static ConfigEntry<float> BloodMagicXPForShieldDamageRatio;
        public static ConfigEntry<float> BloodMagicXP;

        public static ConfigEntry<bool> EnableKnowledgeSharing;
        public static ConfigEntry<float> AnimalTamingSkillGainRate;
        public static ConfigEntry<float> VoyagerSkillGainRate;
        public static ConfigEntry<float> SharedKnowledgeSkillBonusRate;
        public static ConfigEntry<float> SharedKnowledgeCap;
        public static ConfigEntry<string> SharedKnowledgeIgnoreList;

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
            EnableWoodcutting = BindServerConfig("Woodcutting", "EnableWoodcutting", true, "Enable woodcutting skill changes.");
            WoodCuttingDmgMod = BindServerConfig("Woodcutting", "WoodCuttingDmgMod", 1.2f, "How much skill levels impact your chop damage.");
            WoodCuttingLootFactor = BindServerConfig("Woodcutting", "WoodCuttingLootFactor", 3f, "How much the woodcutting skill provides additional loot. 2 is 2x the loot at level 100.", false, 1f, 10f);

            EnableMining = BindServerConfig("Mining", "EnableMining", true, "Enable mining skill changes.");
            MiningDmgMod = BindServerConfig("Mining", "MiningDmgMod", 1.2f, "How much your skill levels impact mining damage.");
            MiningLootFactor = BindServerConfig("Mining", "MiningLootFactor", 2f, "How much the mining skill provides additional loot. 2 is 2x the loot at level 100.");
            EnableMiningAOE = BindServerConfig("Mining", "EnableMiningAOE", true, "Enable AOE mining skill changes.");
            MiningAOERange = BindServerConfig("Mining", "MiningAOERange", 2f, "How far away the mining AOE is applied. How far away an AOE hit is applied.", false, 0.5f, 10f);
            MiningAOELevel = BindServerConfig("Mining", "MiningAOELevel", 50f, "The level that AOE mining requires to activate. What skill level Mining AOE is enabled at.", false, 0f, 100f);
            EnableMiningRockBreaker = BindServerConfig("Mining", "EnableMiningRockBreaker", true, "Enable mining whole veins, by a (small) chance.");
            RockBreakerMaxChance = BindServerConfig("Mining", "RockBreakerMaxChance", 0.05f, "The maximum chance to break a whole vein. 0.05 is 5% chance to break a whole vein at level 100. This is checked on each hit.", false, 0f, 1f);
            RockBreakerRequiredLevel = BindServerConfig("Mining", "RockBreakerRequiredLevel", 75, "The level that vein breaking requires to activate. What skill level whole rocks breaking is enabled at.", false, 0, 100);
            RockBreakerDamage = BindServerConfig("Mining", "RockBreakerDamage", 300f, "Veinbreakers damage, small damage numbers will mean triggering this will not destroy a whole vein, but massively weaken it. Large numbers will ensure the whole vein is destroyed.", false, 0f, 10000f);
            MinehitsPerInterval = BindServerConfig("Mining", "MinehitsPerInterval", 2, "The number of pieces per interval to break when mining large rocks.", true, 1,100);

            EnableRun = BindServerConfig("Run", "EnableRun", true, "Enable run skill changes.");
            RunSpeedFactor = BindServerConfig("Run", "RunSpeedFactor", 0.005f, "How much the run speed is increased based on your run level. Amount applied per level, 0.005 will make level 100 run give 50% faster running.", false, 0.001f, 0.06f);

            EnableCooking = BindServerConfig("Cooking", "EnableCooking", true, "Enable cooking skill changes.");
            CookingBurnReduction = BindServerConfig("Cooking", "CookingBurnReduction", 0.5f, "How much offset is applied to diminishing returns for food, scaled by the players cooking skill. At 1 and cooking 100 food never degrades.", false, 0.1f, 1f);

            EnableBloodMagic = BindServerConfig("BloodMagic", "EnableBloodMagic", true, "Enable blood magic skill changes.");
            BloodMagicXPForShieldDamageRatio = BindServerConfig("BloodMagic", "BloodMagicXPForShieldDamageRatio", 50f, "How much XP is gained for shield damage. 50 is once every 50 damage.", false, 1f, 200f);
            BloodMagicXP = BindServerConfig("BloodMagic", "BloodMagicXP", 1f, "How much XP is gained, used by other blood magic skill settings.", false, 0.1f, 10f);

            EnableStealth = BindServerConfig("Sneak", "EnableStealth", true, "Enable sneak skill changes.");
            SneakSpeedFactor = BindServerConfig("Sneak", "SneakSpeedFactor", 0.03f, "How much sneak speed is increased based on your sneak level. Amount applied per level, 0.03 will make level 100 sneak give normal walkspeed while sneaking.", false, 0.001f, 0.06f);
            SneakNoiseReductionLevel = BindServerConfig("Sneak", "SneakNoiseReductionLevel", 50f, "The level at which noise reduction starts being applied based on your skill", false, 0f, 100f);
            SneakNoiseReductionFactor = BindServerConfig("Sneak", "SneakNoiseReductionFactor", 0.5f, "How much noise is reduced based on your sneak level. Amount applied per level, 0.5 will make level 100 sneak give 50% less noise.", false, 0.1f, 1f);

            EnableAnimalWhisper = BindServerConfig("AnimalHandling", "EnableAnimalWhisper", true, "Enable animal handling skill changes.");
            AnimalTamingSpeedFactor = BindServerConfig("AnimalHandling", "AnimalTamingSpeedFactor", 6f, "How much your animal handling skill impacts taming speed. 6 is 6x taming speed at level 100 (5 minutes vs 30 minutes default)", false, 1f, 10f);
            TamedAnimalLootIncreaseFactor = BindServerConfig("AnimalHandling", "TamedAnimalLootIncreaseFactor", 3f, "How much the animal handling skill improves your loot from tamed creatures. 3 is 3x the loot at level 100", false, 1f, 10f);
            EnableBeeBonuses = BindServerConfig("AnimalHandling", "EnableBeeBonuses", true, "Enables Animal Handling bonuses related to Bees.");
            BetterBeesLevel = BindServerConfig("AnimalHandling", "BetterBeesLevel", 15, "The level at which Bee productivity traits kick in", false, 0, 100);
            BeeHoneyIncreaseDropChance = BindServerConfig("AnimalHandling", "BeeIncreaseDropChance", 1f, "At level 100 skill, each honey gathered will be increased by this. Scaled by skill level. eg: at 1, skill level 10, a 10% chance of another honey.");
            EnableBeeBiomeUnrestricted = BindServerConfig("AnimalHandling", "EnableBeeBiomeUnrestricted", true, "At the specified level, beeshives built by you can produce honey in any biome.");
            BeeBiomeUnrestrictedLevel = BindServerConfig("AnimalHandling", "BeeBiomeUnrestrictedLevel", 25, "At this level, if enabled, beehives built by you can produce honey in any biome.", false, 0, 100);

            EnableGathering = BindServerConfig("Farming", "EnableGathering", true, "Enable gathering skill changes.");
            GatheringLuckFactor = BindServerConfig("Farming", "GatheringLuckFactor", 0.5f, "How much luck impacts gathering. Each level gives you a small chance to get better loot.", false, 0.1f, 5f);
            GatheringRangeFactor = BindServerConfig("Farming", "GatheringRangeFactor", 5f, "AOE gathering range you have at level 100.", false, 3f, 25f);
            FarmingRangeRequiredLevel = BindServerConfig("Farming", "GatheringRangeRequiredLevel", 50, "The level that AOE gathering requires to activate.", false, 0, 100);
            GatheringLuckLevels = BindServerConfig("Farming", "GatheringLuckLevels", "30,50,70,90,100", "The Luck levels that you can roll additional loot at. These should be between 0-100. But can be assigned in any way or number- such as 0,10,10,10,100.");
            GatheringDisallowedItems = BindServerConfig("Farming", "GatheringDisallowedItems", "SurtlingCore,Flint,Wood,Branch,Stone,Amber,AmberPearl,Coins,Ruby,CryptRemains,Obsidian,Crystal,Pot_Shard,DragonEgg,DvergrLantern,DvergrMineTreasure,SulfurRock,VoltureEgg,Swordpiece,MoltenCore,Hairstrands,Tar,BlackCore", "Items which can be picked, but do not get a luck roll for multiple loot and will not be auto-picked.");

            EnableVoyager = BindServerConfig("Voyager", "EnableVoyager", true, "Enable voyager skill changes.");
            VoyagerSkillXPCheckFrequency = BindServerConfig("Voyager", "VoyagerSkillXPCheckFrequency", 5, "How often Voyager skill can be increased while sailing. Rate varies based on your game physics engine speed.", false, 5, 200);
            VoyagerReduceCuttingStart = BindServerConfig("Voyager", "VoyagerReduceCuttingStart", 50f, "The level that the player starts to reduce the penalty of not having the wind at your back.", false, 0f, 100f);
            VoyagerSailingSpeedFactor = BindServerConfig("Voyager", "VoyagerSailingSpeedFactor", 1f, "How much the sailing speed is increased based on your voyager level. Amount applied per level, 2 will make level 100 voyager give 100% faster sailing.", false, 1f, 20f);
            VoyagerIncreaseExplorationRadius = BindServerConfig("Voyager", "VoyagerIncreaseExplorationRadius", 1.5f, "How much the exploration radius is increased based on your voyager level. Amount applied per level, 1 will make level 100 voyager give 100% more exploration radius.", false, 0f, 20f);
            VoyagerPaddleSpeedBonus = BindServerConfig("Voyager", "VoyagerPaddleSpeedBonus", 2f, "How much the paddle speed is increased based on your voyager level. 1 is a 100% bonus at level 100", false, 0.01f, 5f);
            VoyagerPaddleSpeedBonusLevel = BindServerConfig("Voyager", "VoyagerPaddleSpeedBonusLevel", 25f, "The level that the player starts to get a bonus to paddle speed.", false, 0f, 100f);

            EnableWeaponSkill = BindServerConfig("WeaponSkills", "EnableWeaponSkill", true, "Enable weapon skill changes.");
            WeaponSkillStaminaReduction = BindServerConfig("WeaponSkills", "WeaponSkillStaminaReduction", 0.5f, "How much stamina is reduced based on your weapon skill level at level 100. 0.5 will make level 100 weapon skill give 50% less stamina cost.", false, 0f, 1f);
            WeaponSkillParryBonus = BindServerConfig("WeaponSkills", "WeaponSkillParryBonus", 1f, "How much extra XP you get for parrying an attack", false, 0f, 10f);
            WeaponSkillBowDrawStaminaCostReduction = BindServerConfig("WeaponSkills", "WeaponSkillBowDrawStaminaCostReduction", 0.5f, "How much stamina is reduced based on your weapon skill level at level 100. 0.5 will make level 100 weapon skill give 50% less stamina cost. Vanilla is .33", false, 0f, 1f);

            EnableKnowledgeSharing = BindServerConfig("SkillRates", "EnableKnowledgeSharing", true, "Enable shared knowledge, this allows you to gain faster experiance in low skills if you already have other high skills (eg switching primary weapon skill).");
            AnimalTamingSkillGainRate = BindServerConfig("SkillRates", "AnimalTamingSkillGainRate", 1f, "How fast the skill is gained.", false, 1f, 10f);
            VoyagerSkillGainRate = BindServerConfig("SkillRates", "VoyagerSkillGainRate", 1f, "How fast the skill is gained.", false, 1f, 10f);
            SharedKnowledgeSkillBonusRate = BindServerConfig("SkillRates", "SharedKnowledgeSkillBonusRate", 1.5f, "How strong at maximum the xp bonus from shared knowledge will be when catching up skills lower than your highest.", false, 0f, 10f);
            SharedKnowledgeCap = BindServerConfig("SkillRates", "SharedKnowledgeCap", 5f, "The number of levels below your maximum skill that shared knowledge stops providing a bonus at. Eg: max skill 90, at 5 any skills 85+ will not recieve an xp bonus.", true, 0f, 50f);
            SharedKnowledgeIgnoreList = BindServerConfig("SkillRates", "SharedKnowledgeIgnoreList", "", "Comma separated list of skills to ignore when calculating shared knowledge. This is useful for skills that have vastly different XP curves or that you simply do not want an accelerated growth rate in. Invalid skill names will be ignored.");
            SharedKnowledgeIgnoreList.SettingChanged += SharedKnowledge.UnallowedSharedXPSkillTypesChanged;
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
