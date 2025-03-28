using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace ImpactfulSkills.patches
{
    public static class Woodcutting
    {
        [HarmonyPatch(typeof(TreeLog), nameof(TreeLog.Damage))]
        public static class IncreaseTreeLogDamage
        {
            private static void Prefix(HitData hit)
            {
                ModifyChop(hit);
            }
        }

        [HarmonyPatch(typeof(TreeBase), nameof(TreeBase.Damage))]
        public static class IncreaseTreeBaseDamage
        {
            private static void Prefix(HitData hit) {
                ModifyChop(hit);
            }
        }

        [HarmonyPatch(typeof(Destructible), nameof(Destructible.Damage))]
        public static class DestructibleIncreaseChopDamage
        {
            private static void Prefix(Destructible __instance, HitData hit) {
                // Why o why is shrub_2 not a tree?
                if (ValConfig.EnableWoodcutting.Value == true && __instance.m_destructibleType == DestructibleType.Tree || __instance.m_destructibleType == DestructibleType.Default && __instance.gameObject.name == "shrub_2")
                {
                    ModifyChop(hit);
                }
            }
        }

        [HarmonyPatch(typeof(TreeLog), nameof(TreeLog.Destroy))]
        public static class IncreaseDropsFromTree
        {
            private static void Postfix(TreeLog __instance, HitData hitData) {
                if (ValConfig.EnableWoodcutting.Value == true && hitData != null && Player.m_localPlayer != null && hitData.m_attacker == Player.m_localPlayer.GetZDOID()) {
                    IncreaseTreeLogDrops(__instance);
                }
            }
        }

        [HarmonyPatch(typeof(Destructible), nameof(Destructible.Destroy))]
        public static class IncreaseDropsFromDestructibleTree
        {
            private static void Prefix(Destructible __instance, HitData hit) {
                // Logger.LogDebug($"{__instance.m_destructibleType} == {DestructibleType.Tree} | {__instance.m_destructibleType == DestructibleType.Tree}");
                if (ValConfig.EnableWoodcutting.Value == true && __instance.m_destructibleType == DestructibleType.Tree && hit != null && Player.m_localPlayer != null && hit.m_attacker == Player.m_localPlayer.GetZDOID())
                {
                    IncreaseDestructibleTreeDrops(__instance);
                }
            }
        }

        // [HarmonyEmitIL("./dumps")]
        [HarmonyPatch(typeof(TreeBase))]
        public static class DamageHandler_Apply_Patch
        {
            // [HarmonyDebug]
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(TreeBase.RPC_Damage))]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
            {
                var codeMatcher = new CodeMatcher(instructions);
                codeMatcher.MatchStartForward(
                    new CodeMatch(OpCodes.Ldloc_0),
                    new CodeMatch(OpCodes.Ldc_R4)
                    ).Advance(3).InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldarg_0), // Load the instance class
                    Transpilers.EmitDelegate(IncreaseTreeDrops)
                    ).ThrowIfNotMatch("Unable to patch drop increase for trees.");

                return codeMatcher.Instructions();
            }
        }

        public static void ModifyChop(HitData hit) {
            if (ValConfig.EnableWoodcutting.Value == true && hit != null && Player.m_localPlayer != null && hit.m_attacker == Player.m_localPlayer.GetZDOID()) {
                float player_woodcutting_skill_factor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.WoodCutting);
                Logger.LogDebug($"Player skillfactor: {player_woodcutting_skill_factor}");
                float player_chop_bonus = 1 + (ValConfig.WoodCuttingDmgMod.Value * (player_woodcutting_skill_factor * 100f) / 100f);
                Logger.LogDebug($"Player woodcutting dmg multiplier: {player_chop_bonus}");
                hit.m_damage.m_chop *= player_chop_bonus;
            }
        }

        public static void IncreaseDestructibleTreeDrops(Destructible dtree)
        {
            Vector3 position = dtree.transform.position;
            DropTable drops = dtree.GetComponent<DropOnDestroyed>().m_dropWhenDestroyed;
            IncreaseWoodDrops(drops, position);
        }

        public static void IncreaseTreeLogDrops(TreeLog tree)
        {
            Vector3 position = tree.transform.position;
            DropTable drops = tree.m_dropWhenDestroyed;
            IncreaseWoodDrops(drops, position);
        }

        public static void IncreaseTreeDrops(TreeBase tree) {
            if (ValConfig.EnableWoodcutting.Value == false) { return; }
            Vector3 position = tree.transform.position;
            DropTable drops = tree.m_dropWhenDestroyed;
            IncreaseWoodDrops(drops, position);
        }

        public static void IncreaseWoodDrops(DropTable drops, Vector3 position)
        {
            Dictionary<GameObject, int> drops_to_add = new Dictionary<GameObject, int>();
            foreach (var drop in drops.m_drops) {
                drops_to_add.Add(drop.m_item, drop.m_stackMin);
            }
            int drop_amount = 0;
            float player_woodcutting_skill_factor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.WoodCutting);
            
            float min_drop = drops.m_dropMin * (ValConfig.WoodCuttingLootFactor.Value * (player_woodcutting_skill_factor * 100f)) / 100f;
            float max_drop = drops.m_dropMax * (ValConfig.WoodCuttingLootFactor.Value * (player_woodcutting_skill_factor * 100f)) / 100f;
            if (min_drop <= 1f || max_drop <= 1f) {
                // no need to increase drops
                return;
            }

            if (min_drop > 0 && max_drop > 0 && min_drop != max_drop) {
                drop_amount = UnityEngine.Random.Range((int)min_drop, (int)max_drop);
            } else if (min_drop == max_drop) {
                drop_amount = (int)Math.Round(min_drop, 0);
            }
            Logger.LogDebug($"Tree drop increase min_drop: ({drops.m_dropMin}) {min_drop}, max_drop: ({drops.m_dropMax}) {max_drop} drop amount: {drop_amount}");
            if (drops_to_add.Count > 0) {
                foreach (var drop in drops_to_add) {
                    Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0, 360), 0f);
                    int max_stack_size = drop.Key.GetComponent<ItemDrop>().m_itemData.m_shared.m_maxStackSize;
                    if (drop_amount > max_stack_size) {
                        int stacks = drop_amount / max_stack_size;
                        for (int i = 0; i < stacks; i++) {
                            var extra_drop = UnityEngine.Object.Instantiate(drop.Key, position, rotation);
                            extra_drop.GetComponent<ItemDrop>().m_itemData.m_stack = max_stack_size;
                            Logger.LogDebug($"Dropping {max_stack_size} of {drop.Key.name} to the world.");
                        }
                        drop_amount -= (max_stack_size * stacks);
                    } else {
                        var extra_drop = UnityEngine.Object.Instantiate(drop.Key, position, rotation);
                        extra_drop.GetComponent<ItemDrop>().m_itemData.m_stack = drop_amount;
                        Logger.LogDebug($"Dropping {drop_amount} of {drop.Key.name} to the world.");
                    }
                }
            }
        }
    }
}
