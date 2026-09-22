using HarmonyLib;
using ImpactfulSkills.common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace ImpactfulSkills.patches {
    public static class Mining {
        private static readonly int rockmask = LayerMask.GetMask("static_solid", "Default_small", "Default");
        private static Collider[] current_aoe_strike = (Collider[])null;
        private static bool rockbreaker_running = false;
        private static readonly List<string> skipIncreaseDrops = new List<string> { "LeatherScraps", "WitheredBone" };
        private static float rockbreakerActivatedAt = 0;
        // The leviathan whose dive reaction the in-flight sweep is gating, and whether that sweep
        // has already spent its single allowed roll. See LeviathanDiveRollGate.
        private static Leviathan sweep_leviathan = null;
        private static bool leviathan_reaction_rolled = false;

        // Mirrors vanilla MineRock.AllDestroyed. We cannot call that method itself, because
        // LeviathanNeverRemovedByMining prefixes it to always answer false for leviathans.
        private static bool AllAreasDestroyed(MineRock rock) {
            for (int i = 0; i < rock.m_hitAreas.Length; ++i) {
                if ((double)rock.m_nview.GetZDO().GetFloat("Health" + i.ToString(), rock.GetHealth()) > 0.0) { return false; }
            }
            return true;
        }

        private static void ArmLeviathanGate(Leviathan leviathan) {
            Mining.sweep_leviathan = leviathan;
            Mining.leviathan_reaction_rolled = false;
        }

        private static void ClearSweepState() {
            Mining.rockbreaker_running = false;
            Mining.current_aoe_strike = null;
            Mining.sweep_leviathan = null;
            Mining.leviathan_reaction_rolled = false;
        }

        private static void UnallowedMinablesChanged(object s, EventArgs e) {
            try {
                List<String> tunallowed = new List<String>() { };
                foreach (var item in ValConfig.SkipNonRockDropPrefabs.Value.Split(',')) {
                    tunallowed.Add(item);
                }
                if (tunallowed.Count > 0) {
                    skipIncreaseDrops.Clear();
                    skipIncreaseDrops.AddRange(tunallowed);
                }
            } catch (Exception ex) {
                Logger.LogWarning($"Error parsing SkipNonRockDropPrefabs: {ex}");
            }
        }

        public static void SetupMining() {
            try {
                foreach (var unallowed in ValConfig.SkipNonRockDropPrefabs.Value.Split(',')) {
                    skipIncreaseDrops.Add(unallowed);
                }
            } catch (Exception ex) {
                Logger.LogWarning($"Error parsing SkipNonRockDropPrefabs, defaults will be used.: {ex}");
                skipIncreaseDrops.AddRange(new List<string>() {
                    "LeatherScraps",
                    "WitheredBone" });
            }
            ValConfig.GatheringDisallowedItems.SettingChanged += UnallowedMinablesChanged;
        }

        public static void ModifyPickaxeDmg(HitData hit, MineRock instance = null, MineRock5 instance5 = null) {
            if (!ValConfig.EnableMining.Value || hit == null || Player.m_localPlayer == null || hit.m_attacker != Player.m_localPlayer.GetZDOID()) { return; }
            float skillFactor = ((Character)Player.m_localPlayer).GetSkillFactor(Skills.SkillType.Pickaxes);
            float num1 = (float)(1.0 + (double)ValConfig.MiningDmgMod.Value * ((double)skillFactor * 100.0) / 100.0);
            Logger.LogDebug($"Player mining dmg multiplier: {num1}");
            hit.m_damage.m_pickaxe *= num1;
            if (ValConfig.EnableMiningCritHit.Value && (double)skillFactor * 100.0 >= (double)ValConfig.RequiredLevelForMiningCrit.Value && (double)UnityEngine.Random.value <= (double)ValConfig.ChanceForMiningCritHit.Value) {
                Logger.LogDebug("Mining Critical hit activated");
                hit.m_damage.m_pickaxe *= ValConfig.CriticalHitDmgMult.Value;
            }
            // No damage will be done, skip.
            if ((double)hit.m_damage.m_pickaxe <= 0.0) { return; }

            // The AOE hotkey (ValConfig.AOEToggleHotkey) suppresses both mining sweeps. Only sweep code
            // follows; the damage and crit bonuses above still apply, so mining stays buffed while off.
            if (!ValConfig.AOEFeaturesEnabled) { return; }

            // Leviathans hold the player over open water/lava, and MineRock.RPC_Hit fires m_onHit
            // (-> Leviathan.OnHit -> dive roll) for every damaging hit. Our sweeps hit every node
            // at once, which turns a 1-10% dive chance into a near certainty and dumps the player.
            Leviathan leviathan = null;
            if (ValConfig.ProtectLeviathansWhenMined.Value) {
                leviathan = instance != null ? instance.GetComponentInParent<Leviathan>()
                          : instance5 != null ? instance5.GetComponentInParent<Leviathan>() : null;
                if (leviathan != null) {
                    ZNetView lnview = leviathan.GetComponent<ZNetView>();
                    // RPC_Hit early-returns for non-owners, so Leviathan.OnHit only ever runs on the
                    // peer that owns the ZDO. If that isn't us our gate would never be consulted and
                    // the sweep would machine-gun the dive reaction there. Leave the swing vanilla.
                    if (lnview == null || !lnview.IsValid() || !lnview.IsOwner()) {
                        Logger.LogDebug("Leviathan is owned by another peer, skipping mining sweeps.");
                        return;
                    }
                }
            }

            //safety reset
            if (rockbreaker_running == true && Mining.rockbreakerActivatedAt + ValConfig.RockbreakerSafetyResetTimeout.Value < Time.realtimeSinceStartup) {
                Mining.ClearSweepState();
            }
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
                    Mining.rockbreakerActivatedAt = Time.realtimeSinceStartup;
                    if (instance != null && Player.m_localPlayer != null) {
                        Logger.LogDebug("Rock breaker activated on minerock");
                        Mining.current_aoe_strike = instance.m_hitAreas;
                        Mining.ArmLeviathanGate(leviathan);
                        Player.m_localPlayer.StartCoroutine(Mining.MineAoeDamage(instance.m_hitAreas, aoedmg));
                    } else {
                        Mining.ClearSweepState();
                    }

                    // Needs to be gated to ensure we do not flip the rockbreaker flags again if we are already hitting a minerock
                    if (instance5 != null) {
                        if (Player.m_localPlayer != null) {
                            Logger.LogDebug("Rock breaker activated on minerock5");
                            List<Collider> colliderList = new List<Collider>();
                            foreach (MineRock5.HitArea hitArea in instance5.m_hitAreas) {
                                colliderList.Add(hitArea.m_collider);
                            }
                            Mining.current_aoe_strike = colliderList.ToArray();
                            Mining.ArmLeviathanGate(leviathan);
                            Player.m_localPlayer.StartCoroutine(Mining.MineAoeDamage(colliderList.ToArray(), aoedmg));
                        } else {
                            Mining.ClearSweepState();
                        }
                    }
                    return;
                }
            }
            // Check for AOE mining, guard clause
            if (!ValConfig.EnableMiningAOE.Value || (double)skillFactor * 100.0 < (double)ValConfig.MiningAOELevel.Value || Mining.current_aoe_strike != null) { return; }
                
            float chance = ValConfig.ChanceForAOEOnHit.Value;
            if (ValConfig.ChanceForAOEOnHitScalesWithSkill.Value) { chance -= 1f * skillFactor; }
            if (chance < 0f) { chance = 0f; }
            float aoe_roll = UnityEngine.Random.value;
            if (aoe_roll < chance) {
                Logger.LogDebug(string.Format($"AOE Mining failed roll: {aoe_roll} < {chance}"));
            } else {
                Logger.LogDebug($"Player mining aoe activated: {aoe_roll} > {chance}");
                Vector3 point = hit.m_point;
                HitData aoedmg = hit;
                double radius = (double)ValConfig.MiningAOERange.Value * (double)skillFactor;
                int rockmask = Mining.rockmask;
                Collider[] mine_targets = Physics.OverlapSphere(point, (float)radius, rockmask);
                Mining.current_aoe_strike = mine_targets;
                Mining.ArmLeviathanGate(leviathan);
                if (Player.m_localPlayer != null) {
                    Player.m_localPlayer.StartCoroutine(Mining.MineAoeDamage(mine_targets, aoedmg));
                } else {
                    Mining.ClearSweepState();
                }
            }
        }

        public static bool ArrayContains(Collider[] group, Collider target) {
            foreach (UnityEngine.Object @object in group) {
                if (@object == target) { return true; }
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
                Mining.ClearSweepState();
                yield break;
            }
            // Clear in a finally so an exception mid-sweep can't leave current_aoe_strike set, which
            // would block every later AOE and rock breaker roll until the game restarts.
            try {
                if (minerock != null || minerock5 != null) {
                    Collider[] colliderArray;
                    int index;
                    Collider obj_collider;
                    if (mine_targets != null) {
                        if (flag) {
                            colliderArray = mine_targets;
                            for (index = 0; index < colliderArray.Length; ++index) {
                                if (colliderArray == null || minerock == null) { break; }
                                obj_collider = colliderArray[index];
                                if (!(obj_collider == null)) {
                                    ++iterations;
                                    if (iterations % ValConfig.MinehitsPerInterval.Value == 0) { yield return new WaitForFixedUpdate(); }
                                    // The rock can be removed while we wait (see the MineRock5 branch below);
                                    // MineRock.Damage -> InvokeRPC would then dereference its null ZDO.
                                    if (minerock == null || minerock.m_nview == null || !minerock.m_nview.IsValid()) { break; }
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
                                if (colliderArray == null || minerock5 == null) { break; }
                                obj_collider = colliderArray[index];
                                if (!(obj_collider == null)) {
                                    ++iterations;
                                    if (iterations % ValConfig.MinehitsPerInterval.Value == 0)
                                        yield return new WaitForFixedUpdate();
                                    // We call the private DamageArea directly, bypassing MineRock5.Damage/RPC_Damage,
                                    // which normally guard on nview validity. Destroying the last hit area calls
                                    // m_nview.Destroy() -> ResetZDO() (m_zdo = null) while the GameObject lives until
                                    // end of frame, so the next DamageArea -> LoadHealth dereferences a null ZDO.
                                    // This must be checked after the yield, not before it: the sweep starts inside the
                                    // Damage prefix, so the player's own swing is applied while we wait and can break
                                    // the final areas out from under us.
                                    if (minerock5 == null || minerock5.m_nview == null || !minerock5.m_nview.IsValid() || minerock5.m_allDestroyed) { break; }
                                    int areaIndex = minerock5.GetAreaIndex(obj_collider);
                                    if (areaIndex >= 0) {
                                        Logger.LogDebug($"AOE Damage applying to minerock5 index: {areaIndex}");
                                        aoedmg.m_point = obj_collider.bounds.center;
                                        aoedmg.m_hitCollider = obj_collider;
                                        minerock5.DamageArea(areaIndex, aoedmg);
                                    }
                                }
                            }
                        }
                    }
                }
            } finally {
                Mining.ClearSweepState();
            }
        }

        public static void IncreaseDestructibleMineDrops(Destructible dmine) {
            if (dmine.m_spawnWhenDestroyed != null) { return; }
            Vector3 position = ((Component)dmine).transform.position;
            DropOnDestroyed component = ((Component)dmine).GetComponent<DropOnDestroyed>();
            if (component == null || component.m_dropWhenDestroyed == null) { return; }
            Mining.IncreaseMiningDrops(component.m_dropWhenDestroyed, position);
        }

        public static void IncreaseMiningDrops(DropTable drops, Vector3 position, HitData hitdata = null) {
            float nearbyDistance = Vector3.Distance(Player.m_localPlayer.transform.position, position);
            if (nearbyDistance > ValConfig.DistanceMiningDropMultiplierChecks.Value) {
                Logger.LogDebug(string.Format("Player too far away from rock to get increased loot: {0}", nearbyDistance));
            } else {
                float skillFactor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Pickaxes);
                float mineSkillFactor = ValConfig.MiningLootFactor.Value * (skillFactor * 100f);
                // (F - 1) so the factor is a true total multiplier for drop amounts: the vanilla drop still
                // spawns, and we add (F - 1) x base on top, scaled by skill. F = 1 -> vanilla, F = 2 -> 2x at
                // level 100. mineSkillFactor above is kept for the optional drop-chance bonus below.
                float mineLootAmountFactor = (ValConfig.MiningLootFactor.Value - 1f) * (skillFactor * 100f);
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
                            lootdropchance = (float)(mineSkillFactor * drops.m_dropChance / 100.0);
                        if (lootdropchance > 1.0)
                            lootdropchance = 1f;
                        Logger.LogDebug(string.Format("Mining rock check roll: {0} <= {1}", lootdropchance, randomChanceRoll));
                        if (lootdropchance <= randomChanceRoll) {
                            Logger.LogDebug("Mining rock drop increase: " + drop.m_item.name + " failed drop roll");
                        } else {
                            int dropAmountExtra = 0;
                            Logger.LogDebug(string.Format("Mining rock drop current: {0}, max_drop: {1}", drops.m_dropMin, drops.m_dropMax));
                            float minInclusive = (float)(drops.m_dropMin * mineLootAmountFactor / 100.0);
                            float maxExclusive = (float)(drops.m_dropMax * mineLootAmountFactor / 100.0);
                            if (ValConfig.ReducedChanceDropsForLowAmountDrops.Value == true && minInclusive > 0.0 && maxExclusive > 0.0 && minInclusive != 1f) {
                                float dropAmountChanceRoll = UnityEngine.Random.Range(minInclusive, maxExclusive);
                                float dropAmountRandomChanceRoll = UnityEngine.Random.value;
                                if (dropAmountChanceRoll <= dropAmountRandomChanceRoll) {
                                    Logger.LogDebug($"Mining rock drop increase: {drop.m_item.name} failed amount roll {dropAmountChanceRoll} <= {dropAmountRandomChanceRoll}");
                                    continue;
                                }
                            }

                            // Ensure Mining drops are set
                            if ((double)minInclusive == (double)maxExclusive) {
                                dropAmountExtra = Mathf.RoundToInt(minInclusive);
                            } else {
                                float amount = UnityEngine.Random.Range(minInclusive, maxExclusive);
                                dropAmountExtra = Mathf.RoundToInt(amount);
                            }

                            // Modify drops that would always be zero to be a chance, if enabled
                            if (dropAmountExtra < 1) {
                                float rnd = UnityEngine.Random.value;
                                if (ValConfig.FractionalDropsAsChance.Value && rnd <= minInclusive) {
                                    Logger.LogDebug($"Mining rock drop bonus result is less than 1. Roll success ({rnd} <= {minInclusive}) for dropping 1.");
                                    dropAmountExtra = 1;
                                } else {
                                    Logger.LogDebug($"Mining rock drop bonus result is less than 1. No bonus. ({rnd} <= {minInclusive})");
                                    dropAmountExtra = 0;
                                }
                            }

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

        // PatchAll builds a patch processor per type and skips containers that carry no class level
        // [HarmonyPatch], so this transpiler needs its own annotated class like every other patch
        // here; as a loose annotated method on Mining itself it was not reliably applied.
        [HarmonyPatch(typeof(MineRock), nameof(MineRock.RPC_Hit))]
        public static class MinerockDropsPatch {
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/) {
                var codeMatcher = new CodeMatcher(instructions);
                const string failure = "Unable to patch Minerock Drop increase.";
                if (codeMatcher.TryMatchEndForward(failure,
                        new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(MineRock), nameof(MineRock.m_destroyedEffect)))
                    ) && codeMatcher.TryMatchEndForward(failure,
                        new CodeMatch(OpCodes.Pop)
                    )) {
                    codeMatcher.InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldarg_0), // load __instance
                        Transpilers.EmitDelegate(ApplyIncreasedMiningDrops)
                    );
                }

                return codeMatcher.Instructions();
            }

            private static void ApplyIncreasedMiningDrops(MineRock __instance) {
                if (!ValConfig.EnableMining.Value || !(Player.m_localPlayer != null)) { return; }
                Mining.IncreaseMiningDrops(__instance.m_dropItems, __instance.gameObject.transform.position);
            }
        }

        // MineRock.RPC_Hit ends with `if (m_removeWhenDestroyed && AllDestroyed()) m_nview.Destroy();`,
        // which permanently deletes the whole leviathan the moment its last ore node breaks, taking
        // the body the player is standing on with it. Report "not all destroyed" so RPC_Hit leaves the
        // object alone; LeviathanSinksWhenMinedOut starts the dive instead.
        [HarmonyPatch(typeof(MineRock), nameof(MineRock.AllDestroyed))]
        public static class LeviathanNeverRemovedByMining {
            public static bool Prefix(MineRock __instance, ref bool __result) {
                if (!ValConfig.ProtectLeviathansWhenMined.Value) { return true; }
                if (__instance.GetComponent<Leviathan>() == null) { return true; }
                Logger.LogDebug("Blocked MineRock from removing a leviathan; sink will be scheduled instead.");
                __result = false;
                return false;
            }
        }

        // Detection lives here rather than in the prefix above so it still fires when
        // m_removeWhenDestroyed is false and AllDestroyed is short circuited away entirely.
        [HarmonyPatch(typeof(MineRock), nameof(MineRock.RPC_Hit))]
        public static class LeviathanSinksWhenMinedOut {
            public static void Postfix(MineRock __instance) {
                if (!ValConfig.ProtectLeviathansWhenMined.Value) { return; }
                Leviathan leviathan = __instance.GetComponent<Leviathan>();
                if (leviathan == null || __instance.m_nview == null || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner()) { return; }
                if (leviathan.m_left || leviathan.IsInvoking("Leave") || !Mining.AllAreasDestroyed(__instance)) { return; }
                // The same call vanilla's hit reaction uses, so the leviathan plays its leave effects
                // and dive animation, then removes itself once FixedUpdate sees the "submerged" tag.
                Logger.LogDebug($"Leviathan mined out, sinking in {leviathan.m_leaveDelay}s.");
                leviathan.Invoke("Leave", (float)leviathan.m_leaveDelay);
            }
        }

        // Leviathan.OnHit is wired to MineRock.m_onHit in Leviathan.Awake and rolls the dive reaction
        // on every damaging hit. Our sweeps deliver one hit per node inside a couple of frames, so
        // without this the leviathan almost always submerges and the player drowns (or burns, in the
        // Ashlands). Let the swing's first roll through, drop the rest.
        [HarmonyPatch(typeof(Leviathan), nameof(Leviathan.OnHit))]
        public static class LeviathanDiveRollGate {
            public static bool Prefix(Leviathan __instance) {
                if (Mining.sweep_leviathan == null || Mining.sweep_leviathan != __instance) { return true; }
                if (!Mining.leviathan_reaction_rolled) {
                    Mining.leviathan_reaction_rolled = true;
                    return true;
                }
                Logger.LogDebug("Suppressed a duplicate leviathan dive roll from a mining sweep.");
                return false;
            }
        }

        [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.Damage))]
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
                // Drop bonus loot at the fractured piece, not the whole deposit's pivot.
                // m_bound.m_pos is the area's world collider center captured at Awake, so it
                // stays valid even though UpdateMesh has already disabled the broken collider.
                MineRock5.HitArea hitArea = __instance.GetHitArea(index);
                Vector3 piecePos = hitArea != null ? hitArea.m_bound.m_pos : ((Component)__instance).gameObject.transform.position;
                Mining.IncreaseMiningDrops(__instance.m_dropItems, piecePos);
            }
        }

        [HarmonyPatch(typeof(Destructible), nameof(Destructible.Destroy))]
        public static class IncreaseDropsFromDestructibleRock {
            public static void Prefix(Destructible __instance, HitData hit) {
                //Logger.LogDebug($"Destructible destroy called, type: {__instance.m_destructibleType} {__instance.m_destructibleType != DestructibleType.Default} | hit_null? {hit == null} | playernull {!(Player.m_localPlayer != null)} | attacker is current player {hit.m_attacker == Player.m_localPlayer.GetZDOID()}");
                if (!ValConfig.EnableMining.Value || __instance.m_destructibleType != DestructibleType.Default || __instance.m_destructibleType == DestructibleType.Tree || hit == null || !(Player.m_localPlayer != null) || hit.m_attacker != Player.m_localPlayer.GetZDOID())
                    return;
                Mining.IncreaseDestructibleMineDrops(__instance);
            }
        }
    }
}
