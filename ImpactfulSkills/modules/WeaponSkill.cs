using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;
using System.Linq;
using System;
using static ItemDrop;
using UnityEngine;
using ImpactfulSkills.common;
using System.Reflection;

namespace ImpactfulSkills.patches
{
    internal class WeaponSkill
    {
        // ItemData item, int qualityLevel, bool crafting, float worldLevel, int stackOverride = -1, bool appending = false
        [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip), new Type[] { typeof(ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool) })]
        public static class ItemDisplay
        {
            public static void Postfix(ItemData item, ref String __result) {
                // Guard clause due to the postfixed method also having scenarios it can be called without the player defined
                if (ValConfig.EnableWeaponSkill.Value == false || Player.m_localPlayer == null) { return; }
                List<String> entry_lines = __result.Split('\n').ToList();
                List<String> result_lines = new List<string>(entry_lines);
                for (int i = 0; i < entry_lines.Count; i++) {
                    // Logger.LogDebug($"Checking line: {entry_lines[i]}");
                    // An item with m_appendToolTip builds its tooltip by concatenating a nested GetTooltip(appending: true)
                    // for the appended item, so the outer call sees lines this postfix already annotated on the inner one.
                    // Those belong to the appended item, not to `item` - leave them as the inner pass wrote them.
                    if (entry_lines[i].Contains("<color=yellow>")) { continue; }
                    if (entry_lines[i].Contains("item_staminause")) {
                        float player_skill = Player.m_localPlayer.GetSkillFactor(item.m_shared.m_skillType);
                        result_lines[i] = "$item_staminause: <color=orange>" + item.m_shared.m_attack.m_attackStamina + "</color> <color=yellow>(" + Mathf.RoundToInt(ModifyWeaponStaminaCostBySkillLevelInheritFactor(item.m_shared.m_attack.m_attackStamina, 0.33f, player_skill)) + ")</color>";
                        continue;
                    }
                    if (entry_lines[i].Contains("item_staminahold"))
                    {
                        // Vanilla reads the item's own skill for the draw drain, not always Bows.
                        float player_skill = Player.m_localPlayer.GetSkillFactor(item.m_shared.m_skillType);
                        float draw_drain = item.m_shared.m_attack.m_drawStaminaDrain;
                        result_lines[i] = "$item_staminahold: <color=orange>" + draw_drain + "</color> <color=yellow>(" + Mathf.RoundToInt(draw_drain * (1f - BowDrawStaminaReduction(0.33f, player_skill))) + ")</color>/s";
                        continue;
                    }
                }
                __result = String.Join("\n", result_lines);
            }
        }

        [HarmonyPatch(typeof(ItemDrop.ItemData))]
        public static class ModifyStaimaDrainBows
        {
            //[HarmonyEmitIL("./dumps")]
            //[HarmonyDebug]
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(ItemDrop.ItemData.GetDrawStaminaDrain))]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
            {
                var codeMatcher = new CodeMatcher(instructions);
                // Vanilla: drain - drain * 0.33 * skillFactor. The 0.33 stays on the stack and is handed to us, so a mod
                // that changes it still sets the floor, and we hand back what to multiply skillFactor by in its place.
                if (codeMatcher.TryMatchStartForward("Unable to patch Stamina reduction for bows compatibility.",
                    new CodeMatch(OpCodes.Dup),
                    new CodeMatch(OpCodes.Ldc_R4)
                )) {
                    codeMatcher.Advance(2).InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldarg_0),
                        Transpilers.EmitDelegate(ModifyStaminaDrainCostForBow)
                    );
                }

                return codeMatcher.Instructions();
            }
        }

        [HarmonyPatch(typeof(Humanoid))]
        public static class ParryGivesBonusXP
        {
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(Humanoid.BlockAttack))]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
            {
                var codeMatcher = new CodeMatcher(instructions);
                if (codeMatcher.TryMatchStartForward("Unable to patch extra XP for Parry block.",
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Humanoid), nameof(Humanoid.m_perfectBlockEffect)))
                )) {
                    codeMatcher.Advance(2).InsertAndAdvance(
                        Transpilers.EmitDelegate(ExtraXPForParryBlock)
                    );
                }

                return codeMatcher.Instructions();
            }
        }


        [HarmonyPatch(typeof(Attack))]
        public static class ModifyStaimaDrainWeapons
        {
            //[HarmonyEmitIL("./dumps")]
            //[HarmonyDebug]
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(Attack.GetAttackStamina))]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
            {
                var codeMatcher = new CodeMatcher(instructions);
                if (codeMatcher.TryMatchStartForward("Unable to patch stamina cost reduction modification, inherit modifier compatibility.",
                    new CodeMatch(OpCodes.Ldloc_0),
                    new CodeMatch(OpCodes.Ldloc_0),
                    new CodeMatch(OpCodes.Ldc_R4),
                    new CodeMatch(OpCodes.Mul),
                    new CodeMatch(OpCodes.Ldloc_1),
                    new CodeMatch(OpCodes.Mul),
                    new CodeMatch(OpCodes.Sub),
                    new CodeMatch(OpCodes.Stloc_0)
                )) {
                    codeMatcher
                    .RemoveInstruction()
                    .Advance(2)
                    .InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldloc_1),
                        Transpilers.EmitDelegate(ModifyWeaponStaminaCostBySkillLevelInheritFactor)
                    )
                    .RemoveInstructions(4);
                }
                return codeMatcher.Instructions();
            }
        }

        /// <summary>
        /// Stands in for vanilla's 0.33 in drain - drain * 0.33 * skillFactor. That constant is the share taken off per
        /// point of skill factor, so what we return is our share divided by the factor vanilla multiplies it by next.
        /// It used to return 0.33 minus our reduction, which shrank the share as the skill grew, so a higher setting
        /// made bows drain more than vanilla, and 1 removed the skill's reduction entirely.
        /// </summary>
        private static float ModifyStaminaDrainCostForBow(float reduce_factor, ItemDrop.ItemData item) {
            if (ValConfig.EnableWeaponSkill.Value != true || Player.m_localPlayer == null) { return reduce_factor; }
            // The same read vanilla makes, so dividing by it exactly cancels vanilla's multiply.
            float skillfactor = Player.m_localPlayer.GetSkillFactor(item.m_shared.m_skillType);
            if (skillfactor <= 0f) { return reduce_factor; }
            return BowDrawStaminaReduction(reduce_factor, skillfactor) / skillfactor;
        }

        /// <summary>
        /// Share of the draw drain taken off. Ours stops at its level 100 value unless WeaponSkillBonusesPastLevel100 is
        /// on (see SkillCaps), but is never less than vanilla's own, which a low setting or a skill boosted past 100 can
        /// beat. Stops at all of it, so drawing can become free but never pays stamina back.
        /// </summary>
        private static float BowDrawStaminaReduction(float reduce_factor, float skillfactor) {
            float vanilla_reduction = reduce_factor * skillfactor;
            float skilled_reduction = ValConfig.WeaponSkillBowDrawStaminaCostReduction.Value * SkillCaps.BonusFactor(skillfactor, ValConfig.WeaponSkillBonusesPastLevel100);
            return Mathf.Clamp01(Mathf.Max(vanilla_reduction, skilled_reduction));
        }

        private static float ModifyWeaponStaminaCostBySkillLevelInheritFactor(float stam_cost, float reduce_factor, float skillfactor) {
            float vanilla_stam_cost = stam_cost - stam_cost * reduce_factor * skillfactor;
            // Note even when disabled the stamina cost will be reduced because this is the vanilla formula
            if (ValConfig.EnableWeaponSkill.Value != true) {
                //  Logger.LogDebug($"Weapon stamina reduction o:{stam_cost} r:{stam_cost - vanilla_stam_cost}");
                return vanilla_stam_cost;
            }

            // skillfactor is vanilla's own, which other mods can push past 1, so ours stops at its level 100 value
            // unless WeaponSkillBonusesPastLevel100 is on (see SkillCaps). Vanilla's formula above keeps the factor it
            // was given.
            float stam_cost_reduction = stam_cost * ValConfig.WeaponSkillStaminaReduction.Value * SkillCaps.BonusFactor(skillfactor, ValConfig.WeaponSkillBonusesPastLevel100);
            stam_cost_reduction *= (1 + reduce_factor); // Inherit vanilla/other mod reduction factor into the new reduction calculation
            //Logger.LogDebug($"Weapon stamina reduction o:{stam_cost} - r:{stam_cost_reduction} = {(stam_cost - stam_cost_reduction)} s:{skillfactor}");
            // Never dearer than vanilla, which a low WeaponSkillStaminaReduction or a skill boosted past 100 would
            // otherwise make it. An attack can become free, but never pay stamina back.
            return Mathf.Max(0f, Mathf.Min(vanilla_stam_cost, stam_cost - stam_cost_reduction));
        }


        private static void ExtraXPForParryBlock() {
            if (ValConfig.EnableWeaponSkill.Value == true && Player.m_localPlayer != null) {
                Player.m_localPlayer.RaiseSkill(Skills.SkillType.Blocking, ValConfig.WeaponSkillParryBonus.Value);
            }
        }

        static class EquipSpeedHelper
        {
            public static float Apply(float duration, ItemDrop.ItemData item)
            {
                if (Player.m_localPlayer == null) return duration;
                float skillFactor = Player.m_localPlayer.GetSkillFactor(item.m_shared.m_skillType) * 100f;
                float speedMultiplier = 1f + (SkillCaps.BonusLevel(skillFactor, ValConfig.WeaponSkillBonusesPastLevel100) / 100 * ValConfig.WeaponSkillEquipSpeedFactor.Value);

                if (skillFactor >= ValConfig.WeaponSkillEquipRequiredLevel.Value)
                {
                    return duration / speedMultiplier;
                }
                return duration;
            }
        }


        [HarmonyPatch(typeof(Player), nameof(Player.QueueEquipAction))]
        public static class ModifyEquipSpeed_On
        {
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable <CodeInstruction> instructions)
            {
                FieldInfo equipDurationField = AccessTools.Field(typeof(ItemDrop.ItemData.SharedData), nameof(ItemDrop.ItemData.SharedData.m_equipDuration));

                CodeMatcher codeMatcher = new CodeMatcher(instructions);
                if (codeMatcher.TryMatchStartForward("Unable to patch equip speed.",
                    new CodeMatch(OpCodes.Ldfld, equipDurationField))) {
                    codeMatcher.Advance(1);
                    codeMatcher.Insert(
                        new CodeInstruction(OpCodes.Ldarg_1),
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(EquipSpeedHelper), nameof(EquipSpeedHelper.Apply))));
                }
                return codeMatcher.InstructionEnumeration();
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.QueueUnequipAction))]
        public static class ModifyEquipSpeed_Off
        {
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                FieldInfo equipDurationField = AccessTools.Field(typeof(ItemDrop.ItemData.SharedData), nameof(ItemDrop.ItemData.SharedData.m_equipDuration));

                CodeMatcher codeMatcher = new CodeMatcher(instructions);
                if (codeMatcher.TryMatchStartForward("Unable to patch unequip speed.",
                    new CodeMatch(OpCodes.Ldfld, equipDurationField))) {
                    codeMatcher.Advance(1);
                    codeMatcher.Insert(
                        new CodeInstruction(OpCodes.Ldarg_1),
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(EquipSpeedHelper), nameof(EquipSpeedHelper.Apply))));
                }
                return codeMatcher.InstructionEnumeration();
            }
        }

        /// <summary>
        /// Sprinting, jumping and dodging clear the players action queue, and a reload (crossbows, and the elemental or
        /// blood magic weapons that reload) is just a timed action in that queue. Vanilla re-queues the reload as soon as
        /// it is gone, but always from zero. Above the required level we note how far the reload got when one of these
        /// clears it, and hand that progress back to the reload vanilla queues next. Sprinting clears the queue every
        /// tick, so the reload holds still until the sprint ends. A reload drains its eitr while it runs, so a resumed one
        /// only costs the eitr for the time it has left.
        /// </summary>
        public static class ReloadPause
        {
            // Jump and dodge re-queue the reload on the very next tick, and sprinting saves again every tick, so a real
            // pause is always restored well inside this. It stops a stale pause coming back after a weapon swap. A
            // reload that waits on eitr to start again is kept from timing out by KeepPauseWhileWaiting.
            private const float PausedReloadTimeout = 1f;
            private static ItemDrop.ItemData PausedReloadWeapon;
            private static float PausedReloadProgress;
            private static float PausedReloadAt;

            /// <summary>
            /// Looks for the reload on either side of the method rather than rewriting its ClearActionQueue call. Other
            /// mods replace that same call (ZenPlayer's Change Gear While Moving swaps the one in CheckRun for its own
            /// clear that keeps equip actions), and only the first rewrite of a call wins, silently breaking the other.
            /// Watching from outside also catches the reload when that replacement removes it instead of vanilla.
            /// </summary>
            [HarmonyPatch]
            public static class PauseReloadOnMovement
            {
                static IEnumerable<MethodBase> TargetMethods() {
                    yield return AccessTools.DeclaredMethod(typeof(Player), nameof(Player.CheckRun));
                    yield return AccessTools.DeclaredMethod(typeof(Player), nameof(Player.OnJump));
                    yield return AccessTools.DeclaredMethod(typeof(Player), nameof(Player.UpdateDodge));
                }

                private static void Prefix(Player __instance, out Player.MinorActionData __state) {
                    __state = null;
                    if (ValConfig.EnableWeaponSkill.Value && (ValConfig.EnableCrossbowReloadPause.Value || ValConfig.EnableMagicReloadPause.Value) && __instance == Player.m_localPlayer) {
                        __state = FindReload(__instance);
                    }
                }

                // Nothing in these methods finishes a reload, so one that went missing was cleared away.
                private static void Postfix(Player __instance, Player.MinorActionData __state) {
                    if (__state == null || __state.m_time <= 0f || __instance.m_actionQueue.Contains(__state)) { return; }
                    if (!CanPause(__instance, __state.m_item)) { return; }
                    PausedReloadWeapon = __state.m_item;
                    PausedReloadProgress = __state.m_time;
                    PausedReloadAt = Time.time;
                }
            }

            /// <summary>
            /// Vanilla only re-queues a reload that drains eitr once there is enough eitr to start it, which can take well
            /// past the timeout when the reload that was interrupted used most of it. While the paused weapon is still out
            /// and unloaded with no reload queued, the pause is kept fresh. Swapping weapons stops this, so the timeout
            /// still clears a stale pause.
            /// </summary>
            [HarmonyPatch(typeof(Player), nameof(Player.UpdateWeaponLoading))]
            public static class KeepPauseWhileWaiting
            {
                private static void Postfix(Player __instance, ItemDrop.ItemData weapon) {
                    if (PausedReloadWeapon == null || weapon != PausedReloadWeapon) { return; }
                    if (__instance.m_weaponLoaded != weapon && FindReload(__instance) == null) {
                        PausedReloadAt = Time.time;
                    }
                }
            }

            [HarmonyPatch(typeof(Player), nameof(Player.QueueReloadAction))]
            public static class ResumePausedReload
            {
                private static void Postfix(Player __instance) {
                    if (PausedReloadWeapon == null) { return; }
                    Player.MinorActionData reload = FindReload(__instance);
                    if (reload == null) { return; }

                    if (reload.m_item == PausedReloadWeapon && reload.m_time == 0f && Time.time - PausedReloadAt <= PausedReloadTimeout) {
                        reload.m_time = PausedReloadProgress;
                    }
                    PausedReloadWeapon = null;
                }
            }

            private static Player.MinorActionData FindReload(Player player) {
                foreach (Player.MinorActionData action in player.m_actionQueue) {
                    if (action.m_type == Player.MinorActionData.ActionType.Reload) { return action; }
                }
                return null;
            }

            // Elemental and blood magic weapons have their own settings, every other weapon that reloads (crossbows,
            // and any a mod adds) uses the crossbow ones. Each is checked against the weapon's own skill.
            private static bool CanPause(Player player, ItemDrop.ItemData weapon) {
                Skills.SkillType skill = weapon.m_shared.m_skillType;
                if (skill == Skills.SkillType.ElementalMagic || skill == Skills.SkillType.BloodMagic) {
                    return ValConfig.EnableMagicReloadPause.Value && player.GetSkillLevel(skill) >= ValConfig.MagicReloadPauseLevel.Value;
                }
                return ValConfig.EnableCrossbowReloadPause.Value && player.GetSkillLevel(skill) >= ValConfig.CrossbowReloadPauseLevel.Value;
            }
        }

        /// <summary>
        /// Crossbow and bow hits on a creature grant more XP the further the target is from the shooter. Projectile.OnHit
        /// runs on the shooters client, so m_owner is the local player.
        /// </summary>
        [HarmonyPatch(typeof(Projectile), nameof(Projectile.OnHit))]
        public static class RangedDistanceXP
        {
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codeMatcher = new CodeMatcher(instructions);
                // m_raiseSkillAmount is also copied into the HitData earlier on, only the load feeding RaiseSkill is ours.
                if (codeMatcher.TryMatchStartForward("Unable to patch ranged distance XP.",
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Projectile), nameof(Projectile.m_raiseSkillAmount))),
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(Character), nameof(Character.RaiseSkill)))
                )) {
                    codeMatcher.Advance(1).InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldarg_2),
                        Transpilers.EmitDelegate(ScaleRangedXPByDistance)
                    );
                }
                return codeMatcher.Instructions();
            }

            private static float ScaleRangedXPByDistance(float xp, Projectile projectile, Vector3 hitPoint) {
                if (ValConfig.EnableWeaponSkill.Value == false || !(projectile.m_owner is Player player)) { return xp; }

                if (projectile.m_skill == Skills.SkillType.Crossbows && ValConfig.EnableCrossbowDistanceXP.Value) {
                    float distance = Vector3.Distance(player.transform.position, hitPoint);
                    float bonus = DistanceXPBonus(distance, 0f, ValConfig.CrossbowDistanceXPMaxDistance.Value, ValConfig.CrossbowDistanceXPMaxBonus.Value, 1f);
                    Logger.LogDebug($"Crossbow hit at {distance:0.0}m, XP bonus {bonus:0.00} ({xp} -> {xp * (1f + bonus)})");
                    return xp * (1f + bonus);
                }

                if (projectile.m_skill == Skills.SkillType.Bows && ValConfig.EnableBowDistanceXP.Value) {
                    // Arrows fly slower and for longer than bolts, so this measures from where the arrow was loosed
                    // rather than where the player is now, or backing off while it is in the air would add distance.
                    // m_startPoint is set once in Setup and never moves.
                    float distance = Vector3.Distance(projectile.m_startPoint, hitPoint);
                    float bonus = DistanceXPBonus(distance, ValConfig.BowDistanceXPMinDistance.Value, ValConfig.BowDistanceXPMaxDistance.Value, ValConfig.BowDistanceXPMaxBonus.Value, ValConfig.BowDistanceXPCurve.Value);
                    Logger.LogDebug($"Bow hit at {distance:0.0}m, XP bonus {bonus:0.00} ({xp} -> {xp * (1f + bonus)})");
                    return xp * (1f + bonus);
                }

                return xp;
            }

            /// <summary>
            /// No bonus up to minDistance, the full maxBonus from maxDistance on, and in between the share of the way
            /// there raised to curve. A curve of 1 grows evenly, higher keeps more of the bonus for the longest hits.
            /// A max at or inside the min (a misconfigured server) gives the full bonus past the max and none before it.
            /// </summary>
            private static float DistanceXPBonus(float distance, float minDistance, float maxDistance, float maxBonus, float curve) {
                float range = maxDistance - minDistance;
                float progress = range > 0f ? Mathf.Clamp01((distance - minDistance) / range) : (distance >= maxDistance ? 1f : 0f);
                return maxBonus * Mathf.Pow(progress, Mathf.Max(curve, 1f));
            }
        }

    }
}
