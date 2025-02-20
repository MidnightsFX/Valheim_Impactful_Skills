using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImpactfulSkills.patches
{
    public static class Mining
    {
        private static readonly int rockmask = LayerMask.GetMask("static_solid", "Default_small", "Default");

        [HarmonyPatch(typeof(MineRock), nameof(MineRock.Damage))]
        public static class MinerockDmgPatch
        {
            private static void Prefix(HitData hit)
            {
                ModifyPickaxeDmg(hit);
            }
        }

        [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.Damage))]
        public static class Minerock5DmgPatch
        {
            private static void Prefix(HitData hit)
            {
                ModifyPickaxeDmg(hit);
            }
        }

        [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.Destroy))]
        public static class Minerock5DestroyPatch
        {
            private static void Postfix(MineRock5 __instance, HitData hitData)
            {
                if (hitData != null && Player.m_localPlayer != null)
                {
                    IncreaseMiningDrops(__instance.m_dropItems, __instance.gameObject.transform.position, hitData);
                }
            }
        }

        public static void ModifyPickaxeDmg(HitData hit)
        {
            if (hit != null && Player.m_localPlayer != null && hit.m_attacker == Player.m_localPlayer.GetZDOID())
            {
                float player_skill_factor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Pickaxes);
                float player_pickaxe_bonus = (ValConfig.MiningDmgMod.Value * (player_skill_factor) / 100f);
                Logger.LogDebug($"Player mining dmg multiplier: {player_pickaxe_bonus}");
                hit.m_damage.m_chop *= player_pickaxe_bonus;
            }
        }

        public static void IncreaseMiningDrops(DropTable drops, Vector3 position, HitData hitdata)
        {
            float distance_to_rock = Vector3.Distance(Player.m_localPlayer.transform.position, position);
            if (distance_to_rock > 15f) {
                Logger.LogDebug($"Player too far away from rock to get increased loot: {distance_to_rock}");
                return;
            }
            float player_skill_factor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Pickaxes);

            Dictionary<GameObject, int> drops_to_add = new Dictionary<GameObject, int>();
            foreach (var drop in drops.m_drops)
            {
                drops_to_add.Add(drop.m_item, drop.m_stackMin);
            }
            int drop_amount = 0;
            
            float min_drop = (drops.m_dropMin * (ValConfig.MiningLootFactor.Value * player_skill_factor)) - drops.m_dropMin;
            float max_drop = (drops.m_dropMax * (ValConfig.MiningLootFactor.Value * player_skill_factor)) - drops.m_dropMax;
            if (min_drop > 0 && max_drop > 0 && min_drop != max_drop) {
                drop_amount = UnityEngine.Random.Range((int)min_drop, (int)max_drop);
            } else if (min_drop == max_drop) {
                drop_amount = (int)Math.Round(min_drop, 0);
            }
            Logger.LogDebug($"Mining rock drop increase min_drop: {min_drop}, max_drop: {max_drop} drop amount: {drop_amount}");
            foreach (var drop in drops_to_add)
            {
                Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0, 360), 0f);
                int max_stack_size = drop.Key.GetComponent<ItemDrop>().m_itemData.m_shared.m_maxStackSize;
                if (drop_amount > max_stack_size)
                {
                    int stacks = drop_amount / max_stack_size;
                    for (int i = 0; i < stacks; i++)
                    {
                        var extra_drop = UnityEngine.Object.Instantiate(drop.Key, position, rotation);
                        extra_drop.GetComponent<ItemDrop>().m_itemData.m_stack = max_stack_size;
                    }
                    drop_amount -= (max_stack_size * stacks);
                }
                else
                {
                    var extra_drop = UnityEngine.Object.Instantiate(drop.Key, position, rotation);
                    extra_drop.GetComponent<ItemDrop>().m_itemData.m_stack = drop_amount;
                }
            }

            // Trigger AOE mining if the player has the required skill level
            if (player_skill_factor >= ValConfig.MiningAOELevel.Value)
            {
                // Don't apply AOE damage if there was no hitdata in the first place- this happens when the rock is destroyed by gravity etc
                if (hitdata == null) { return; }

                float hitdmg = hitdata.m_damage.m_pickaxe * (player_skill_factor / 100f);
                if (hitdmg <= 0) { hitdmg = 1f; }
                HitData aoedmg = new HitData() { m_damage = new HitData.DamageTypes() { m_pickaxe = hitdmg } };
                foreach (Collider obj_collider in Physics.OverlapSphere(position, 3f, rockmask))
                {
                    MineRock minerock = obj_collider.GetComponent<MineRock>() ?? obj_collider.transform.parent?.GetComponent<MineRock>();
                    if (minerock != null)
                    {
                        minerock.Damage(aoedmg);
                    } else {
                        MineRock5 minerock5 = obj_collider.GetComponent<MineRock5>() ?? obj_collider.transform.parent?.GetComponent<MineRock5>();
                        if (minerock5 != null)
                        {
                            minerock.Damage(aoedmg);
                        }
                    }

                }
            }
        }
    }
}
