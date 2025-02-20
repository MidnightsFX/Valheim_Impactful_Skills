using HarmonyLib;
using System;
using System.Collections.Generic;
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
            private static void Prefix(HitData hit)
            {
                ModifyChop(hit);
            }
        }

        [HarmonyPatch(typeof(TreeLog), nameof(TreeLog.Destroy))]
        public static class IncreaseDropsFromTree
        {
            private static void Postfix(TreeLog __instance, HitData hitData)
            {
                if (hitData != null && Player.m_localPlayer != null && hitData.m_attacker == Player.m_localPlayer.GetZDOID())
                {
                    IncreaseTreeDrops(__instance.m_dropWhenDestroyed, __instance.gameObject.transform.position);
                }
            }
        }

        [HarmonyPatch(typeof(TreeBase), nameof(TreeBase.Destroy))]
        public static class IncreaseDropsFromTreeBase
        {
            private static void Postfix(TreeLog __instance, HitData hitData)
            {
                if (hitData != null && Player.m_localPlayer != null && hitData.m_attacker == Player.m_localPlayer.GetZDOID())
                {
                    IncreaseTreeDrops(__instance.m_dropWhenDestroyed, __instance.gameObject.transform.position);
                }
            }
        }

        public static void ModifyChop(HitData hit)
        {
            if (hit != null && Player.m_localPlayer != null && hit.m_attacker == Player.m_localPlayer.GetZDOID()) {
                float player_woodcutting_skill_factor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.WoodCutting);
                float player_chop_bonus = (ValConfig.WoodCuttingDmgMod.Value * (player_woodcutting_skill_factor) / 100f);
                Logger.LogDebug($"Player woodcutting dmg multiplier: {player_chop_bonus}");
                hit.m_damage.m_chop *= player_chop_bonus;
            }
        }

        public static void IncreaseTreeDrops(DropTable drops, Vector3 position)
        {
            Dictionary<GameObject, int> drops_to_add = new Dictionary<GameObject, int>();
            foreach (var drop in drops.m_drops) {
                drops_to_add.Add(drop.m_item, drop.m_stackMin);
            }
            int drop_amount = 0;
            float player_woodcutting_skill_factor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.WoodCutting);
            float min_drop = (drops.m_dropMin * (ValConfig.WoodCuttingLootFactor.Value * player_woodcutting_skill_factor)) - drops.m_dropMin;
            float max_drop = (drops.m_dropMax * (ValConfig.WoodCuttingLootFactor.Value * player_woodcutting_skill_factor)) - drops.m_dropMax;
            if (min_drop > 0 && max_drop > 0 && min_drop != max_drop) {
                drop_amount = UnityEngine.Random.Range((int)min_drop, (int)max_drop);
            } else if (min_drop == max_drop) {
                drop_amount = (int)Math.Round(min_drop, 0);
            }
            Logger.LogDebug($"Tree drop increase min_drop: {min_drop}, max_drop: {max_drop} drop amount: {drop_amount}");
            foreach (var drop in drops_to_add) {
                Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0, 360), 0f);
                int max_stack_size = drop.Key.GetComponent<ItemDrop>().m_itemData.m_shared.m_maxStackSize;
                if (drop_amount > max_stack_size) {
                    int stacks = drop_amount / max_stack_size;
                    for (int i = 0; i < stacks; i++) {
                        var extra_drop = UnityEngine.Object.Instantiate(drop.Key, position, rotation);
                        extra_drop.GetComponent<ItemDrop>().m_itemData.m_stack = max_stack_size;
                    }
                    drop_amount -= (max_stack_size * stacks);
                } else {
                    var extra_drop = UnityEngine.Object.Instantiate(drop.Key, position, rotation);
                    extra_drop.GetComponent<ItemDrop>().m_itemData.m_stack = drop_amount;
                }
            }
        }
    }
}
