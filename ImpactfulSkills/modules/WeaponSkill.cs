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
                        float player_skill = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Bows); // drawStaminaDrain - drawStaminaDrain * 0.33f * skillFactor;
                        result_lines[i] = "$item_staminahold: <color=orange>" + item.m_shared.m_attack.m_drawStaminaDrain + "</color> <color=yellow>(" + Mathf.RoundToInt(item.m_shared.m_attack.m_drawStaminaDrain - item.m_shared.m_attack.m_drawStaminaDrain *  ValConfig.WeaponSkillBowDrawStaminaCostReduction.Value * player_skill) + ")</color>/s";
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
                if (codeMatcher.TryMatchStartForward("Unable to patch Stamina reduction for bows compatibility.",
                    new CodeMatch(OpCodes.Dup),
                    new CodeMatch(OpCodes.Ldc_R4)
                )) {
                    codeMatcher.Advance(1).RemoveInstructions(1).InsertAndAdvance(
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

        private static float ModifyStaminaDrainCostForBow() {
            if (ValConfig.EnableWeaponSkill.Value != true || Player.m_localPlayer == null) { return 0.33f; }
            float skilled_cost = 0.33f - (ValConfig.WeaponSkillBowDrawStaminaCostReduction.Value * 0.33f * Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Bows));
            if (skilled_cost <0.01f) { skilled_cost = 0.01f; }
            return skilled_cost;
        }

        private static float ModifyWeaponStaminaCostBySkillLevelInheritFactor(float stam_cost, float reduce_factor, float skillfactor) {
            // Note even when disabled the stamina cost will be reduced because this is the vanilla formula
            if (ValConfig.EnableWeaponSkill.Value != true) {
                float vanilla_stam_reduction = stam_cost * reduce_factor * skillfactor;
                //  Logger.LogDebug($"Weapon stamina reduction o:{stam_cost} r:{vanilla_stam_reduction}");
                return (stam_cost - vanilla_stam_reduction);
            }

            float stam_cost_reduction = stam_cost * ValConfig.WeaponSkillStaminaReduction.Value * skillfactor;
            stam_cost_reduction *= (1 + reduce_factor); // Inherit vanilla/other mod reduction factor into the new reduction calculation
            //Logger.LogDebug($"Weapon stamina reduction o:{stam_cost} - r:{stam_cost_reduction} = {(stam_cost - stam_cost_reduction)} s:{skillfactor}");
            return (stam_cost - stam_cost_reduction);
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
                float speedMultiplier = 1f + (skillFactor / 100 * ValConfig.WeaponSkillEquipSpeedFactor.Value);

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
        /// Sprinting, jumping and dodging clear the players action queue, and a crossbow reload is just a timed action
        /// in that queue. Vanilla re-queues the reload as soon as it is gone, but always from zero. Above the required
        /// level we note how far the reload got when one of these clears it, and hand that progress back to the reload
        /// vanilla queues next. Sprinting clears the queue every tick, so the reload holds still until the sprint ends.
        /// </summary>
        public static class CrossbowReloadPause
        {
            // Jump and dodge re-queue the reload on the very next tick, and sprinting saves again every tick, so a real
            // pause is always restored well inside this. It stops a stale pause coming back after a weapon swap.
            private const float PausedReloadTimeout = 1f;
            private static ItemDrop.ItemData PausedReloadWeapon;
            private static float PausedReloadProgress;
            private static float PausedReloadAt;

            [HarmonyPatch]
            public static class PauseReloadOnMovement
            {
                static IEnumerable<MethodBase> TargetMethods() {
                    yield return AccessTools.DeclaredMethod(typeof(Player), nameof(Player.CheckRun));
                    yield return AccessTools.DeclaredMethod(typeof(Player), nameof(Player.OnJump));
                    yield return AccessTools.DeclaredMethod(typeof(Player), nameof(Player.UpdateDodge));
                }

                [HarmonyTranspiler]
                static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
                {
                    var codeMatcher = new CodeMatcher(instructions);
                    if (codeMatcher.TryMatchStartForward($"Unable to patch crossbow reload pause for {original.Name}.",
                        new CodeMatch(i => i.opcode == OpCodes.Callvirt && i.operand is MethodInfo method && method.Name == nameof(Player.ClearActionQueue))
                    )) {
                        // Set keeps any labels on the call. The player is already on the stack as the call's instance.
                        codeMatcher.Set(OpCodes.Call, AccessTools.Method(typeof(CrossbowReloadPause), nameof(ClearActionQueuePausingReload)));
                    }
                    return codeMatcher.Instructions();
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

            private static void ClearActionQueuePausingReload(Player player) {
                if (ValConfig.EnableWeaponSkill.Value && ValConfig.EnableCrossbowReloadPause.Value && player == Player.m_localPlayer) {
                    Player.MinorActionData reload = FindReload(player);
                    if (reload != null && reload.m_time > 0f && player.GetSkillLevel(reload.m_item.m_shared.m_skillType) >= ValConfig.CrossbowReloadPauseLevel.Value) {
                        PausedReloadWeapon = reload.m_item;
                        PausedReloadProgress = reload.m_time;
                        PausedReloadAt = Time.time;
                    }
                }
                player.ClearActionQueue();
            }

            private static Player.MinorActionData FindReload(Player player) {
                foreach (Player.MinorActionData action in player.m_actionQueue) {
                    if (action.m_type == Player.MinorActionData.ActionType.Reload) { return action; }
                }
                return null;
            }
        }

        /// <summary>
        /// Crossbow hits on a creature grant more XP the further the target is from the shooter. Projectile.OnHit runs
        /// on the shooters client, so m_owner is the local player.
        /// </summary>
        [HarmonyPatch(typeof(Projectile), nameof(Projectile.OnHit))]
        public static class CrossbowDistanceXP
        {
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codeMatcher = new CodeMatcher(instructions);
                // m_raiseSkillAmount is also copied into the HitData earlier on, only the load feeding RaiseSkill is ours.
                if (codeMatcher.TryMatchStartForward("Unable to patch crossbow distance XP.",
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Projectile), nameof(Projectile.m_raiseSkillAmount))),
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(Character), nameof(Character.RaiseSkill)))
                )) {
                    codeMatcher.Advance(1).InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldarg_2),
                        Transpilers.EmitDelegate(ScaleCrossbowXPByDistance)
                    );
                }
                return codeMatcher.Instructions();
            }

            private static float ScaleCrossbowXPByDistance(float xp, Projectile projectile, Vector3 hitPoint) {
                if (ValConfig.EnableWeaponSkill.Value == false || ValConfig.EnableCrossbowDistanceXP.Value == false) { return xp; }
                if (projectile.m_skill != Skills.SkillType.Crossbows || !(projectile.m_owner is Player player)) { return xp; }

                float distance = Vector3.Distance(player.transform.position, hitPoint);
                float bonus = ValConfig.CrossbowDistanceXPMaxBonus.Value * Mathf.Clamp01(distance / ValConfig.CrossbowDistanceXPMaxDistance.Value);
                Logger.LogDebug($"Crossbow hit at {distance:0.0}m, XP bonus {bonus:0.00} ({xp} -> {xp * (1f + bonus)})");
                return xp * (1f + bonus);
            }
        }

    }
}
