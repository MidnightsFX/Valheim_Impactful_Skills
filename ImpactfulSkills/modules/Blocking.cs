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
                    __result = baseBlock + baseBlock * (skillFactor) * ValConfig.BlockPowerFactor.Value; // its configuraable!
                    // noisey
                    //Logger.LogDebug($"Blocking bonus enabled | original: {baseBlock}, improved {__result}");
                }
            }
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.BlockAttack))]
        internal class BlockStaminaGain_Patch {
            [HarmonyPostfix]
            static void Postfix(Humanoid __instance, bool __result) {
                if (ValConfig.EnableBlocking.Value != true || Player.m_localPlayer == null || (!__result) || __instance != Player.m_localPlayer) { return; }

                float skillLevel = Player.m_localPlayer.GetSkillLevel(Skills.SkillType.Blocking);
                float m_blockTimer = __instance.m_blockTimer;
                bool parriedYes = __instance.GetCurrentBlocker().m_shared.m_timedBlockBonus > 1f && m_blockTimer != -1f && m_blockTimer < 0.25f; // parry flag

                // no stam gain on parry if configed
                if (ValConfig.EnableParryStaminaGain.Value != true && parriedYes == true) { return; }

                if (skillLevel >= ValConfig.BlockStaminaGainRequiredLevel.Value) {
                    float staminaGain = (skillLevel / 100) * ValConfig.BlockStaminaGainFactor.Value * __instance.m_blockStaminaDrain;
                    Logger.LogDebug($"Blocking returning stamina {staminaGain}, full cost: {__instance.m_blockStaminaDrain}");
                    __instance.AddStamina(staminaGain);
                }
            }
        }
    }
}
