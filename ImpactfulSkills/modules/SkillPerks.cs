using ImpactfulSkills.modules.Multiplant;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImpactfulSkills.patches
{
    /// <summary>
    /// Every perk this mod gives a skill, shared by the unlock messages (UnlockMessages) and the compendium page that
    /// lists them (CompendiumSkillPerks).
    ///
    /// A perk with a level repeats its feature's own level check, reading the skill the same way (GetSkillLevel, or
    /// GetSkillFactor * 100) with the same arithmetic. A perk without one works from level 0 and grows with the skill.
    /// Each perk only counts while the toggles its feature checks are on. Runtime toggles such as the AOE hotkey are
    /// left out, the perk is still there whether or not it is switched on. The checks read the boosted level (see
    /// SkillCaps), so gear or a status effect can unlock a perk early.
    ///
    /// A perk's name is localized from its Name token, and its explanation from that token with "_description" added.
    /// </summary>
    internal static class SkillPerks
    {
        internal class Perk
        {
            public string Name;
            public Func<bool> Enabled;
            // The level the perk unlocks at as configured, for showing. Null for a perk that works from level 0.
            public Func<float> Level;
            // Given GetSkillLevel and GetSkillFactor for the skill, whether the perk works.
            public Func<float, float, bool> Reached;

            public string Description => Name + "_description";
        }

        // Equipping and unequipping gets faster past WeaponSkillEquipRequiredLevel for an item of any skill, so this
        // covers every skill that equippable items use. Shields use Blocking and the fishing rod uses Fishing.
        private static readonly Skills.SkillType[] EquipSkills = {
            Skills.SkillType.Swords, Skills.SkillType.Knives, Skills.SkillType.Clubs, Skills.SkillType.Polearms,
            Skills.SkillType.Spears, Skills.SkillType.Axes, Skills.SkillType.Bows, Skills.SkillType.Crossbows,
            Skills.SkillType.Unarmed, Skills.SkillType.ElementalMagic, Skills.SkillType.BloodMagic,
            Skills.SkillType.Pickaxes, Skills.SkillType.Blocking, Skills.SkillType.Fishing
        };

        // WeaponSkill.cs reduces the stamina of any attack by its item's skill. Bows have their own draw perk, and
        // bows, crossbows and magic weapons spend their stamina drawing, reloading or on eitr rather than on the attack.
        private static readonly Skills.SkillType[] AttackStaminaSkills = {
            Skills.SkillType.Swords, Skills.SkillType.Knives, Skills.SkillType.Clubs, Skills.SkillType.Polearms,
            Skills.SkillType.Spears, Skills.SkillType.Axes, Skills.SkillType.Unarmed, Skills.SkillType.Pickaxes
        };

        private static Dictionary<Skills.SkillType, List<Perk>> perks;
        // Built on first use, since this mod's own skills only get their skill types once they register in Awake.
        internal static Dictionary<Skills.SkillType, List<Perk>> BySkill => perks ??= Build();

        private static Dictionary<Skills.SkillType, List<Perk>> Build() {
            Dictionary<Skills.SkillType, List<Perk>> all = new();
            void Add(Skills.SkillType skill, string name, Func<bool> enabled, Func<float> level, Func<float, float, bool> reached) {
                if (skill == Skills.SkillType.None) { return; }
                if (!all.TryGetValue(skill, out List<Perk> list)) {
                    list = new List<Perk>();
                    all[skill] = list;
                }
                list.Add(new Perk { Name = name, Enabled = enabled, Level = level, Reached = reached });
            }
            void AddInnate(Skills.SkillType skill, string name, Func<bool> enabled) {
                Add(skill, name, enabled, null, (_, _) => true);
            }

            // Woodcutting.cs
            AddInnate(Skills.SkillType.WoodCutting, "$perk_woodcutting_damage", () => ValConfig.EnableWoodcutting.Value);
            AddInnate(Skills.SkillType.WoodCutting, "$perk_woodcutting_yield", () => ValConfig.EnableWoodcutting.Value);

            // Mining.cs ModifyPickaxeDmg and the drop multiplier
            AddInnate(Skills.SkillType.Pickaxes, "$perk_mining_damage", () => ValConfig.EnableMining.Value);
            AddInnate(Skills.SkillType.Pickaxes, "$perk_mining_yield", () => ValConfig.EnableMining.Value);
            Add(Skills.SkillType.Pickaxes, "$unlock_mining_crit",
                () => ValConfig.EnableMining.Value && ValConfig.EnableMiningCritHit.Value,
                () => ValConfig.RequiredLevelForMiningCrit.Value,
                (_, factor) => Mathf.Round(factor * 100f) >= ValConfig.RequiredLevelForMiningCrit.Value);
            Add(Skills.SkillType.Pickaxes, "$unlock_rockbreaker",
                () => ValConfig.EnableMining.Value && ValConfig.EnableMiningRockBreaker.Value,
                () => ValConfig.RockBreakerRequiredLevel.Value,
                (_, factor) => Mathf.Round(factor * 100f) >= ValConfig.RockBreakerRequiredLevel.Value);
            Add(Skills.SkillType.Pickaxes, "$unlock_aoe_mining",
                () => ValConfig.EnableMining.Value && ValConfig.EnableMiningAOE.Value,
                () => ValConfig.MiningAOELevel.Value,
                (_, factor) => Mathf.Round(factor * 100f) >= ValConfig.MiningAOELevel.Value);

            // Sneaking.cs speed, noise reduction and backstab bonus
            AddInnate(Skills.SkillType.Sneak, "$perk_sneak_speed", () => ValConfig.EnableStealth.Value);
            Add(Skills.SkillType.Sneak, "$unlock_quiet_sneak",
                () => ValConfig.EnableStealth.Value,
                () => ValConfig.SneakNoiseReductionLevel.Value,
                (level, _) => level >= ValConfig.SneakNoiseReductionLevel.Value);
            Add(Skills.SkillType.Sneak, "$unlock_backstab",
                () => ValConfig.EnableSneakBonusDamage.Value,
                () => ValConfig.SneakBackstabBonusLevel.Value,
                (level, _) => level >= ValConfig.SneakBackstabBonusLevel.Value);

            // Running.cs
            AddInnate(Skills.SkillType.Run, "$perk_run_speed", () => ValConfig.EnableRun.Value);

            // Jumping.cs
            Add(Skills.SkillType.Jump, "$unlock_jump_height",
                () => ValConfig.EnableJump.Value,
                () => ValConfig.JumpHeightRequiredLevel.Value,
                (_, factor) => factor * 100f >= ValConfig.JumpHeightRequiredLevel.Value);
            Add(Skills.SkillType.Jump, "$unlock_fall_damage",
                () => ValConfig.EnableJump.Value && ValConfig.EnableFallDamageReduction.Value,
                () => ValConfig.FallDamageReductionRequiredLevel.Value,
                (_, factor) => factor * 100f >= ValConfig.FallDamageReductionRequiredLevel.Value);
            Add(Skills.SkillType.Jump, "$unlock_safe_fall",
                () => ValConfig.EnableJump.Value && ValConfig.EnableFallDamageHeightBonus.Value,
                () => ValConfig.FallDamageHeightRequiredLevel.Value,
                (_, factor) => factor * 100f >= ValConfig.FallDamageHeightRequiredLevel.Value);

            // AnimalWhisper.cs taming speed, slaughter loot and bees, AnimalTaming.cs, AnimalBonusStar.cs
            AddInnate(AnimalWhisper.AnimalHandling, "$perk_taming_speed", () => ValConfig.EnableAnimalWhisper.Value);
            AddInnate(AnimalWhisper.AnimalHandling, "$perk_hand_feeding", () => AnimalTaming.HandFeedingEnabled);
            AddInnate(AnimalWhisper.AnimalHandling, "$perk_slaughter_loot", () => ValConfig.EnableAnimalWhisper.Value);
            Add(AnimalWhisper.AnimalHandling, "$unlock_calm_taming",
                () => ValConfig.EnableAnimalWhisper.Value && ValConfig.EnableTamingCalm.Value,
                () => ValConfig.TamingCalmLevel.Value,
                (level, _) => level >= ValConfig.TamingCalmLevel.Value);
            Add(AnimalWhisper.AnimalHandling, "$unlock_better_bees",
                () => ValConfig.EnableBeeBonuses.Value,
                () => ValConfig.BetterBeesLevel.Value,
                (level, _) => level >= ValConfig.BetterBeesLevel.Value);
            Add(AnimalWhisper.AnimalHandling, "$unlock_bees_anywhere",
                () => ValConfig.EnableBeeBiomeUnrestricted.Value,
                () => ValConfig.BeeBiomeUnrestrictedLevel.Value,
                (level, _) => level >= ValConfig.BeeBiomeUnrestrictedLevel.Value);
            Add(AnimalWhisper.AnimalHandling, "$unlock_bonus_star",
                () => ValConfig.EnableAnimalWhisper.Value && ValConfig.EnableAnimalBonusStar.Value,
                () => ValConfig.AnimalBonusStarRequiredLevel.Value,
                (_, factor) => factor * 100f >= ValConfig.AnimalBonusStarRequiredLevel.Value);

            // Gathering.cs luck and scythe reach, PlantingStamina.cs which has no toggle, PlantGrowthTime.cs,
            // Gathering.cs AOE, PlantGrid.cs, PlantBiome.cs
            AddInnate(Skills.SkillType.Farming, "$perk_farming_luck", () => ValConfig.EnableGathering.Value);
            AddInnate(Skills.SkillType.Farming, "$perk_scythe_reach", () => ValConfig.EnableGathering.Value);
            AddInnate(Skills.SkillType.Farming, "$perk_planting_stamina", () => true);
            Add(Skills.SkillType.Farming, "$unlock_growth_time",
                () => ValConfig.EnableFarmingGrowthTimeDisplay.Value,
                () => ValConfig.FarmingGrowthTimeDisplayLevel.Value,
                (level, _) => level >= ValConfig.FarmingGrowthTimeDisplayLevel.Value);
            Add(Skills.SkillType.Farming, "$unlock_aoe_harvest",
                () => ValConfig.EnableGathering.Value && ValConfig.EnableGatheringAOE.Value,
                () => ValConfig.FarmingRangeRequiredLevel.Value,
                (_, factor) => factor * 100f >= ValConfig.FarmingRangeRequiredLevel.Value);
            Add(Skills.SkillType.Farming, "$unlock_multiplant",
                () => ValConfig.EnableFarmingMultiPlant.Value && !PlantGrid.UseOtherPlantGridSystem,
                () => ValConfig.FarmingMultiplantRequiredLevel.Value,
                (level, _) => level >= ValConfig.FarmingMultiplantRequiredLevel.Value);
            Add(Skills.SkillType.Farming, "$unlock_plant_anywhere",
                () => ValConfig.EnableFarmingBiomeUnrestricted.Value,
                () => ValConfig.FarmingBiomeUnrestrictedLevel.Value,
                (level, _) => level >= ValConfig.FarmingBiomeUnrestrictedLevel.Value);

            // Voyaging.cs
            AddInnate(Voyaging.VoyagingSkill, "$perk_sail_speed", () => ValConfig.EnableVoyager.Value);
            AddInnate(Voyaging.VoyagingSkill, "$perk_crew_speed", () => ValConfig.EnableVoyager.Value && ValConfig.EnableFriendsRowSpeedBonus.Value);
            AddInnate(Voyaging.VoyagingSkill, "$perk_exploration", () => ValConfig.EnableVoyager.Value);
            Add(Voyaging.VoyagingSkill, "$unlock_paddle_speed",
                () => ValConfig.EnableVoyager.Value,
                () => ValConfig.VoyagerPaddleSpeedBonusLevel.Value,
                (level, _) => level >= ValConfig.VoyagerPaddleSpeedBonusLevel.Value);
            Add(Voyaging.VoyagingSkill, "$unlock_boat_damage",
                () => ValConfig.EnableBoatDamageReduction.Value,
                () => ValConfig.BoatDamageReductionLevel.Value,
                (level, _) => level >= ValConfig.BoatDamageReductionLevel.Value);
            Add(Voyaging.VoyagingSkill, "$unlock_sail_wind",
                () => ValConfig.EnableVoyager.Value,
                () => ValConfig.VoyagerReduceCuttingStart.Value,
                (level, _) => Voyaging.GetCuttingProgress(level) > 0f);
            Add(Voyaging.VoyagingSkill, "$unlock_impact_resist",
                () => ValConfig.VoyagerImpactResistance.Value,
                () => ValConfig.VoyagerImpactResistanceLevel.Value,
                (level, _) => level >= ValConfig.VoyagerImpactResistanceLevel.Value);

            // Hauling.cs
            AddInnate(Hauling.HaulingSkill, "$perk_carry_weight", () => ValConfig.EnableHauling.Value && ValConfig.EnableCarryWeightBonus.Value);
            AddInnate(Hauling.HaulingSkill, "$perk_cart_weight", () => ValConfig.EnableHauling.Value && ValConfig.EnableHaulingCartMassReduction.Value);

            // WeaponSkill.cs attack and draw stamina, distance XP, EquipSpeedHelper which has no toggle, and
            // ReloadPause. The reload pause name is not crossbow specific, so the magic skills share it.
            foreach (Skills.SkillType skill in AttackStaminaSkills) {
                AddInnate(skill, "$perk_attack_stamina", () => ValConfig.EnableWeaponSkill.Value);
            }
            AddInnate(Skills.SkillType.Bows, "$perk_bow_draw", () => ValConfig.EnableWeaponSkill.Value);
            AddInnate(Skills.SkillType.Bows, "$perk_distance_xp", () => ValConfig.EnableWeaponSkill.Value && ValConfig.EnableBowDistanceXP.Value);
            AddInnate(Skills.SkillType.Crossbows, "$perk_distance_xp", () => ValConfig.EnableWeaponSkill.Value && ValConfig.EnableCrossbowDistanceXP.Value);
            foreach (Skills.SkillType skill in EquipSkills) {
                Add(skill, "$unlock_quick_equip",
                    () => true,
                    () => ValConfig.WeaponSkillEquipRequiredLevel.Value,
                    (_, factor) => factor * 100f >= ValConfig.WeaponSkillEquipRequiredLevel.Value);
            }
            Add(Skills.SkillType.Crossbows, "$unlock_crossbow_pause",
                () => ValConfig.EnableWeaponSkill.Value && ValConfig.EnableCrossbowReloadPause.Value,
                () => ValConfig.CrossbowReloadPauseLevel.Value,
                (level, _) => level >= ValConfig.CrossbowReloadPauseLevel.Value);
            foreach (Skills.SkillType skill in new[] { Skills.SkillType.ElementalMagic, Skills.SkillType.BloodMagic }) {
                Add(skill, "$unlock_crossbow_pause",
                    () => ValConfig.EnableWeaponSkill.Value && ValConfig.EnableMagicReloadPause.Value,
                    () => ValConfig.MagicReloadPauseLevel.Value,
                    (level, _) => level >= ValConfig.MagicReloadPauseLevel.Value);
            }

            // Blocking.cs
            Add(Skills.SkillType.Blocking, "$unlock_block_power",
                () => ValConfig.EnableBlocking.Value,
                () => ValConfig.BlockPowerRequiredLevel.Value,
                (level, _) => level >= ValConfig.BlockPowerRequiredLevel.Value);
            Add(Skills.SkillType.Blocking, "$unlock_block_stamina",
                () => ValConfig.EnableBlocking.Value,
                () => ValConfig.BlockStaminaGainRequiredLevel.Value,
                (level, _) => level >= ValConfig.BlockStaminaGainRequiredLevel.Value);

            // Swimming.cs
            Add(Skills.SkillType.Swim, "$unlock_swim_speed",
                () => ValConfig.EnableSwimming.Value,
                () => ValConfig.SwimSpeedRequiredLevel.Value,
                (_, factor) => factor * 100 >= ValConfig.SwimSpeedRequiredLevel.Value);
            Add(Skills.SkillType.Swim, "$unlock_swim_stamina",
                () => ValConfig.EnableSwimming.Value && ValConfig.EnableSwimStaminaCostReduction.Value,
                () => ValConfig.SwimStaminaReductionLevel.Value,
                (_, factor) => factor * 100 >= ValConfig.SwimStaminaReductionLevel.Value);

            // Crafting.cs durability saves, CraftableBonus and material returns, Cooking.cs food decay, streak and stamina regen
            Add(Skills.SkillType.Crafting, "$unlock_durability_saves",
                () => ValConfig.EnableDurabilityLossPrevention.Value,
                () => ValConfig.DurabilitySaveLevel.Value,
                (_, factor) => ValConfig.DurabilitySaveLevel.Value <= Mathf.Round(factor * 100f));
            Add(Skills.SkillType.Crafting, "$unlock_bonus_crafts",
                () => ValConfig.EnableCrafting.Value && ValConfig.EnableBonusItemCrafting.Value,
                () => ValConfig.CraftingBonusCraftsLevel.Value,
                (level, _) => level >= ValConfig.CraftingBonusCraftsLevel.Value);
            Add(Skills.SkillType.Crafting, "$unlock_material_returns",
                () => ValConfig.EnableMaterialReturns.Value,
                () => ValConfig.CraftingMaterialReturnsLevel.Value,
                (level, _) => level >= ValConfig.CraftingMaterialReturnsLevel.Value);
            AddInnate(Skills.SkillType.Cooking, "$perk_food_decay", () => ValConfig.EnableCooking.Value && ValConfig.EnableCookingDegradeReduction.Value);
            AddInnate(Skills.SkillType.Cooking, "$perk_cooking_streak", () => ValConfig.EnableCooking.Value && ValConfig.EnableCookingStreak.Value);
            Add(Skills.SkillType.Cooking, "$unlock_bonus_cooking",
                () => ValConfig.EnableCrafting.Value && ValConfig.EnableCookingBonusItems.Value,
                () => ValConfig.RequiredLevelForBonusCookingItems.Value,
                (level, _) => level >= ValConfig.RequiredLevelForBonusCookingItems.Value);
            Add(Skills.SkillType.Cooking, "$unlock_food_stamina_regen",
                () => ValConfig.EnableCooking.Value && ValConfig.EnableCookingStaminaRegen.Value,
                () => ValConfig.CookingStaminaRegenRequiredLevel.Value,
                (level, _) => level >= ValConfig.CookingStaminaRegenRequiredLevel.Value);

            // Forging.cs station bonuses, ForgingRefinement.cs, ForgedItems.cs
            Add(Forging.ForgingSkill, "$unlock_station_tier1",
                () => ValConfig.EnableForging.Value && ValConfig.EnableStationBonusTier1.Value,
                () => ValConfig.StationBonusTier1Level.Value,
                (level, _) => level >= ValConfig.StationBonusTier1Level.Value);
            Add(Forging.ForgingSkill, "$unlock_station_tier2",
                () => ValConfig.EnableForging.Value && ValConfig.EnableStationBonusTier2.Value,
                () => ValConfig.StationBonusTier2Level.Value,
                (level, _) => level >= ValConfig.StationBonusTier2Level.Value);
            Add(Forging.ForgingSkill, "$unlock_refinement",
                () => ValConfig.EnableForging.Value && ValConfig.EnableRefinementBonus.Value,
                () => ValConfig.RefinementBonusLevel.Value,
                (level, _) => level >= ValConfig.RefinementBonusLevel.Value);
            Add(Forging.ForgingSkill, "$forging_masterwork",
                () => ValConfig.EnableForging.Value && ValConfig.EnableMasterwork.Value,
                () => ValConfig.MasterworkLevel.Value,
                (level, _) => level >= ValConfig.MasterworkLevel.Value);
            Add(Forging.ForgingSkill, "$forging_lightweight",
                () => ValConfig.EnableForging.Value && ValConfig.EnableLightweight.Value,
                () => ValConfig.LightweightLevel.Value,
                (level, _) => level >= ValConfig.LightweightLevel.Value);

            // BloodMagic.cs shield feature
            Add(Skills.SkillType.BloodMagic, "$unlock_shield_reset",
                () => ValConfig.EnableBloodMagic.Value && ValConfig.EnableBloodMagicShieldReset.Value,
                () => ValConfig.BloodMagicShieldResetLevel.Value,
                (level, _) => level >= ValConfig.BloodMagicShieldResetLevel.Value);

            // ElementalMagic.cs spread reduction
            Add(Skills.SkillType.ElementalMagic, "$unlock_elemental_accuracy",
                () => ValConfig.EnableElementalMagic.Value && ValConfig.EnableElementalMagicSpreadReduction.Value,
                () => ValConfig.ElementalMagicSpreadReductionLevel.Value,
                (level, _) => level >= ValConfig.ElementalMagicSpreadReductionLevel.Value);

            return all;
        }
    }
}
