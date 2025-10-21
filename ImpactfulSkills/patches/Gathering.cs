using HarmonyLib;
using ImpactfulSkills.common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;

namespace ImpactfulSkills.patches
{
    public static class Gathering
    {
        private static readonly int pickableMask = LayerMask.GetMask("piece_nonsolid", "item", "Default_small");
        static readonly List<String> UnallowedPickables = new List<String>() { };
        private static List<float> luck_levels = new List<float> { };
        private static bool enabled_aoe_gathering = true;


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
            foreach (var item in ValConfig.GatheringLuckLevels.Value.Split(',')) {
                luck_levels.Add(float.Parse(item));
            }
            ValConfig.GatheringLuckLevels.SettingChanged += PickableLuckLevelsChanged;
            foreach (var unallowed in ValConfig.GatheringDisallowedItems.Value.Split(','))
            {
                UnallowedPickables.Add(unallowed);
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
                codeMatcher.MatchStartForward(
                        new CodeMatch(OpCodes.Ldc_I4_0), 
                        new CodeMatch(OpCodes.Stloc_0), // int bonus_num = 0;
                        new CodeMatch(OpCodes.Ldarg_0),
                        new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Pickable), nameof(Pickable.m_picked)))
                    )
                    .Advance(2)
                    .Insert(
                        new CodeInstruction(OpCodes.Ldarg_0), // Load the instance class
                        Transpilers.EmitDelegate(DetermineExtraDrops),
                        new CodeInstruction(OpCodes.Stloc_0)
                    )
                    .Advance(3)
                    .CreateLabelOffset(out Label label, offset: 59)
                    .InsertAndAdvance(new CodeInstruction(OpCodes.Br, label))
                    .ThrowIfNotMatch("Unable remove vanilla pickable luckydrop.");
                return codeMatcher.Instructions();
            }

            static int DetermineExtraDrops(Pickable __instance)
            {
                if (Player.m_localPlayer == null || __instance.m_picked == true) { return 0; }
                if (UnallowedPickables.Contains(__instance.m_itemPrefab.name)) {
                    Logger.LogDebug($"Pickable is not an allowed gathering item.");
                    return 0;
                }
                // Increase item drops based on luck, and the gathering skill
                float player_skill_factor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Farming);
                float player_luck = (ValConfig.GatheringLuckFactor.Value * player_skill_factor * 100f) / 100f;
                float luck_roll = UnityEngine.Random.Range(0f, 50f) + player_luck;
                int extra_drops = 0;
                foreach (var level in luck_levels)
                {
                    Logger.LogDebug($"Gathering Luck roll: {luck_roll} > {level}");
                    if (luck_roll > level)
                    {
                        extra_drops += 1;
                    }
                    else { break; }
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

                // Gain a little XP for the skill
                Player.m_localPlayer.RaiseSkill(Skills.SkillType.Farming, (1 + extra_drops));
                return extra_drops;
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
                codeMatcher.MatchStartForward(
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Attack), nameof(Attack.m_harvestRadiusMaxLevel)))
                    ).Advance(1).InsertAndAdvance(
                    Transpilers.EmitDelegate(IncreaseHarvestWeaponRange))
                    .ThrowIfNotMatch("Unable to increase vanilla harvest max range.");

                return codeMatcher.Instructions();
            }
        }

        [HarmonyPatch(typeof(Pickable), nameof(Pickable.Interact))]
        public static class GatheringLuckPatch
        {

            private static void Postfix(ref bool __result, Pickable __instance, Humanoid character)
            {
                if (ValConfig.EnableGathering.Value == true && ValConfig.EnableGatheringAOE.Value == true && Player.m_localPlayer != null && character == Player.m_localPlayer && __instance != null)
                {
                    if (UnallowedPickables.Contains(__instance.m_itemPrefab.name)){
                        Logger.LogDebug($"Pickable is not a gathering item.");
                        return;
                    }

                    float player_skill_factor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Farming);
                    Logger.LogDebug($"Checking for AOE gathering {(player_skill_factor * 100f) > ValConfig.FarmingRangeRequiredLevel.Value} && {enabled_aoe_gathering}");
                    if ((player_skill_factor * 100f) > ValConfig.FarmingRangeRequiredLevel.Value && enabled_aoe_gathering) {
                        float pickable_distance = ValConfig.GatheringRangeFactor.Value * player_skill_factor;
                        Collider[] targets = Physics.OverlapSphere(__instance.transform.position, pickable_distance, pickableMask);
                        Logger.LogDebug($"AOE Picking {targets.Count()} in harvest range {pickable_distance}.");
                        enabled_aoe_gathering = false;
                        if (targets.Length <= 5) {
                            foreach (Collider obj_collider in targets) {
                                Pickable pickable_item = obj_collider.GetComponent<Pickable>() ?? obj_collider.GetComponentInParent<Pickable>();
                                if (pickable_item != null) {
                                    Logger.LogDebug($"Checking {pickable_item.gameObject.name} in harvest range.");
                                    if (!UnallowedPickables.Contains(pickable_item.m_itemPrefab.name)) {
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
            static IEnumerator PickAOE(Collider[] targets)
            {
                int iterations = 0;
                foreach (Collider obj_collider in targets)
                {
                    iterations++;
                    if (iterations % 10 == 0)
                    {
                        yield return new WaitForSeconds(0.1f);
                    }
                    if (obj_collider == null) { continue; }
                    Pickable pickable_item = obj_collider.GetComponent<Pickable>() ?? obj_collider.GetComponentInParent<Pickable>();
                    if (pickable_item != null) {
                        //Logger.LogDebug($"Async Checking {pickable_item.gameObject.name} in harvest range.");
                        if (!UnallowedPickables.Contains(pickable_item.m_itemPrefab.name)) {
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
