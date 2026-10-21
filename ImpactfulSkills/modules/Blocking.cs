using HarmonyLib;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ImpactfulSkills.patches {
    public static class Blocking {
        [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetBlockPower), typeof(int), typeof(float))]
        internal class BlockPower_Patch { 
            [HarmonyPostfix]
            static void Postfix(float skillFactor, ref float __result) {
                if (ValConfig.EnableBlocking.Value != true || Player.m_localPlayer == null) { return; }

                if (Player.m_localPlayer.GetSkillLevel(Skills.SkillType.Blocking) >= ValConfig.BlockPowerRequiredLevel.Value) {
                    float baseBlock = __result / (1f + (skillFactor) * 0.5f); // Vanilla block
                    // Our share stops at its level 100 value unless BlockingBonusesPastLevel100 is on (see SkillCaps), but
                    // never drops below what vanilla already gave a skill boosted past 100.
                    __result = Mathf.Max(__result, baseBlock + baseBlock * SkillCaps.BonusFactor(skillFactor, ValConfig.BlockingBonusesPastLevel100) * ValConfig.BlockPowerFactor.Value); // its configuraable!
                    // noisey
                    //Logger.LogDebug($"Blocking bonus enabled | original: {baseBlock}, improved {__result}");
                }
            }
        }

        /// <summary>
        /// Gives back a share of the stamina a block cost. Vanilla charges m_blockStaminaDrain scaled by how much of the
        /// shield's block power the hit used (a parry charges m_perfectBlockStaminaDrain instead), then equipment and
        /// status effects adjust it. The refund used to be a share of the full m_blockStaminaDrain whatever was charged,
        /// so blocking a hit well under your block power gave back more stamina than it took. It is now a share of what
        /// the block's charges took, counted by CountBlockStaminaCharge.
        /// </summary>
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.BlockAttack))]
        internal class BlockStaminaGain_Patch {
            // Only true while the local player's BlockAttack runs, so nothing else that reads block stamina is counted.
            // Only the local player's call touches these: knocking the attacker back after a block can run that
            // character's own BlockAttack inside ours, and it must not reset the count.
            internal static bool CountingCharges;
            internal static float StaminaCharged;

            [HarmonyPrefix]
            static void Prefix(Humanoid __instance, out bool __state) {
                __state = false;
                if (Player.m_localPlayer == null || __instance != Player.m_localPlayer) { return; }
                StaminaCharged = 0f;
                CountingCharges = ValConfig.EnableBlocking.Value == true;
                // Vanilla's own parry test, made before it runs.
                ItemDrop.ItemData blocker = __instance.GetCurrentBlocker();
                __state = blocker != null && blocker.m_shared.m_timedBlockBonus > 1f && __instance.m_blockTimer != -1f && __instance.m_blockTimer < 0.25f;
            }

            [HarmonyPostfix]
            static void Postfix(Humanoid __instance, bool __result, bool __state) {
                if (Player.m_localPlayer == null || __instance != Player.m_localPlayer) { return; }
                CountingCharges = false;
                if (ValConfig.EnableBlocking.Value != true || (!__result)) { return; }

                // no stam gain on parry if configed
                if (ValConfig.EnableParryStaminaGain.Value != true && __state == true) { return; }

                float skillLevel = Player.m_localPlayer.GetSkillLevel(Skills.SkillType.Blocking);
                if (skillLevel >= ValConfig.BlockStaminaGainRequiredLevel.Value && StaminaCharged > 0f) {
                    float staminaGain = (SkillCaps.BonusLevel(skillLevel, ValConfig.BlockingBonusesPastLevel100) / 100) * ValConfig.BlockStaminaGainFactor.Value * StaminaCharged;
                    Logger.LogDebug($"Blocking returning stamina {staminaGain}, block cost: {StaminaCharged}");
                    __instance.AddStamina(staminaGain);
                }
            }

            // A throw skips the postfix, which would leave other block stamina reads being counted.
            [HarmonyFinalizer]
            static void Finalizer(Humanoid __instance) {
                if (__instance == Player.m_localPlayer) { CountingCharges = false; }
            }
        }

        /// <summary>
        /// BlockAttack charges its stamina right after this settles the cost, once for a block and again for a parry
        /// whose shield has no stamina regen. Reading it here picks up every adjustment without matching vanilla's IL.
        /// </summary>
        [HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyBlockStaminaUsage))]
        internal class CountBlockStaminaCharge {
            // Last, so other mods' changes to the cost are already in.
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            static void Postfix(SEMan __instance, ref float staminaUse) {
                // The attacker's own block can run inside ours when it is knocked back, so only the player's counts.
                if (!BlockStaminaGain_Patch.CountingCharges || __instance.m_character != Player.m_localPlayer || staminaUse <= 0f) { return; }
                // What UseStamina is about to take: scaled by the world's stamina rate, and never more than the player has left.
                BlockStaminaGain_Patch.StaminaCharged += Mathf.Min(staminaUse * Game.m_staminaRate, Player.m_localPlayer.GetStamina());
            }
        }
    }
}
