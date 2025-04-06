using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace ImpactfulSkills.patches
{
    public static class Gathering
    {
        private static readonly int pickableMask = LayerMask.GetMask("piece_nonsolid", "item", "Default_small");
        static readonly List<String> UnallowedPickables = new List<String>() { };
        private static List<float> luck_levels = new List<float> { };


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
            // [HarmonyDebug]
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(Pickable.Interact))]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
            {
                var codeMatcher = new CodeMatcher(instructions);
                codeMatcher.MatchStartForward(
                    new CodeMatch(OpCodes.Ldloc_1),
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Ldfld),
                    new CodeMatch(OpCodes.Callvirt),
                    new CodeMatch(OpCodes.Stloc_2)
                    ).RemoveInstructions(43)
                    .ThrowIfNotMatch("Unable remove vanilla pickable luckydrop.");

                return codeMatcher.Instructions();
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
        public static class GahteringLuckPatch
        {
            private static void Postfix(ref bool __result, Humanoid character, Pickable __instance)
            {
                if (ValConfig.EnableGathering.Value == true && Player.m_localPlayer != null && character == Player.m_localPlayer)
                {
                    if (UnallowedPickables.Contains(__instance.m_itemPrefab.name)){
                        Logger.LogDebug($"Pickable is not a gathering item.");
                        return;
                    }
                    if (__instance.m_picked == true) {
                        return;
                    }
                    // Increase item drops based on luck, and the gathering skill
                    float player_skill = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Farming);
                    float player_luck = (ValConfig.GatheringLuckFactor.Value * player_skill * 100f) / 100f;
                    float luck_roll = UnityEngine.Random.Range(0f, 50f) + player_luck;
                    int extra_drops = 0;
                    foreach (var level in luck_levels) {
                        Logger.LogDebug($"Gathering Luck roll: {luck_roll} > {level}");
                        if (luck_roll > level) {
                            extra_drops += 1;
                            Logger.LogDebug($"Gathering Luck lvl ({level}) added, drop total: {extra_drops}");
                        }
                    }
                    // Create the lucky effect to show that the player got extra drops
                    if (extra_drops > 0) {
                        Vector3 spawnp = __instance.transform.position + Vector3.up * __instance.m_spawnOffset;
                        Logger.LogDebug($"Spawning extra drops {extra_drops}");
                        // Show bonus text amount
                        DamageText.instance.ShowText(DamageText.TextType.Bonus, __instance.transform.position + Vector3.up * __instance.m_spawnOffset, $"+{extra_drops}", player: true);
                        for (int i = 0; i < extra_drops; i++) {
                            __instance.m_bonusEffect.Create(spawnp, Quaternion.identity);
                            UnityEngine.Object.Instantiate(__instance.m_itemPrefab, spawnp, __instance.transform.rotation);
                        }
                    }

                    if ((player_skill * 100f) > ValConfig.FarmingRangeRequiredLevel.Value) {
                        Logger.LogDebug("Triggering AoE gathering");
                        float pickable_distance = ValConfig.GatheringRangeFactor.Value * (player_skill * 100f);
                        Collider[] targets = Physics.OverlapSphere(__instance.transform.position, pickable_distance, pickableMask);
                        if (targets.Length <= 5) {
                            foreach (Collider obj_collider in targets) {
                                Pickable pickable_item = obj_collider.GetComponent<Pickable>() ?? obj_collider.GetComponentInParent<Pickable>();
                                if (pickable_item != null) {
                                    Logger.LogDebug($"Checking {pickable_item.gameObject.name} in harvest range.");
                                    if (!UnallowedPickables.Contains(pickable_item.m_itemPrefab.name)) {
                                        if (pickable_item.CanBePicked()) {
                                            pickable_item.Interact(Player.m_localPlayer, false, false);
                                        }
                                    }
                                }
                            }
                        } else {
                            __instance.StartCoroutine(PickAOE(targets));
                        }
                    }
                    // Gain a little XP for the skill
                    Player.m_localPlayer.RaiseSkill(Skills.SkillType.Farming, (1 + extra_drops));
                }
            }
        }

        private static float IncreaseHarvestWeaponRange(float max_harvest_range) {
            if (ValConfig.EnableGathering.Value == true) {
                return ValConfig.GatheringRangeFactor.Value + max_harvest_range;
            }
            return max_harvest_range;
        }

        // Coroutine to handle the AOE gathering of large sets of pickables
        static IEnumerator PickAOE(Collider[] targets) {
            int iterations = 0;
            foreach (Collider obj_collider in targets) {
                iterations++;
                if (iterations % 5 == 0) {
                    yield return new WaitForSeconds(0.1f);
                }
                if (obj_collider == null) { continue; }
                Pickable pickable_item = obj_collider.GetComponent<Pickable>() ?? obj_collider.GetComponentInParent<Pickable>();
                if (pickable_item != null) {
                    Logger.LogDebug($"Checking {pickable_item.gameObject.name} in harvest range.");
                    if (!UnallowedPickables.Contains(pickable_item.m_itemPrefab.name)) {
                        if (pickable_item.CanBePicked()) {
                            pickable_item.Interact(Player.m_localPlayer, false, false);
                        }
                    }
                }
            }
            yield break;
        }
    }
}
