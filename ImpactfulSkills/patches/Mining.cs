using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.HID;

namespace ImpactfulSkills.patches
{
    public static class Mining
    {
        private static readonly int rockmask = LayerMask.GetMask("static_solid", "Default_small", "Default");
        private static Collider[] current_aoe_strike = null;
        private static bool rockbreaker_running = false;

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

                if (rockbreaker_running == false && current_aoe_strike == null) {
                    float rock_breaker_roll = UnityEngine.Random.value;
                    Logger.LogDebug($"Rock breaker roll: {rock_breaker_roll} <= {ValConfig.RockBreakerMaxChance.Value}");
                    if (ValConfig.EnableMiningRockBreaker.Value && (player_skill_factor * 100f) >= ValConfig.RockBreakerRequiredLevel.Value && rock_breaker_roll <= ValConfig.RockBreakerMaxChance.Value)
                    {
                        Logger.LogDebug("Rock breaker activated!");
                        HitData rockbreak = hit;
                        rockbreak.m_damage.m_pickaxe = ValConfig.RockBreakerDamage.Value;
                        rockbreaker_running = true;
                        if (instance != null)
                        {
                            Logger.LogDebug("Rock breaker activated on minerock");
                            current_aoe_strike = instance.m_hitAreas;
                            instance.StartCoroutine(MineAoeDamage(instance.m_hitAreas, rockbreak));
                        }
                        if (instance5 != null)
                        {
                            Logger.LogDebug("Rock breaker activated on minerock5");
                            List<Collider> mr5targets = new List<Collider>();
                            foreach (var area in instance5.m_hitAreas)
                            {
                                mr5targets.Add(area.m_collider);
                            }
                            current_aoe_strike = mr5targets.ToArray();
                            instance5.StartCoroutine(MineAoeDamage(mr5targets.ToArray(), rockbreak));
                        }
                        return;
                    }
                }
                

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

        public static bool ArrayContains(Collider[] group, Collider target)
        {
            foreach(Collider obj_collider in group) {
                if (obj_collider == target) { return true; }
            }
            return false;
        }

        static IEnumerator MineAoeDamage(Collider[] mine_targets, HitData aoedmg)
        {
            MineRock5 minerock5 = null;
            MineRock minerock = null;
            bool is_minerock = true;
            int iterations = 0;
            foreach (Collider b in mine_targets)
            {
                MineRock mr = b.gameObject.GetComponentInParent<MineRock>();
                if (mr != null) {
                    minerock = mr;
                    is_minerock = true;
                    break;
                }
                MineRock5 mr5 = b.gameObject.GetComponentInParent<MineRock5>();
                if (mr5 != null)
                {
                    minerock5 = mr5;
                    is_minerock = false;
                    break;
                }
            }

            if (is_minerock) {
                foreach (Collider obj_collider in mine_targets)
                {
                    if (obj_collider == null) { continue; }
                    iterations++;
                    if (iterations % ValConfig.MinehitsPerInterval.Value == 0) { yield return new WaitForSeconds(0.2f); }
                    if (ArrayContains(minerock.m_hitAreas, obj_collider))
                    {
                        Logger.LogDebug($"AOE Damage applying to minerock");
                        aoedmg.m_point = obj_collider.bounds.center;
                        aoedmg.m_hitCollider = obj_collider;
                        minerock.Damage(aoedmg);
                    }
                }
            } else {
                foreach (Collider obj_collider in mine_targets) {
                    if (obj_collider == null) { continue; }
                    iterations++;
                    if (iterations % 10 == 0) { yield return new WaitForSeconds(0.1f); }
                    int index = minerock5.GetAreaIndex(obj_collider);
                    if (index < 0) { continue; }
                    Logger.LogDebug($"AOE Damage applying to minerock5");
                    Logger.LogDebug($"AOE Damage applying to minerock5 index: {index}");
                    aoedmg.m_point = obj_collider.bounds.center;
                    aoedmg.m_hitCollider = obj_collider;
                    minerock5.DamageArea(index, aoedmg);
                } 
            }

            rockbreaker_running = false;
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
            if (drops.m_drops != null) {
                foreach (var drop in drops.m_drops)
                {
                    float droll = UnityEngine.Random.value;
                    Logger.LogDebug($"Mining rock check roll: {droll} < {(1 - drops.m_dropChance)}");
                    // Use the drops chance to randomly roll if we get extra drops for this drop
                    if (droll < (1 - drops.m_dropChance)) {
                        Logger.LogDebug($"Mining rock drop increase: {drop.m_item.name} failed drop roll");
                        continue;
                    }
                    if (drops_to_add.ContainsKey(drop.m_item))
                    {
                        drops_to_add[drop.m_item] += drop.m_stackMin;
                    }
                    else
                    {
                        drops_to_add.Add(drop.m_item, drop.m_stackMin);
                    }

                }
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
            if (drops_to_add.Count == 0) { return; }
            Logger.LogDebug($"Mining rock drop increase min_drop: {min_drop}, max_drop: {max_drop} drop amount: {drop_amount}");
            Player.m_localPlayer.StartCoroutine(DropItemsAsync(drops_to_add, position, 1f));
        }

        static IEnumerator DropItemsAsync(Dictionary<GameObject, int> drops, Vector3 centerPos, float dropArea)
        {
            int obj_spawns = 0;

            foreach (var drop in drops)
            {
                bool set_stack_size = false;
                int max_stack_size = 0;
                var item = drop.Key;
                int amount = drop.Value;
                Logger.LogDebug($"Dropping {item.name} {amount}");
                for (int i = 0; i < amount;)
                {

                    // Wait for a short duration to avoid dropping too many items at once
                    if (obj_spawns > 0 && obj_spawns % 10 == 0)
                    {
                        yield return new WaitForSeconds(0.1f);
                    }

                    // Drop the item at the specified position
                    GameObject droppedItem = UnityEngine.Object.Instantiate(item, centerPos, Quaternion.identity);
                    obj_spawns++;

                    ItemDrop component = droppedItem.GetComponent<ItemDrop>();
                    if (set_stack_size == false)
                    {
                        set_stack_size = true;
                        if (component) { max_stack_size = component.m_itemData.m_shared.m_maxStackSize; }
                    }

                    // Drop in stacks if this is an item
                    if ((object)component != null)
                    {
                        int remaining = (amount - i);
                        if (remaining > 0)
                        {
                            if (amount > max_stack_size)
                            {
                                component.m_itemData.m_stack = max_stack_size;
                                i += max_stack_size;
                            }
                            else
                            {
                                component.m_itemData.m_stack = remaining;
                                i += remaining;
                            }
                        }
                        component.m_itemData.m_worldLevel = (byte)Game.m_worldLevel;
                    }

                    Rigidbody component2 = droppedItem.GetComponent<Rigidbody>();
                    if ((bool)component2)
                    {
                        Vector3 insideUnitSphere = UnityEngine.Random.insideUnitSphere;
                        if (insideUnitSphere.y < 0f)
                        {
                            insideUnitSphere.y = 0f - insideUnitSphere.y;
                        }
                        component2.AddForce(insideUnitSphere * 5f, ForceMode.VelocityChange);
                    }
                    i++;
                }
            }

            yield break;
        }
    }
}
