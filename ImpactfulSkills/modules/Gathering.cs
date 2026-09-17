using HarmonyLib;
using ImpactfulSkills.common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using static Player;

namespace ImpactfulSkills.patches
{
    public static class Gathering
    {
        private static readonly int pickableMask = LayerMask.GetMask("piece_nonsolid", "item", "Default_small");
        static readonly List<String> UnallowedPickables = new List<String>() { };
        private static List<float> luck_levels = new List<float> { };
        private static bool enabled_aoe_gathering = true;
        private static float aoe_gathering_activated_at = 0;


        private static void PickableLuckLevelsChanged(object s, EventArgs e)
        {
            try {
                List<float> tluck_levels = new List<float> { };
                foreach (var item in ValConfig.GatheringLuckLevels.Value.Split(',')) {
                    tluck_levels.Add(float.Parse(item));
                }
                if (tluck_levels.Count > 0) {
                    luck_levels = tluck_levels;
                }
            }
            catch (Exception ex) {
                Logger.LogWarning($"Error parsing GatheringLuckLevels: {ex}");
            }
        }

        private static void UnallowedPickablesChanged(object s, EventArgs e) {
            try {
                List<String> tunallowed = new List<String>() { };
                foreach (var item in ValConfig.GatheringDisallowedItems.Value.Split(',')) {
                    tunallowed.Add(item);
                }
                if (tunallowed.Count > 0) {
                    UnallowedPickables.Clear();
                    UnallowedPickables.AddRange(tunallowed);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error parsing GatheringDisallowedItems: {ex}");
            }
        }

        public static void SetupGatherables() {
            try {
                foreach (var item in ValConfig.GatheringLuckLevels.Value.Split(',')) {
                    luck_levels.Add(float.Parse(item));
                }
            } catch (Exception ex) {
                Logger.LogWarning($"Error parsing GatheringLuckLevels, defaults will be used: {ex}");
                luck_levels.AddRange(new List<float>() { 30, 50, 70, 90, 100 });
            }

            ValConfig.GatheringLuckLevels.SettingChanged += PickableLuckLevelsChanged;
            try {
                foreach (var unallowed in ValConfig.GatheringDisallowedItems.Value.Split(',')) {
                    UnallowedPickables.Add(unallowed);
                }
            } catch (Exception ex) {
                Logger.LogWarning($"Error parsing GatheringDisallowedItems, defaults will be used.: {ex}");
                UnallowedPickables.AddRange(new List<string>() {
                    "SurtlingCore",
                    "Flint",
                    "Wood",
                    "Branch",
                    "Stone",
                    "Amber",
                    "AmberPearl",
                    "Coins",
                    "Ruby",
                    "CryptRemains",
                    "Obsidian",
                    "Crystal",
                    "Pot_Shard",
                    "DragonEgg",
                    "DvergrLantern",
                    "DvergrMineTreasure",
                    "SulfurRock",
                    "VoltureEgg",
                    "Swordpiece",
                    "MoltenCore",
                    "Hairstrands",
                    "Tar",
                    "BlackCore" });
            }
            ValConfig.GatheringDisallowedItems.SettingChanged += UnallowedPickablesChanged;
        }

        [HarmonyPatch(typeof(Pickable))]
        public static class DisableVanillaGatheringLuck
        {
            //[HarmonyEmitIL("./dumps")]
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(Pickable.Interact))]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions , ILGenerator generator)
            {
                var codeMatcher = new CodeMatcher(instructions, generator);
                if (!codeMatcher.TryMatchStartForward("Unable remove vanilla pickable luckydrop.",
                        new CodeMatch(OpCodes.Ldc_I4_0),
                        new CodeMatch(OpCodes.Stloc_0), // int bonus_num = 0;
                        new CodeMatch(OpCodes.Ldarg_0),
                        new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Pickable), nameof(Pickable.m_picked)))
                    )) {
                    return codeMatcher.Instructions();
                }
                int blockStart = codeMatcher.Pos;

                // DetermineExtraDrops stands in for vanilla's whole picked/skill/bonus block - the harvest stats, the
                // bonus text, the effect and the skill XP included - so we branch past all of it and land where vanilla
                // lands when its own bonus roll fails. That used to be a fixed "59 instructions ahead", which 1.0 turned
                // into a jump into the middle of the ShowText argument list.
                if (!codeMatcher.TryMatchStartForward("Unable remove vanilla pickable luckydrop.",
                        new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Pickable), nameof(Pickable.m_maxLevelBonusChance))),
                        new CodeMatch(OpCodes.Mul),
                        new CodeMatch(instr => instr.opcode == OpCodes.Bge_Un || instr.opcode == OpCodes.Bge_Un_S)
                    )) {
                    return codeMatcher.Instructions();
                }
                if (!(codeMatcher.InstructionAt(2).operand is Label afterVanillaBonus)) {
                    Logger.LogWarning("Unable remove vanilla pickable luckydrop. Vanilla's bonus roll no longer branches to a label. Skipping this patch.");
                    return codeMatcher.Instructions();
                }

                codeMatcher.Start().Advance(blockStart + 2).Insert(
                    new CodeInstruction(OpCodes.Ldarg_0), // Load the instance class
                    new CodeInstruction(OpCodes.Ldarg_1), // Load the character picking it
                    Transpilers.EmitDelegate(DetermineExtraDrops),
                    new CodeInstruction(OpCodes.Stloc_0),
                    new CodeInstruction(OpCodes.Br, afterVanillaBonus));

                return codeMatcher.Instructions();
            }

            static int DetermineExtraDrops(Pickable __instance, Humanoid character)
            {
                if (Player.m_localPlayer == null || __instance.m_picked == true || __instance.m_itemPrefab == null) { return 0; }
                if (UnallowedPickables.Contains(__instance.m_itemPrefab.name)) {
                    Logger.LogDebug($"Pickable is not an allowed gathering item.");
                    RecordHarvest(__instance, character, 0);
                    return 0;
                }
                // Increase item drops based on luck, and the gathering skill
                float skill_influence = 0.5f * (Player.m_localPlayer.GetSkillLevel(Skills.SkillType.Farming));
                float luck_roll = UnityEngine.Random.Range(0f, 50f);
                float full_roll = luck_roll + skill_influence;
                int extra_drops = 0;
                Logger.LogDebug($"Gathering roll: luck: {luck_roll} skill: {skill_influence}");
                foreach (var level in luck_levels) {
                    if (full_roll > level) {
                        extra_drops += 1;
                    } else { 
                        break;
                    }
                    Logger.LogDebug($"Gathering Luck roll: {full_roll} > {level}");
                }
                Logger.LogDebug($"Gathering Luck, drop total: {extra_drops}");
                //Create the lucky effect to show that the player got extra drops
                if (extra_drops > 0) {
                    Vector3 spawnp = __instance.transform.position + Vector3.up * __instance.m_spawnOffset;
                    Logger.LogDebug($"Spawning extra drops {extra_drops}");
                    // Show bonus text amount
                    DamageText.instance.ShowText(DamageText.TextType.Bonus, __instance.transform.position + Vector3.up * __instance.m_spawnOffset, $"+{extra_drops}", player: true);
                    __instance.m_bonusEffect.Create(spawnp, Quaternion.identity);
                    //for (int i = 0; i < extra_drops; i++)
                    //{
                    //    __instance.m_bonusEffect.Create(spawnp, Quaternion.identity);
                    //    UnityEngine.Object.Instantiate(__instance.m_itemPrefab, spawnp, __instance.transform.rotation);
                    //}
                }

                RecordHarvest(__instance, character, extra_drops);

                // Gain a little XP for the skill
                Player.m_localPlayer.RaiseSkill(Skills.SkillType.Farming, (1 + extra_drops));
                return extra_drops;
            }

            // Vanilla's harvest stats sit inside the block the transpiler skips, so they are recorded here instead.
            // Luck drops count as extra harvests, so they progress harvesting achievements along with the pick itself.
            static void RecordHarvest(Pickable pickable, Humanoid character, int extraDrops)
            {
                // Vanilla's guard: only a player's first pick of this pickable counts, not repeat interacts.
                if (!(character is Player) || pickable.m_pickedLocal) { return; }

                int harvested = 1 + extraDrops;
                if (pickable.m_harvestStat != PlayerStatType.None) {
                    Game.instance.IncrementPlayerStat(pickable.m_harvestStat, harvested);
                }
                Game.instance.GetPlayerProfile().IncrementStatPickable(pickable.m_itemPrefab.name, harvested);
            }
        }

        [HarmonyPatch(typeof(Attack))]
        public static class HarvestRangeIncreasesScythe
        {
            // [HarmonyDebug]
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(Attack.DoMeleeAttack))]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
            {
                var codeMatcher = new CodeMatcher(instructions);
                if (codeMatcher.TryMatchStartForward("Unable to increase vanilla harvest max range.",
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Attack), nameof(Attack.m_harvestRadiusMaxLevel)))
                )) {
                    codeMatcher.Advance(1).InsertAndAdvance(
                        Transpilers.EmitDelegate(IncreaseHarvestWeaponRange));
                }

                return codeMatcher.Instructions();
            }
        }

        [HarmonyPatch(typeof(Pickable), nameof(Pickable.Interact))]
        public static class GatheringLuckPatch
        {

            private static void Postfix(ref bool __result, Pickable __instance, Humanoid character)
            {
                if (ValConfig.EnableGathering.Value == true && ValConfig.EnableGatheringAOE.Value == true && ValConfig.AOEFeaturesEnabled && Player.m_localPlayer != null && character == Player.m_localPlayer && __instance != null && __instance.m_itemPrefab != null)
                {
                    if (UnallowedPickables.Contains(__instance.m_itemPrefab.name)){
                        Logger.LogDebug($"Pickable is not a gathering item.");
                        return;
                    }

                    float player_skill_factor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Farming);
                    //safety reset
                    if (enabled_aoe_gathering == false && aoe_gathering_activated_at + ValConfig.PlantingAOEHarvestResetSafety.Value < Time.realtimeSinceStartup) {
                        enabled_aoe_gathering = true;
                    }

                    Logger.LogDebug($"Checking for AOE gathering {(player_skill_factor * 100f) >= ValConfig.FarmingRangeRequiredLevel.Value} && {enabled_aoe_gathering}");
                    if ((player_skill_factor * 100f) >= ValConfig.FarmingRangeRequiredLevel.Value && enabled_aoe_gathering) {
                        float pickable_distance = ValConfig.GatheringRangeFactor.Value * player_skill_factor;
                        Collider[] targets = Physics.OverlapSphere(__instance.transform.position, pickable_distance, pickableMask);
                        Logger.LogDebug($"AOE Picking {targets.Count()} in harvest range {pickable_distance}.");
                        aoe_gathering_activated_at = Time.realtimeSinceStartup;
                        enabled_aoe_gathering = false;
                        if (targets.Length <= 5) {
                            foreach (Collider obj_collider in targets) {
                                Pickable pickable_item = obj_collider.GetComponent<Pickable>() ?? obj_collider.GetComponentInParent<Pickable>();
                                if (pickable_item != null) {
                                    Logger.LogDebug($"Checking {pickable_item.gameObject.name} in harvest range.");
                                    if (pickable_item.m_itemPrefab != null && !UnallowedPickables.Contains(pickable_item.m_itemPrefab.name)) {
                                        if (pickable_item.CanBePicked()) {
                                            pickable_item.m_nview.ClaimOwnership();
                                            pickable_item.Interact(Player.m_localPlayer, false, false);
                                        }
                                    }
                                }
                            }
                            enabled_aoe_gathering = true;
                        } else {
                            Player.m_localPlayer.StartCoroutine(PickAOE(targets));
                        }
                    }
                    
                }
            }

            // Coroutine to handle the AOE gathering of large sets of pickables
            static IEnumerator PickAOE(Collider[] targets) {
                int iterations = 0;
                foreach (Collider obj_collider in targets) {
                    iterations++;
                    if (iterations % 10 == 0) {
                        yield return new WaitForSeconds(0.1f);
                    }
                    if (obj_collider == null) { continue; }
                    Pickable pickable_item = obj_collider.GetComponent<Pickable>() ?? obj_collider.GetComponentInParent<Pickable>();
                    if (pickable_item != null) {
                        //Logger.LogDebug($"Async Checking {pickable_item.gameObject.name} in harvest range.");
                        if (pickable_item.m_itemPrefab != null && !UnallowedPickables.Contains(pickable_item.m_itemPrefab.name)) {
                            if (pickable_item.CanBePicked()) {
                                pickable_item.m_nview.ClaimOwnership();
                                pickable_item.Interact(Player.m_localPlayer, false, false);
                            }
                        }
                    }
                }
                enabled_aoe_gathering = true;
                yield break;
            }
        }

        private static float IncreaseHarvestWeaponRange(float max_harvest_range) {
            if (ValConfig.EnableGathering.Value == true) {
                return ValConfig.GatheringRangeFactor.Value + max_harvest_range;
            }
            return max_harvest_range;
        }


    }
}
