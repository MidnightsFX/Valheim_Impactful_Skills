using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ImpactfulSkills.patches {
    public static class Mining {
        private static readonly int rockmask = LayerMask.GetMask("static_solid", "Default_small", "Default");
        private static Collider[] current_aoe_strike = (Collider[])null;
        private static bool rockbreaker_running = false;
        private static readonly List<string> skipIncreaseDrops = new List<string> { "LeatherScraps", "WitheredBone" };

        public static void ModifyPickaxeDmg(HitData hit, MineRock instance = null, MineRock5 instance5 = null) {
            if (!ValConfig.EnableMining.Value || hit == null || Player.m_localPlayer == null || hit.m_attacker != Player.m_localPlayer.GetZDOID())
                return;
            float skillFactor = ((Character)Player.m_localPlayer).GetSkillFactor((Skills.SkillType)12);
            float num1 = (float)(1.0 + (double)ValConfig.MiningDmgMod.Value * ((double)skillFactor * 100.0) / 100.0);
            Logger.LogDebug(string.Format("Player mining dmg multiplier: {0}", (object)num1));
            hit.m_damage.m_pickaxe *= num1;
            if (ValConfig.EnableMiningCritHit.Value && (double)skillFactor * 100.0 >= (double)ValConfig.RequiredLevelForMiningCrit.Value && (double)UnityEngine.Random.value <= (double)ValConfig.ChanceForMiningCritHit.Value) {
                Logger.LogDebug("Mining Critical hit activated");
                hit.m_damage.m_pickaxe *= ValConfig.CriticalHitDmgMult.Value;
            }
            // No damage will be done, skip.
            if ((double)hit.m_damage.m_pickaxe <= 0.0) { return; }
            // Check for whole rock breaker
            if (!Mining.rockbreaker_running && Mining.current_aoe_strike == null) {
                float num2 = UnityEngine.Random.value;
                float num3 = skillFactor * ValConfig.RockBreakerMaxChance.Value;
                Logger.LogDebug(string.Format("Rock breaker roll: {0} <= {1}", (object)num2, (object)num3));
                if (ValConfig.EnableMiningRockBreaker.Value && (double)skillFactor * 100.0 >= (double)ValConfig.RockBreakerRequiredLevel.Value && (double)num2 <= (double)num3) {
                    Logger.LogDebug("Rock breaker activated!");
                    HitData aoedmg = hit;
                    aoedmg.m_damage.m_pickaxe = ValConfig.RockBreakerDamage.Value;
                    Mining.rockbreaker_running = true;
                    if (instance != null && Player.m_localPlayer != null) {
                        Logger.LogDebug("Rock breaker activated on minerock");
                        Mining.current_aoe_strike = instance.m_hitAreas;
                        Player.m_localPlayer.StartCoroutine(Mining.MineAoeDamage(instance.m_hitAreas, aoedmg));
                    } else {
                        Mining.rockbreaker_running = false;
                        Mining.current_aoe_strike = null;
                    }
                    if (instance5 != null && Player.m_localPlayer != null) {
                        Logger.LogDebug("Rock breaker activated on minerock5");
                        List<Collider> colliderList = new List<Collider>();
                        foreach (MineRock5.HitArea hitArea in instance5.m_hitAreas) {
                            colliderList.Add(hitArea.m_collider);
                        }
                        Mining.current_aoe_strike = colliderList.ToArray();
                        Player.m_localPlayer.StartCoroutine(Mining.MineAoeDamage(colliderList.ToArray(), aoedmg));
                    } else {
                        Mining.rockbreaker_running = false;
                        Mining.current_aoe_strike = null;
                    }
                    return;
                }
            }
            // Check for AOE mining, guard clause
            if (!ValConfig.EnableMiningAOE.Value || (double)skillFactor * 100.0 < (double)ValConfig.MiningAOELevel.Value || Mining.current_aoe_strike != null) { return; }
                
            float num4 = ValConfig.ChanceForAOEOnHit.Value;
            if (ValConfig.ChanceForAOEOnHitScalesWithSkill.Value)
                num4 += 1f * skillFactor;
            float num5 = UnityEngine.Random.value;
            if ((double)num5 < (double)num4) {
                Logger.LogDebug(string.Format("AOE Mining failed roll: {0} < {1}", (object)num5, (object)num4));
            } else {
                Logger.LogDebug("Player mining aoe activated");
                Vector3 point = hit.m_point;
                HitData.DamageTypes damage = hit.m_damage;
                HitData aoedmg = hit;
                double radius = (double)ValConfig.MiningAOERange.Value * (double)skillFactor;
                int rockmask = Mining.rockmask;
                Collider[] mine_targets = Physics.OverlapSphere(point, (float)radius, rockmask);
                Mining.current_aoe_strike = mine_targets;
                if (Player.m_localPlayer != null) {
                    Player.m_localPlayer.StartCoroutine(Mining.MineAoeDamage(mine_targets, aoedmg));
                } else {
                    Mining.current_aoe_strike = null;
                    Mining.rockbreaker_running = false;
                }
            }
        }

        public static bool ArrayContains(Collider[] group, Collider target) {
            foreach (UnityEngine.Object @object in group) {
                if (@object == target)
                    return true;
            }
            return false;
        }

        private static IEnumerator MineAoeDamage(Collider[] mine_targets, HitData aoedmg) {
            MineRock5 minerock5 = null;
            MineRock minerock = null;
            bool flag = true;
            int iterations = 0;
            try {
                foreach (Collider mineTarget in mine_targets) {
                    MineRock componentInParent1 = mineTarget.gameObject.GetComponentInParent<MineRock>();
                    if (componentInParent1 != null) {
                        minerock = componentInParent1;
                        flag = true;
                        break;
                    }
                    MineRock5 componentInParent2 = mineTarget.gameObject.GetComponentInParent<MineRock5>();
                    if (componentInParent2 != null) {
                        minerock5 = componentInParent2;
                        flag = false;
                        break;
                    }
                }
            } catch (Exception ex) {
                Logger.LogWarning("Exception trying to get minerock parent object, AOE mining skipped: " + ex.Message);
                Mining.rockbreaker_running = false;
                Mining.current_aoe_strike = null;
                yield break;
            }
            if (minerock != null || minerock5 != null) {
                Collider[] colliderArray;
                int index;
                Collider obj_collider;
                if (mine_targets != null) {
                    if (flag) {
                        colliderArray = mine_targets;
                        for (index = 0; index < colliderArray.Length; ++index) {
                            obj_collider = colliderArray[index];
                            if (!(obj_collider == null)) {
                                ++iterations;
                                if (iterations % ValConfig.MinehitsPerInterval.Value == 0)
                                    yield return (object)new WaitForSeconds(0.2f);
                                if (Mining.ArrayContains(minerock.m_hitAreas, obj_collider)) {
                                    Logger.LogDebug("AOE Damage applying to minerock");
                                    aoedmg.m_point = obj_collider.bounds.center;
                                    aoedmg.m_hitCollider = obj_collider;
                                    minerock.Damage(aoedmg);
                                }
                            }
                        }
                    } else {
                        colliderArray = mine_targets;
                        for (index = 0; index < colliderArray.Length; ++index) {
                            obj_collider = colliderArray[index];
                            if (!(obj_collider == null)) {
                                ++iterations;
                                if (iterations % ValConfig.MinehitsPerInterval.Value == 0)
                                    yield return (object)new WaitForSeconds(0.1f);
                                int areaIndex = minerock5.GetAreaIndex(obj_collider);
                                if (areaIndex >= 0) {
                                    Logger.LogDebug("AOE Damage applying to minerock5");
                                    Logger.LogDebug(string.Format("AOE Damage applying to minerock5 index: {0}", (object)areaIndex));
                                    aoedmg.m_point = obj_collider.bounds.center;
                                    aoedmg.m_hitCollider = obj_collider;
                                    minerock5.DamageArea(areaIndex, aoedmg);
                                }
                            }
                        }
                    }
                }
            }
            Mining.rockbreaker_running = false;
            Mining.current_aoe_strike = null;
        }

        public static void IncreaseDestructibleMineDrops(Destructible dmine) {
            if (dmine.m_spawnWhenDestroyed != null)
                return;
            Vector3 position = ((Component)dmine).transform.position;
            DropOnDestroyed component = ((Component)dmine).GetComponent<DropOnDestroyed>();
            if (component == null || component.m_dropWhenDestroyed == null)
                return;
            Mining.IncreaseMiningDrops(component.m_dropWhenDestroyed, position);
        }

        public static void IncreaseMiningDrops(DropTable drops, Vector3 position, HitData hitdata = null) {
            float nearbyDistance = Vector3.Distance(Player.m_localPlayer.transform.position, position);
            if (nearbyDistance > 15.0) {
                Logger.LogDebug(string.Format("Player too far away from rock to get increased loot: {0}", nearbyDistance));
            } else {
                float skillFactor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.WoodCutting);
                float num2 = ValConfig.MiningLootFactor.Value * (skillFactor * 100f);
                Dictionary<GameObject, int> drops1 = new Dictionary<GameObject, int>();
                if (drops.m_drops != null) {
                    foreach (DropTable.DropData drop in drops.m_drops) {
                        if (ValConfig.SkipNonRockDropIncreases.Value) {
                            if (skipIncreaseDrops.Contains(drop.m_item.name)) {
                                continue;
                            }
                        }
                        float randomChanceRoll = UnityEngine.Random.value;
                        float lootdropchance = drops.m_dropChance;
                        if (ValConfig.SkillLevelBonusEnabledForMiningDropChance.Value)
                            lootdropchance = (float)(num2 * (drops.m_dropChance * 2.0) / 100.0);
                        if (lootdropchance > 1.0)
                            lootdropchance = 1f;
                        Logger.LogDebug(string.Format("Mining rock check roll: {0} <= {1}", lootdropchance, randomChanceRoll));
                        if (lootdropchance <= randomChanceRoll) {
                            Logger.LogDebug("Mining rock drop increase: " + drop.m_item.name + " failed drop roll");
                        } else {
                            int dropAmountExtra = 0;
                            Logger.LogDebug(string.Format("Mining rock drop current: {0}, max_drop: {1}", drops.m_dropMin, drops.m_dropMax));
                            float minInclusive = (float)((double)drops.m_dropMin * (double)num2 / 100.0);
                            float maxExclusive = (float)((double)drops.m_dropMax * (double)num2 / 100.0);
                            if (ValConfig.ReducedChanceDropsForLowAmountDrops.Value == true && minInclusive > 0.0 && maxExclusive > 0.0 && minInclusive != 1f) {
                                float dropAmountChanceRoll = UnityEngine.Random.Range(minInclusive, maxExclusive);
                                float dropAmountRandomChanceRoll = UnityEngine.Random.value;
                                if (dropAmountChanceRoll <= dropAmountRandomChanceRoll) {
                                    Logger.LogDebug($"Mining rock drop increase: {drop.m_item.name} failed amount roll {dropAmountChanceRoll} <= {dropAmountRandomChanceRoll}");
                                    continue;
                                }
                                dropAmountExtra = UnityEngine.Random.Range((int)minInclusive, (int)maxExclusive);
                            } else if ((double)minInclusive == (double)maxExclusive)
                                dropAmountExtra = Mathf.RoundToInt(minInclusive);
                            Logger.LogDebug($"Mining rock drop increase {drop.m_item.name} min_drop: {minInclusive}, max_drop: {maxExclusive} drop amount: {dropAmountExtra}");
                            if (drops1.ContainsKey(drop.m_item))
                                drops1[drop.m_item] += dropAmountExtra;
                            else
                                drops1.Add(drop.m_item, dropAmountExtra);
                        }
                    }
                }
                if (drops1.Count == 0)
                    return;
                Player.m_localPlayer.StartCoroutine(Mining.DropItemsAsync(drops1, position, 1f));
            }
        }

        private static IEnumerator DropItemsAsync(
          Dictionary<GameObject, int> drops,
          Vector3 centerPos,
          float dropArea) {
            int obj_spawns = 0;
            foreach (KeyValuePair<GameObject, int> drop in drops) {
                bool set_stack_size = false;
                int max_stack_size = 0;
                GameObject item = drop.Key;
                int amount = drop.Value;
                Logger.LogDebug(string.Format("Dropping {0} {1}", (object)item.name, (object)amount));
                for (int i = 0; i < amount; ++i) {
                    if (obj_spawns > 0 && obj_spawns % 10 == 0)
                        yield return (object)new WaitForSeconds(0.1f);
                    GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(item, centerPos, Quaternion.identity);
                    ++obj_spawns;
                    ItemDrop component1 = gameObject.GetComponent<ItemDrop>();
                    if (!set_stack_size) {
                        set_stack_size = true;
                        if ((bool)component1)
                            max_stack_size = component1.m_itemData.m_shared.m_maxStackSize;
                    }
                    if (component1 != null) {
                        int num = amount - i;
                        if (num > 0) {
                            if (amount > max_stack_size) {
                                component1.m_itemData.m_stack = max_stack_size;
                                i += max_stack_size;
                            } else {
                                component1.m_itemData.m_stack = num;
                                i += num;
                            }
                        }
                        component1.m_itemData.m_worldLevel = (int)(byte)Game.m_worldLevel;
                    }
                    Rigidbody component2 = gameObject.GetComponent<Rigidbody>();
                    if ((bool)component2) {
                        Vector3 insideUnitSphere = UnityEngine.Random.insideUnitSphere;
                        if ((double)insideUnitSphere.y < 0.0)
                            insideUnitSphere.y = 0.0f - insideUnitSphere.y;
                        component2.AddForce(insideUnitSphere * 5f, ForceMode.VelocityChange);
                    }
                }
                item = null;
            }
        }

        [HarmonyPatch(typeof(MineRock), nameof(MineRock.Damage))]
        public static class MinerockDmgPatch {
            public static void Prefix(HitData hit, MineRock __instance) {
                Mining.ModifyPickaxeDmg(hit, __instance);
            }
        }

        [HarmonyPatch(typeof(MineRock5), nameof(MineRock.Damage))]
        public static class Minerock5DmgPatch {
            public static void Prefix(HitData hit, MineRock5 __instance) {
                Mining.ModifyPickaxeDmg(hit, instance5: __instance);
            }
        }

        [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.RPC_SetAreaHealth))]
        public static class Minerock5DestroyPatch {
            public static void Postfix(MineRock5 __instance, long sender, int index, float health) {
                if (!ValConfig.EnableMining.Value || !(Player.m_localPlayer != null) || (double)health > 0.0)
                    return;
                Mining.IncreaseMiningDrops(__instance.m_dropItems, ((Component)__instance).gameObject.transform.position);
            }
        }

        [HarmonyPatch(typeof(Destructible), "Destroy")]
        public static class IncreaseDropsFromDestructibleRock {
            public static void Prefix(Destructible __instance, HitData hit) {
                if (!ValConfig.EnableMining.Value || __instance.m_destructibleType != DestructibleType.Default && __instance.m_destructibleType != DestructibleType.Tree || hit == null || !(Player.m_localPlayer != null) || hit.m_attacker == Player.m_localPlayer.GetZDOID())
                    return;
                Mining.IncreaseDestructibleMineDrops(__instance);
            }
        }
    }
}
