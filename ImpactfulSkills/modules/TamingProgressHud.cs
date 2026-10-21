using HarmonyLib;
using ImpactfulSkills.common;
using Jotunn.Managers;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ImpactfulSkills.patches
{
    /// <summary>
    /// A line above the stamina, adrenaline and eitr bars with the taming progress of a creature being tamed. Vanilla
    /// only shows it in the hover text, which needs you within a few meters, close enough to frighten most creatures.
    /// It shows the creature you are looking at, or else the closest one being tamed within TamingProgressDisplayRange.
    ///
    /// Everything shown is read from the creature's ZDO, so it is the same on every client and needs no RPC.
    /// </summary>
    public static class TamingProgressHud
    {
        private const float PickInterval = 0.25f;
        // Above the stamina bar's root, clearing the eitr bar that sits on top of it. Vanilla moves that root up while
        // the build or ship HUD is open, and this follows it.
        private const float OffsetAboveStamina = 62f;

        private static TextMeshProUGUI progressText;
        private static Hud failedHud;
        private static float nextPick;
        private static Tameable shown;

        [HarmonyPatch(typeof(Hud), nameof(Hud.Update))]
        public static class ShowTamingProgress
        {
            private static void Postfix(Hud __instance) {
                Player player = Player.m_localPlayer;
                if (!ValConfig.EnableAnimalWhisper.Value || !ValConfig.EnableTamingProgressDisplay.Value || player == null) {
                    Hide();
                    return;
                }

                if (Time.time >= nextPick) {
                    nextPick = Time.time + PickInterval;
                    shown = PickCreature(player);
                    // Unity-overloaded null check is false once the Hud is destroyed (e.g. logout).
                    if (shown != null && progressText == null && (failedHud == __instance || !TryCreateText(__instance))) { return; }
                    if (progressText != null) {
                        progressText.text = shown != null ? ProgressLine(shown) : "";
                    }
                }

                if (shown == null || progressText == null) {
                    Hide();
                    return;
                }
                Vector2 stamina = __instance.m_staminaBar2Root.anchoredPosition;
                progressText.rectTransform.anchoredPosition = new Vector2(stamina.x, stamina.y + OffsetAboveStamina);
                progressText.enabled = true;
            }
        }

        private static void Hide() {
            if (progressText != null) { progressText.enabled = false; }
        }

        private static Tameable PickCreature(Player player) {
            float range = ValConfig.TamingProgressDisplayRange.Value;
            Vector3 position = player.transform.position;

            Character hovered = player.GetHoverCreature();
            if (hovered != null) {
                Tameable tameable = hovered.GetComponent<Tameable>();
                if (AnimalTaming.IsBeingTamed(tameable) && Vector3.Distance(position, hovered.transform.position) <= range) {
                    return tameable;
                }
            }

            Tameable closest = null;
            float closestDistance = range;
            List<Character> characters = Character.GetAllCharacters();
            foreach (Character character in characters) {
                if (character == null || character.IsPlayer() || character.IsTamed()) { continue; }
                float distance = Vector3.Distance(position, character.transform.position);
                if (distance > closestDistance) { continue; }
                Tameable tameable = character.GetComponent<Tameable>();
                if (AnimalTaming.IsBeingTamed(tameable)) {
                    closest = tameable;
                    closestDistance = distance;
                }
            }
            return closest;
        }

        private static string ProgressLine(Tameable tameable) {
            string line = LocalizationManager.Instance.TryTranslate("$taming_progress")
                .Replace("{0}", Localization.instance.Localize(tameable.m_character.m_name))
                .Replace("{1}", tameable.GetTameness().ToString())
                .Replace("{2}", Localization.instance.Localize(tameable.GetStatusString()));
            if (AnimalTaming.HandFeedingEnabled) {
                float wait = AnimalTaming.HandFeedCooldownLeft(tameable);
                line += ", " + (wait > 0f
                    ? LocalizationManager.Instance.TryTranslate("$taming_feed_wait").Replace("{0}", Mathf.CeilToInt(wait).ToString())
                    : LocalizationManager.Instance.TryTranslate("$taming_feed_ready"));
            }
            return line;
        }

        private static bool TryCreateText(Hud hud) {
            if (hud.m_rootObject == null || hud.m_staminaBar2Root == null || hud.m_hoverName == null) {
                Logger.LogWarning("Taming progress HUD: could not find the HUD root, stamina bar or hover text.");
                failedHud = hud;
                return false;
            }

            // Under the HUD root, so it hides along with the rest of the HUD.
            GameObject line = new GameObject("ImpactfulSkills_TamingProgress", typeof(RectTransform));
            line.layer = hud.m_rootObject.layer;
            RectTransform rect = (RectTransform)line.transform;
            rect.SetParent(hud.m_rootObject.transform, false);
            // The same anchor and pivot as the resource bars, so it shares their positions.
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(600f, 30f);

            progressText = line.AddComponent<TextMeshProUGUI>();
            progressText.font = hud.m_hoverName.font;
            progressText.fontSharedMaterial = hud.m_hoverName.fontSharedMaterial;
            progressText.fontSize = hud.m_hoverName.fontSize > 0f ? hud.m_hoverName.fontSize * 0.85f : 16f;
            progressText.color = hud.m_hoverName.color;
            progressText.alignment = TextAlignmentOptions.Bottom;
            progressText.overflowMode = TextOverflowModes.Overflow;
            progressText.raycastTarget = false;
            progressText.enabled = false;
            return true;
        }
    }
}
