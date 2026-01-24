using BepInEx;
using BepInEx.Configuration;
using ImpactfulSkills.patches;
using System.IO;

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
        public static ConfigEntry<float> ChanceForAOEOnHit;
        public static ConfigEntry<bool> SkillLevelBonusEnabledForMiningDropChance;
        public static ConfigEntry<bool> ChanceForAOEOnHitScalesWithSkill;
        public static ConfigEntry<bool> EnableMiningCritHit;
        public static ConfigEntry<int> RequiredLevelForMiningCrit;
        public static ConfigEntry<float> ChanceForMiningCritHit;
        public static ConfigEntry<float> CriticalHitDmgMult;
        public static ConfigEntry<bool> SkipNonRockDropIncreases;
        public static ConfigEntry<bool> ReducedChanceDropsForLowAmountDrops;
        public static ConfigEntry<float> DistanceMiningDropMultiplierChecks;
        public static ConfigEntry<float> RockbreakerSafetyResetTimeout;
        public static ConfigEntry<string> SkipNonRockDropPrefabs;
        public static ConfigEntry<bool> FractionalDropsAsChance;

        public static ConfigEntry<bool> EnableStealth;
        public static ConfigEntry<float> SneakSpeedFactor;
        public static ConfigEntry<float> SneakNoiseReductionLevel;
        public static ConfigEntry<float> SneakNoiseReductionFactor;
        public static ConfigEntry<bool> EnableSneakBonusDamage;
        public static ConfigEntry<int> SneakBackstabBonusLevel;
        public static ConfigEntry<float> SneakBackstabBonusFactor;

        public static ConfigEntry<bool> EnableRun;
        public static ConfigEntry<float> RunSpeedFactor;

        public static ConfigEntry<bool> EnableAnimalWhisper;
        public static ConfigEntry<float> AnimalTamingSpeedFactor;
        public static ConfigEntry<float> TamedAnimalLootIncreaseFactor;
        public static ConfigEntry<int> BetterBeesLevel;
        public static ConfigEntry<bool> EnableBeeBonuses;
        public static ConfigEntry<int> BeeBiomeUnrestrictedLevel;
        public static ConfigEntry<bool> EnableBeeBiomeUnrestricted;
        public static ConfigEntry<float> BeeHoneyOutputIncreaseBySkill;
        public static ConfigEntry<float> BeeHarvestXP;

        public static ConfigEntry<bool> EnableGathering;
        public static ConfigEntry<bool> EnableGatheringAOE;
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

        public static ConfigEntry<bool> EnableCrafting;
        public static ConfigEntry<bool> EnableDurabilitySaves;
        public static ConfigEntry<bool> ScaleDurabilitySaveBySkillLevel;
        public static ConfigEntry<bool> EnableDurabilityLossPrevention;
        public static ConfigEntry<int> DurabilitySaveLevel;
        public static ConfigEntry<float> ChanceForDurabilityLossPrevention;
        public static ConfigEntry<int> CraftingMaxBonus;
        public static ConfigEntry<float> CraftingBonusChance;
        public static ConfigEntry<bool> EnableBonusItemCrafting;
        public static ConfigEntry<bool> EnableCraftBonusAsFraction;
        public static ConfigEntry<float> CraftBonusFractionOfCraftNumber;
        public static ConfigEntry<int> CraftingBonusCraftsLevel;
        public static ConfigEntry<bool> EnableMaterialReturns;
        public static ConfigEntry<int> CraftingMaterialReturnsLevel;
        public static ConfigEntry<float> MaxCraftingMaterialReturnPercent;
        public static ConfigEntry<float> ChanceForMaterialReturn;

        public static ConfigEntry<bool> EnableSwimming;
        public static ConfigEntry<int> SwimSpeedRequiredLevel;
        public static ConfigEntry<float> SwimmingSpeedFactor;
        public static ConfigEntry<bool> EnableSwimStaminaCostReduction;
        public static ConfigEntry<int> SwimStaminaReductionLevel;
        public static ConfigEntry<float> SwimStaminaCostReductionFactor;

        public ValConfig(ConfigFile cf)
        {
            // ensure all the config values are created
            cfg = cf;
            cfg.SaveOnConfigSet = true;
            CreateConfigValues(cf);
            Logger.setDebugLogging(EnableDebugMode.Value);
            SetupMainFileWatcher();
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
            EnableMiningCritHit = BindServerConfig("Mining", "EnableMiningCritHit", true, "Enables mining critial hit strikes, must hit the required level for it to be active.");
            RequiredLevelForMiningCrit = BindServerConfig("Mining", "RequiredLevelForMiningCrit", 25, "Level the player must be for critical mining hits to activate", false, 0, 100);
            ChanceForMiningCritHit = BindServerConfig("Mining", "ChanceForMiningCritHit", 0.1f, "Chance for a critical hit when mining", valmax: 1f);
            CriticalHitDmgMult = BindServerConfig("Mining", "CriticalHitDmgMult", 3f, "Multipler for damage from a critical hit", valmin: 1f, valmax: 20f);
            MiningLootFactor = BindServerConfig("Mining", "MiningLootFactor", 2f, "How much the mining skill provides additional loot. 2 is 2x the loot at level 100.");
            EnableMiningAOE = BindServerConfig("Mining", "EnableMiningAOE", true, "Enable AOE mining skill changes.");
            ChanceForAOEOnHit = BindServerConfig("Mining", "ChanceForAOEOnHit", 0.3f, "Once AOE Mining is enabled, this is the chance for it to activate on any hit", valmax: 1f);
            ChanceForAOEOnHitScalesWithSkill = BindServerConfig("Mining", "ChanceForAOEOnHitScalesWithSkill", true, "Increases your chance for an AOE strike based on player skill");
            MiningAOERange = BindServerConfig("Mining", "MiningAOERange", 2f, "How far away the mining AOE is applied. How far away an AOE hit is applied.", valmin: 0.5f, valmax: 10f);
            MiningAOELevel = BindServerConfig("Mining", "MiningAOELevel", 50f, "The level that AOE mining requires to activate. What skill level Mining AOE is enabled at.", valmax: 100f);
            EnableMiningRockBreaker = BindServerConfig("Mining", "EnableMiningRockBreaker", true, "Enable mining whole veins, by a (small) chance.");
            RockBreakerMaxChance = BindServerConfig("Mining", "RockBreakerMaxChance", 0.05f, "The maximum chance to break a whole vein. 0.05 is 5% chance to break a whole vein at level 100. This is checked on each hit.", valmax: 1f);
            RockBreakerRequiredLevel = BindServerConfig("Mining", "RockBreakerRequiredLevel", 75, "The level that vein breaking requires to activate. What skill level whole rocks breaking is enabled at.", false, 0, 100);
            RockBreakerDamage = BindServerConfig("Mining", "RockBreakerDamage", 300f, "Veinbreakers damage, small damage numbers will mean triggering this will not destroy a whole vein, but massively weaken it. Large numbers will ensure the whole vein is destroyed.", valmax: 10000f);
            MinehitsPerInterval = BindServerConfig("Mining", "MinehitsPerInterval", 2, "The number of pieces per interval to break when mining large rocks.", true, 1, 100);
            SkillLevelBonusEnabledForMiningDropChance = BindServerConfig("Mining", "SkillLevelBonusEnabledForMiningDropChance", false, "Pickaxes skill level provides a bonus to drop chance for drops that are not gaurenteed (This can significantly increase muddy scrap-pile drops).");
            SkipNonRockDropIncreases = BindServerConfig("Mining", "SkipNonRockDropIncreases", true, "When enabled, only ores/rocks will get the increased drops, this primarily impacts muddy scrap piles in vanilla.");
            SkipNonRockDropPrefabs = BindServerConfig("Mining", "SkipNonRockDropPrefabs", "LeatherScraps,WitheredBone", "List of prefabs which will not recieve increased mining drops. Should be comma seperated without spaces.");
            ReducedChanceDropsForLowAmountDrops = BindServerConfig("Mining", "ReducedChanceDropsForLowAmountDrops", false, "When Enabled, drops that have an amount increase below 1 will only have a chance to happen instead of being rounded up to 1, and always happening.");
            FractionalDropsAsChance = BindServerConfig("Mining", "FractionalDropsAsChance", true, "When enabled, drops that are less than 1 become a chance to drop one. When disabled drops below 1 will never result in drops.");
            DistanceMiningDropMultiplierChecks = BindServerConfig("Mining", "DistanceMiningDropMultiplierChecks", 20f, "How far away the loot multiplier will check when rocks are destroyed. Increasing this significantly can cause a performance impact.", true, 10, 100);
            RockbreakerSafetyResetTimeout = BindServerConfig("Mining", "RockbreakerSafetyResetTimeout", 30f, "How long to wait before re-enabling rock breaker after its last activation.", true, 10f, 120f);

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
            EnableSneakBonusDamage = BindServerConfig("Sneak", "EnableSneakBonusDamage", true, "Enable sneak bonus damage changes.");
            SneakBackstabBonusLevel = BindServerConfig("Sneak", "SneakBackstabBonusLevel", 25, "The level at which backstab damage starts being applied based on your skill", false, 0, 100);
            SneakBackstabBonusFactor = BindServerConfig("Sneak", "SneakBackstabBonusFactor", 2f, "How much backstab damage is increased based on your sneak level. 1 is a 100% bonus backstab damage at skill level 100.", valmin: 0.1f, valmax: 10f);

            EnableAnimalWhisper = BindServerConfig("AnimalHandling", "EnableAnimalWhisper", true, "Enable animal handling skill changes.");
            AnimalTamingSpeedFactor = BindServerConfig("AnimalHandling", "AnimalTamingSpeedFactor", 6f, "How much your animal handling skill impacts taming speed. 6 is 6x taming speed at level 100 (5 minutes vs 30 minutes default)", false, 1f, 10f);
            TamedAnimalLootIncreaseFactor = BindServerConfig("AnimalHandling", "TamedAnimalLootIncreaseFactor", 3f, "How much the animal handling skill improves your loot from tamed creatures. 3 is 3x the loot at level 100", false, 1f, 10f);
            EnableBeeBonuses = BindServerConfig("AnimalHandling", "EnableBeeBonuses", true, "Enables Animal Handling bonuses related to Bees.");
            BetterBeesLevel = BindServerConfig("AnimalHandling", "BetterBeesLevel", 15, "The level at which Bee productivity traits kick in", false, 0, 100);
            BeeHoneyOutputIncreaseBySkill = BindServerConfig("AnimalHandling", "BeeHoneyOutputIncreaseBySkill", 1f, "At level 100 skill, and 1.0 this results in a 100% increase in honey gathered.");
            EnableBeeBiomeUnrestricted = BindServerConfig("AnimalHandling", "EnableBeeBiomeUnrestricted", true, "At the specified level, beeshives built by you can produce honey in any biome.");
            BeeBiomeUnrestrictedLevel = BindServerConfig("AnimalHandling", "BeeBiomeUnrestrictedLevel", 25, "At this level, if enabled, beehives built by you can produce honey in any biome.", false, 0, 100);
            BeeHarvestXP = BindServerConfig("AnimalHandling", "BeeHarvestXP", 2f, "The amount of xp for Animal handling provided by harvesting a single honey from a beehive", false, 0f, 20f);

            EnableGathering = BindServerConfig("Farming", "EnableGathering", true, "Enable gathering skill changes.");
            GatheringLuckFactor = BindServerConfig("Farming", "GatheringLuckFactor", 0.5f, "How much luck impacts gathering. Each level gives you a small chance to get better loot.", false, 0.1f, 5f);
            EnableGatheringAOE = BindServerConfig("Farming", "EnableGatheringAOE", true, "Enable AOE gathering skill changes.");
            GatheringRangeFactor = BindServerConfig("Farming", "GatheringRangeFactor", 5f, "AOE gathering range you have at level 100.", false, 3f, 25f);
            FarmingRangeRequiredLevel = BindServerConfig("Farming", "GatheringRangeRequiredLevel", 50, "The level that AOE gathering requires to activate.", false, 0, 100);
            GatheringLuckLevels = BindServerConfig("Farming", "GatheringLuckLevels", "30,50,70,90,100", "Higher values have a lower chance of dropping. Each comma seperated number entry (0-100) is a chance at an additional drop.");
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

            EnableCrafting = BindServerConfig("Crafting", "EnableCrafting", true, "Enable crafting skill changes.");
            EnableDurabilityLossPrevention = BindServerConfig("Crafting", "EnableDurabilityLossPrevention", true, "Enables durability reduction prevention that can scale with player skill.");
            EnableDurabilitySaves = BindServerConfig("Crafting", "EnableDurabilitySaves", true, "Enables reducing how often you use durability for your items.");
            DurabilitySaveLevel = BindServerConfig("Crafting", "DurabilitySaveLevel", 25, "Level requirement to enable durability saves.", false, 0, 150);
            ChanceForDurabilityLossPrevention = BindServerConfig("Crafting", "ChanceForDurabilityLossPrevention", 0.25f, "Chance that you will not use durability on use.", valmax: 1f);
            ScaleDurabilitySaveBySkillLevel = BindServerConfig("Crafting", "ScaleDurabilitySaveBySkillLevel", true, "Reduces MaxChanceForDurabilityPreserveOnUse based on level, at lvl 50 crafting MaxChanceForDurabilityPreserveOnUse is 50% of its value.");
            EnableBonusItemCrafting = BindServerConfig("Crafting", "EnableBonusItemCrafting", true, "Enables crafting bonus items.");
            CraftingBonusCraftsLevel = BindServerConfig("Crafting", "CraftingBonusCraftsLevel", 50, "The level at which you can start getting bonus crafts.", false, 0, 100);
            CraftingMaxBonus = BindServerConfig("Crafting", "CraftingMaxBonus", 3, "The maximum number of additional bonus crafts you can get from crafting an item", false, 0, 150);
            CraftingBonusChance = BindServerConfig("Crafting", "CraftingBonusChance", 0.3f, "The chance to get a bonus craft when crafting an item. 0.5 is a 50% chance to get a bonus craft at level 100. Bonus crafting success can stack up to the CraftingMaxBonus times.", false, 0, 1f);
            EnableCraftBonusAsFraction = BindServerConfig("Crafting", "EnableCraftBonusAsFraction", true, "Enable crafting bonus as a fraction of the number crafted (for recipes where the result is more than 1). If disabled, the bonus is always 1 item.");
            CraftBonusFractionOfCraftNumber = BindServerConfig("Crafting", "CraftBonusFractionOfCraftNumber", 0.25f, "If the number of items crafted by the recipe is greater than 1, a percentage of the number crafted is used for the bonus. This determines that percentage. Eg: craft 20 arrows (1 craft) a .25 value would give you 5 arrows for 1 bonus craft.");
            EnableMaterialReturns = BindServerConfig("Crafting", "EnableMaterialReturns", true, "Enable material returns from crafting.");
            CraftingMaterialReturnsLevel = BindServerConfig("Crafting", "CraftingMaterialReturnsLevel", 75, "The level at which material returns start being applied based on your skill", false, 0, 100);
            MaxCraftingMaterialReturnPercent = BindServerConfig("Crafting", "MaxCraftingMaterialReturnPercent", 0.3f, "The maximum percentage of materials that can be returned from crafting. 0.5 is 50% at level 100.", valmax: 1f);
            ChanceForMaterialReturn = BindServerConfig("Crafting", "ChanceForMaterialReturn", 0.15f, "The chance to return materials when crafting an item. 0.25 is a 25% chance to return materials at level 100.", valmax: 1f);
            EnableCooking = BindServerConfig("Cooking", "EnableCooking", true, "Enable cooking skill changes.");
            CookingBurnReduction = BindServerConfig("Cooking", "CookingBurnReduction", 0.5f, "How much offset is applied to diminishing returns for food, scaled by the players cooking skill. At 1 and cooking 100 food never degrades.", valmin: 0.1f, valmax: 1f);

            EnableKnowledgeSharing = BindServerConfig("SkillRates", "EnableKnowledgeSharing", true, "Enable shared knowledge, this allows you to gain faster experiance in low skills if you already have other high skills (eg switching primary weapon skill).");
            AnimalTamingSkillGainRate = BindServerConfig("SkillRates", "AnimalTamingSkillGainRate", 1f, "How fast the skill is gained.", false, 1f, 10f);
            VoyagerSkillGainRate = BindServerConfig("SkillRates", "VoyagerSkillGainRate", 1f, "How fast the skill is gained.", false, 1f, 10f);
            SharedKnowledgeSkillBonusRate = BindServerConfig("SkillRates", "SharedKnowledgeSkillBonusRate", 1.5f, "How strong at maximum the xp bonus from shared knowledge will be when catching up skills lower than your highest.", false, 0f, 10f);
            SharedKnowledgeCap = BindServerConfig("SkillRates", "SharedKnowledgeCap", 5f, "The number of levels below your maximum skill that shared knowledge stops providing a bonus at. Eg: max skill 90, at 5 any skills 85+ will not recieve an xp bonus.", true, 0f, 50f);
            SharedKnowledgeIgnoreList = BindServerConfig("SkillRates", "SharedKnowledgeIgnoreList", "", "Comma separated list of skills to ignore when calculating shared knowledge. This is useful for skills that have vastly different XP curves or that you simply do not want an accelerated growth rate in. Invalid skill names will be ignored.");
            SharedKnowledgeIgnoreList.SettingChanged += SharedKnowledge.UnallowedSharedXPSkillTypesChanged;

            EnableSwimming = BindServerConfig("Swimming", "EnableSwimming", true, "Enable swimming skill changes.");
            EnableSwimStaminaCostReduction = BindServerConfig("Swimming", "EnableSwimStaminaCostReduction", true, "Enables swim stamina cost reduction, at the level specified by SwimStaminaReductionLevel.");
            SwimSpeedRequiredLevel = BindServerConfig("Swimming", "SwimSpeedRequiredLevel", 25, "The level that swimming speed increases start being applied based on your skill", false, 0, 100);
            SwimmingSpeedFactor = BindServerConfig("Swimming", "SwimmingSpeedFactor", 3.0f, "How much swimming speed is increased based on your swimming level. This is modified by your characters swimming level. At skill level 100 the full value is in effect.", false, 0.1f, 10f);
            SwimStaminaReductionLevel = BindServerConfig("Swimming", "SwimStaminaReductionLevel", 50, "The level that swim stamina cost reductions start being applied based on your skill", false, 0, 100);
            SwimStaminaCostReductionFactor = BindServerConfig("Swimming", "SwimStaminaCostReductionFactor", 0.5f, "How much swim stamina cost is reduced based on your swimming level. This is modified by your characters swimming level. At skill level 100 the full value is in effect.", false, 0.1f, 1f);
        }

        internal static void SetupMainFileWatcher() {
            // Setup a file watcher to detect changes to the config file
            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.Path = Path.GetDirectoryName(cfg.ConfigFilePath);
            // Ignore changes to other files
            watcher.Filter = "MidnightsFX.ImpactfulSkills.cfg";
            watcher.Changed += OnConfigFileChanged;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private static void OnConfigFileChanged(object sender, FileSystemEventArgs e) {
            // We only want the config changes being allowed if this is a server (ie in game in a hosted world or dedicated ideally)
            if (ZNet.instance.IsServer() == false) {
                return;
            }
            // Handle the config file change event
            Logger.LogInfo("Configuration file has been changed, reloading settings.");
            cfg.Reload();
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
