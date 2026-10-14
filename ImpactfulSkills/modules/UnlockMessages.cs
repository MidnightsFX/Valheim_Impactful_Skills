using HarmonyLib;
using ImpactfulSkills.modules.Multiplant;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImpactfulSkills.patches
{
    /// <summary>
    /// Tells the local player in the middle of the screen when a skill level-up unlocks one of this mod's features,
    /// eg "Rockbreaker unlocked!". Features unlocked by the same level-up share one message, since a centre message
    /// replaces the one before it.
    ///
    /// Each unlock repeats its feature's own level check, reading the skill the same way (GetSkillLevel, or
    /// GetSkillFactor * 100) with the same arithmetic, and is only announced while the toggles that feature checks are
    /// on. Runtime toggles such as the AOE hotkey are left out, the feature is unlocked whether or not it is switched
    /// on. The checks read the boosted level (see SkillCaps), so a feature that gear or a status effect unlocks early
    /// is announced on the level-up that crosses it with that boost, and not again later.
    ///
    /// Only Skills.RaiseSkill is watched, which raises a skill at most one level per call. The raiseskill console
    /// command sets the level directly and announces nothing.
    /// </summary>
    internal static class UnlockMessages
    {
        private class Unlock
        {
            public string Name;
            public Func<bool> Enabled;
            // Given GetSkillLevel and GetSkillFactor for the skill, whether the feature works.
            public Func<float, float, bool> Reached;
        }

        // Equipping and unequipping gets faster past WeaponSkillEquipRequiredLevel for an item of any skill, so this
        // covers every skill that equippable items use. Shields use Blocking and the fishing rod uses Fishing.
        private static readonly Skills.SkillType[] EquipSkills = {
            Skills.SkillType.Swords, Skills.SkillType.Knives, Skills.SkillType.Clubs, Skills.SkillType.Polearms,
            Skills.SkillType.Spears, Skills.SkillType.Axes, Skills.SkillType.Bows, Skills.SkillType.Crossbows,
            Skills.SkillType.Unarmed, Skills.SkillType.ElementalMagic, Skills.SkillType.BloodMagic,
            Skills.SkillType.Pickaxes, Skills.SkillType.Blocking, Skills.SkillType.Fishing
        };

        private static Dictionary<Skills.SkillType, List<Unlock>> unlocks;
        // Built on first use, since this mod's own skills only get their skill types once they register in Awake.
        private static Dictionary<Skills.SkillType, List<Unlock>> Unlocks => unlocks ??= BuildUnlocks();

        private static Dictionary<Skills.SkillType, List<Unlock>> BuildUnlocks() {
            Dictionary<Skills.SkillType, List<Unlock>> all = new();
            void Add(Skills.SkillType skill, string name, Func<bool> enabled, Func<float, float, bool> reached) {
                if (skill == Skills.SkillType.None) { return; }
                if (!all.TryGetValue(skill, out List<Unlock> list)) {
                    list = new List<Unlock>();
                    all[skill] = list;
                }
                list.Add(new Unlock { Name = name, Enabled = enabled, Reached = reached });
            }

            // Mining.cs ModifyPickaxeDmg
            Add(Skills.SkillType.Pickaxes, "$unlock_mining_crit",
                () => ValConfig.EnableMining.Value && ValConfig.EnableMiningCritHit.Value,
                (_, factor) => Mathf.Round(factor * 100f) >= ValConfig.RequiredLevelForMiningCrit.Value);
            Add(Skills.SkillType.Pickaxes, "$unlock_rockbreaker",
                () => ValConfig.EnableMining.Value && ValConfig.EnableMiningRockBreaker.Value,
                (_, factor) => Mathf.Round(factor * 100f) >= ValConfig.RockBreakerRequiredLevel.Value);
            Add(Skills.SkillType.Pickaxes, "$unlock_aoe_mining",
                () => ValConfig.EnableMining.Value && ValConfig.EnableMiningAOE.Value,
                (_, factor) => Mathf.Round(factor * 100f) >= ValConfig.MiningAOELevel.Value);

            // Sneaking.cs noise reduction and backstab bonus
            Add(Skills.SkillType.Sneak, "$unlock_quiet_sneak",
                () => ValConfig.EnableStealth.Value,
                (level, _) => level >= ValConfig.SneakNoiseReductionLevel.Value);
            Add(Skills.SkillType.Sneak, "$unlock_backstab",
                () => ValConfig.EnableSneakBonusDamage.Value,
                (level, _) => level >= ValConfig.SneakBackstabBonusLevel.Value);

            // Jumping.cs
            Add(Skills.SkillType.Jump, "$unlock_jump_height",
                () => ValConfig.EnableJump.Value,
                (_, factor) => factor * 100f >= ValConfig.JumpHeightRequiredLevel.Value);
            Add(Skills.SkillType.Jump, "$unlock_fall_damage",
                () => ValConfig.EnableJump.Value && ValConfig.EnableFallDamageReduction.Value,
                (_, factor) => factor * 100f >= ValConfig.FallDamageReductionRequiredLevel.Value);
            Add(Skills.SkillType.Jump, "$unlock_safe_fall",
                () => ValConfig.EnableJump.Value && ValConfig.EnableFallDamageHeightBonus.Value,
                (_, factor) => factor * 100f >= ValConfig.FallDamageHeightRequiredLevel.Value);

            // AnimalWhisper.cs bees, AnimalBonusStar.cs
            Add(AnimalWhisper.AnimalHandling, "$unlock_better_bees",
                () => ValConfig.EnableBeeBonuses.Value,
                (level, _) => level >= ValConfig.BetterBeesLevel.Value);
            Add(AnimalWhisper.AnimalHandling, "$unlock_bees_anywhere",
                () => ValConfig.EnableBeeBiomeUnrestricted.Value,
                (level, _) => level >= ValConfig.BeeBiomeUnrestrictedLevel.Value);
            Add(AnimalWhisper.AnimalHandling, "$unlock_bonus_star",
                () => ValConfig.EnableAnimalWhisper.Value && ValConfig.EnableAnimalBonusStar.Value,
                (_, factor) => factor * 100f >= ValConfig.AnimalBonusStarRequiredLevel.Value);

            // PlantGrowthTime.cs, Gathering.cs AOE, PlantGrid.cs, PlantBiome.cs
            Add(Skills.SkillType.Farming, "$unlock_growth_time",
                () => ValConfig.EnableFarmingGrowthTimeDisplay.Value,
                (level, _) => level >= ValConfig.FarmingGrowthTimeDisplayLevel.Value);
            Add(Skills.SkillType.Farming, "$unlock_aoe_harvest",
                () => ValConfig.EnableGathering.Value && ValConfig.EnableGatheringAOE.Value,
                (_, factor) => factor * 100f >= ValConfig.FarmingRangeRequiredLevel.Value);
            Add(Skills.SkillType.Farming, "$unlock_multiplant",
                () => ValConfig.EnableFarmingMultiPlant.Value && !PlantGrid.UseOtherPlantGridSystem,
                (level, _) => level >= ValConfig.FarmingMultiplantRequiredLevel.Value);
            Add(Skills.SkillType.Farming, "$unlock_plant_anywhere",
                () => ValConfig.EnableFarmingBiomeUnrestricted.Value,
                (level, _) => level >= ValConfig.FarmingBiomeUnrestrictedLevel.Value);

            // Voyaging.cs
            Add(Voyaging.VoyagingSkill, "$unlock_paddle_speed",
                () => ValConfig.EnableVoyager.Value,
                (level, _) => level >= ValConfig.VoyagerPaddleSpeedBonusLevel.Value);
            Add(Voyaging.VoyagingSkill, "$unlock_boat_damage",
                () => ValConfig.EnableBoatDamageReduction.Value,
                (level, _) => level >= ValConfig.BoatDamageReductionLevel.Value);
            Add(Voyaging.VoyagingSkill, "$unlock_sail_wind",
                () => ValConfig.EnableVoyager.Value,
                (level, _) => Voyaging.GetCuttingProgress(level) > 0f);
            Add(Voyaging.VoyagingSkill, "$unlock_impact_resist",
                () => ValConfig.VoyagerImpactResistance.Value,
                (level, _) => level >= ValConfig.VoyagerImpactResistanceLevel.Value);

            // WeaponSkill.cs EquipSpeedHelper, which has no toggle, and CrossbowReloadPause
            foreach (Skills.SkillType skill in EquipSkills) {
                Add(skill, "$unlock_quick_equip",
                    () => true,
                    (_, factor) => factor * 100f >= ValConfig.WeaponSkillEquipRequiredLevel.Value);
            }
            Add(Skills.SkillType.Crossbows, "$unlock_crossbow_pause",
                () => ValConfig.EnableWeaponSkill.Value && ValConfig.EnableCrossbowReloadPause.Value,
                (level, _) => level >= ValConfig.CrossbowReloadPauseLevel.Value);

            // Blocking.cs
            Add(Skills.SkillType.Blocking, "$unlock_block_power",
                () => ValConfig.EnableBlocking.Value,
                (level, _) => level >= ValConfig.BlockPowerRequiredLevel.Value);
            Add(Skills.SkillType.Blocking, "$unlock_block_stamina",
                () => ValConfig.EnableBlocking.Value,
                (level, _) => level >= ValConfig.BlockStaminaGainRequiredLevel.Value);

            // Swimming.cs
            Add(Skills.SkillType.Swim, "$unlock_swim_speed",
                () => ValConfig.EnableSwimming.Value,
                (_, factor) => factor * 100 >= ValConfig.SwimSpeedRequiredLevel.Value);
            Add(Skills.SkillType.Swim, "$unlock_swim_stamina",
                () => ValConfig.EnableSwimming.Value && ValConfig.EnableSwimStaminaCostReduction.Value,
                (_, factor) => factor * 100 >= ValConfig.SwimStaminaReductionLevel.Value);

            // Crafting.cs durability saves, CraftableBonus and material returns
            Add(Skills.SkillType.Crafting, "$unlock_durability_saves",
                () => ValConfig.EnableDurabilityLossPrevention.Value,
                (_, factor) => ValConfig.DurabilitySaveLevel.Value <= Mathf.Round(factor * 100f));
            Add(Skills.SkillType.Crafting, "$unlock_bonus_crafts",
                () => ValConfig.EnableCrafting.Value && ValConfig.EnableBonusItemCrafting.Value,
                (level, _) => level >= ValConfig.CraftingBonusCraftsLevel.Value);
            Add(Skills.SkillType.Crafting, "$unlock_material_returns",
                () => ValConfig.EnableMaterialReturns.Value,
                (level, _) => level >= ValConfig.CraftingMaterialReturnsLevel.Value);
            Add(Skills.SkillType.Cooking, "$unlock_bonus_cooking",
                () => ValConfig.EnableCrafting.Value && ValConfig.EnableCookingBonusItems.Value,
                (level, _) => level >= ValConfig.RequiredLevelForBonusCookingItems.Value);

            // Forging.cs station bonuses, ForgingRefinement.cs, ForgedItems.cs
            Add(Forging.ForgingSkill, "$unlock_station_tier1",
                () => ValConfig.EnableForging.Value && ValConfig.EnableStationBonusTier1.Value,
                (level, _) => level >= ValConfig.StationBonusTier1Level.Value);
            Add(Forging.ForgingSkill, "$unlock_station_tier2",
                () => ValConfig.EnableForging.Value && ValConfig.EnableStationBonusTier2.Value,
                (level, _) => level >= ValConfig.StationBonusTier2Level.Value);
            Add(Forging.ForgingSkill, "$unlock_refinement",
                () => ValConfig.EnableForging.Value && ValConfig.EnableRefinementBonus.Value,
                (level, _) => level >= ValConfig.RefinementBonusLevel.Value);
            Add(Forging.ForgingSkill, "$forging_masterwork",
                () => ValConfig.EnableForging.Value && ValConfig.EnableMasterwork.Value,
                (level, _) => level >= ValConfig.MasterworkLevel.Value);
            Add(Forging.ForgingSkill, "$forging_lightweight",
                () => ValConfig.EnableForging.Value && ValConfig.EnableLightweight.Value,
                (level, _) => level >= ValConfig.LightweightLevel.Value);

            // BloodMagic.cs shield feature
            Add(Skills.SkillType.BloodMagic, "$unlock_shield_reset", 
                () => ValConfig.EnableBloodMagic.Value && ValConfig.EnableBloodMagicShieldReset.Value,
                (level, _) => level >= ValConfig.BloodMagicShieldResetLevel.Value);

            return all;
        }

        [HarmonyPatch(typeof(Skills), nameof(Skills.RaiseSkill))]
        public static class AnnounceUnlocks
        {
            private static void Prefix(Skills __instance, Skills.SkillType skillType, out (float level, float factor)? __state) {
                __state = null;
                if (!ValConfig.EnableUnlockMessages.Value || !ValConfig.ShowUnlockMessages.Value) { return; }
                if (__instance.m_player == null || __instance.m_player != Player.m_localPlayer || !Unlocks.ContainsKey(skillType)) { return; }
                __state = Read(__instance.m_player, skillType);
            }

            private static void Postfix(Skills __instance, Skills.SkillType skillType, (float level, float factor)? __state) {
                if (__state == null || __instance.m_player == null) { return; }
                (float level, float factor) before = __state.Value;
                (float level, float factor) after = Read(__instance.m_player, skillType);
                if (after == before) { return; }

                List<string> lines = new();
                foreach (Unlock unlock in Unlocks[skillType]) {
                    if (!unlock.Enabled() || unlock.Reached(before.level, before.factor) || !unlock.Reached(after.level, after.factor)) { continue; }
                    lines.Add(LocalizationManager.Instance.TryTranslate("$unlock_message").Replace("{0}", LocalizationManager.Instance.TryTranslate(unlock.Name)));
                }
                if (lines.Count > 0) {
                    __instance.m_player.Message(MessageHud.MessageType.Center, string.Join("\n", lines));
                }
            }

            private static (float level, float factor) Read(Player player, Skills.SkillType skillType) {
                return (player.GetSkillLevel(skillType), player.GetSkillFactor(skillType));
            }
        }
    }
}
