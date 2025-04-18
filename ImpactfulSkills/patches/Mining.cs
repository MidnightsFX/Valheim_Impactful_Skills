﻿using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ImpactfulSkills.patches
{
    public static class Mining
    {
        private static readonly int rockmask = LayerMask.GetMask("static_solid", "Default_small", "Default");
        private static Collider[] current_aoe_strike = null;

        [HarmonyPatch(typeof(MineRock), nameof(MineRock.Damage))]
        public static class MinerockDmgPatch
        {
            private static void Prefix(HitData hit, MineRock __instance)
            {
                ModifyPickaxeDmg(hit, __instance);
            }
        }

        [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.Damage))]
        public static class Minerock5DmgPatch
        {
            private static void Prefix(HitData hit, MineRock5 __instance)
            {
                ModifyPickaxeDmg(hit, null, __instance);
            }
        }

        [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.RPC_SetAreaHealth))]
        public static class Minerock5DestroyPatch
        {
            private static void Postfix(MineRock5 __instance, long sender, int index, float health)
            {
                if (ValConfig.EnableMining.Value == true && Player.m_localPlayer != null && health <= 0)
                {
                    IncreaseMiningDrops(__instance.m_dropItems, __instance.gameObject.transform.position);
                }
            }
        }

        public static void ModifyPickaxeDmg(HitData hit, MineRock instance = null, MineRock5 instance5 = null)
        {
            if (ValConfig.EnableMining.Value == true && hit != null && Player.m_localPlayer != null && hit.m_attacker == Player.m_localPlayer.GetZDOID())
            {
                float player_skill_factor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Pickaxes);
                float player_pickaxe_bonus = 1 + (ValConfig.MiningDmgMod.Value * (player_skill_factor * 100f) / 100f);
                Logger.LogDebug($"Player mining dmg multiplier: {player_pickaxe_bonus}");
                hit.m_damage.m_pickaxe *= player_pickaxe_bonus;

                if (hit.m_damage.m_pickaxe <= 0f) { return; }

                // Trigger AOE mining if the player has the required skill level
                if (ValConfig.EnableMiningAOE.Value && (player_skill_factor * 100f) >= ValConfig.MiningAOELevel.Value && current_aoe_strike == null) {
                    Logger.LogDebug("Player mining aoe activated");
                    Vector3 position = hit.m_point;
                    float hitdmg = hit.m_damage.m_pickaxe;
                    HitData aoedmg = hit;
                    Collider[] mine_targets = Physics.OverlapSphere(position, (ValConfig.MiningAOERange.Value * player_skill_factor), rockmask);
                    current_aoe_strike = mine_targets;
                    if (instance != null) { instance.StartCoroutine(MineAoeDamage(mine_targets, aoedmg)); }
                    if (instance5 != null) { instance5.StartCoroutine(MineAoeDamage(mine_targets, aoedmg)); }
                }
            }
        }

        static IEnumerator MineAoeDamage(Collider[] mine_targets, HitData aoedmg)
        {
            int iterations = 0;
            foreach (Collider obj_collider in mine_targets)
            {
                iterations++;
                if (iterations % 5 == 0)
                {
                    yield return new WaitForSeconds(0.1f);
                }
                if (obj_collider == null) { continue; }
                Logger.LogDebug($"AOE hit on: {obj_collider.name}");
                MineRock minerock = obj_collider.gameObject.GetComponentInParent<MineRock>();
                aoedmg.m_point = obj_collider.bounds.center;
                aoedmg.m_hitCollider = obj_collider;
                if (minerock != null)
                {
                    Logger.LogDebug($"AOE Damage applying to minerock");
                    minerock.Damage(aoedmg);
                }
                else
                {
                    MineRock5 minerock5 = obj_collider.gameObject.GetComponentInParent<MineRock5>();
                    if (minerock5 != null)
                    {
                        Logger.LogDebug($"AOE Damage applying to minerock5");
                        int index = minerock5.GetAreaIndex(obj_collider);
                        Logger.LogDebug($"AOE Damage applying to minerock5 index: {index}");
                        minerock5.DamageArea(index, aoedmg);
                    }
                }
            }
            current_aoe_strike = null;
            yield break;
        }

        public static void IncreaseMiningDrops(DropTable drops, Vector3 position, HitData hitdata = null)
        {
            float distance_to_rock = Vector3.Distance(Player.m_localPlayer.transform.position, position);
            if (distance_to_rock > 15f) {
                Logger.LogDebug($"Player too far away from rock to get increased loot: {distance_to_rock}");
                return;
            }
            float player_skill_factor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Pickaxes);

            Dictionary<GameObject, int> drops_to_add = new Dictionary<GameObject, int>();
            foreach (var drop in drops.m_drops) {
                float droll = UnityEngine.Random.value;
                Logger.LogDebug($"Mining rock check roll: {droll} < {(1 - drops.m_dropChance)}");
                // Use the drops chance to randomly roll if we get extra drops for this drop
                if (droll < (1 - drops.m_dropChance)) {
                    Logger.LogDebug($"Mining rock drop increase: {drop.m_item.name} failed drop roll");
                    continue;
                }
                drops_to_add.Add(drop.m_item, drop.m_stackMin);
            }
            int drop_amount = 0;
            Logger.LogDebug($"Mining rock drop current: {drops.m_dropMin}, max_drop: {drops.m_dropMax}");
            float min_drop = drops.m_dropMin * (ValConfig.MiningLootFactor.Value * (player_skill_factor * 100f)) / 100f;
            float max_drop = drops.m_dropMax * (ValConfig.MiningLootFactor.Value * (player_skill_factor * 100f)) / 100f;
            if (min_drop > 0 && max_drop > 0 && min_drop != max_drop) {
                drop_amount = UnityEngine.Random.Range((int)min_drop, (int)max_drop);
            } else if (min_drop == max_drop) {
                drop_amount = (int)Math.Round(min_drop, 0);
            }
            Logger.LogDebug($"Mining rock drop increase min_drop: {min_drop}, max_drop: {max_drop} drop amount: {drop_amount}");
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
