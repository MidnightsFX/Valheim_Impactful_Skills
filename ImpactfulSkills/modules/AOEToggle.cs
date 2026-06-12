using HarmonyLib;
using ImpactfulSkills.modules.Multiplant;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ImpactfulSkills.patches {

    /// <summary>
    /// Per-frame detection of the configurable AOE toggle hotkey. Flipping the shared
    /// <see cref="ValConfig.AOEFeaturesEnabled"/> flag enables/disables BOTH AOE harvesting
    /// (see Gathering.cs) and AOE planting at once, and shows a top-right feedback message.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.Update))]
    public static class AOEToggleHotkeyPatch {
        static void Postfix(Player __instance) {
            if (Player.m_localPlayer == null || __instance != Player.m_localPlayer) { return; }
            // Ignore the hotkey while typing in chat or the developer console.
            if (Chat.instance != null && Chat.instance.HasFocus()) { return; }
            if (Console.IsVisible()) { return; }

            if (ValConfig.AOEToggleHotkey.Value.IsDown()) {
                ValConfig.AOEFeaturesEnabled = !ValConfig.AOEFeaturesEnabled;
                // Mirror into the planting flag so the existing ghost show/hide logic works unchanged.
                PlantGrid.MultiplantDisabled = !ValConfig.AOEFeaturesEnabled;

                string msg = ValConfig.AOEFeaturesEnabled
                    ? Localization.instance.Localize("$aoe_enabled")
                    : Localization.instance.Localize("$aoe_disabled");
                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, msg);
            }
        }
    }

    /// <summary>
    /// Renders a single key hint ("[key] Toggle AOE") in the build placement HUD while the
    /// planting grid is active, using the live configured key so rebinds reflect immediately.
    /// </summary>
    [HarmonyPatch(typeof(KeyHints), nameof(KeyHints.UpdateHints))]
    public static class AOEKeyHintPatch {
        private static GameObject _hintObj;
        private static TextMeshProUGUI _keyText;

        static void Postfix(KeyHints __instance) {
            bool show = PlantGrid.GridPlantingActive
                        && PlantGrid.HoldingCultivator()
                        && ValConfig.EnableFarmingMultiPlant.Value;

            if (!show) {
                // Unity-overloaded null check is false once the object is destroyed (e.g. world reload).
                if (_hintObj != null) { _hintObj.SetActive(false); }
                return;
            }

            // (Re)create lazily; _hintObj == null is also true after the prior KeyHints was destroyed.
            if (_hintObj == null && !TryCreateHint(__instance)) { return; }

            _hintObj.SetActive(true);
            // Read fresh each frame (full shortcut, including any modifiers) so rebinds update live.
            _keyText.text = ValConfig.AOEToggleHotkey.Value.ToString();
        }

        /// <summary>
        /// Clones an existing keyboard build-hint row ("Copy") under m_buildHints/Keyboard, reusing
        /// its label + single-key layout. Hierarchy mirrors Advize PlantEasily's proven approach.
        /// </summary>
        private static bool TryCreateHint(KeyHints instance) {
            if (instance.m_buildHints == null) { return false; }

            Transform keyboardRoot = instance.m_buildHints.transform.Find("Keyboard");
            if (keyboardRoot == null) {
                Logger.LogWarning("AOE toggle hint: could not find 'Keyboard' build-hint root.");
                return false;
            }
            Transform template = keyboardRoot.Find("Copy");
            if (template == null) {
                Logger.LogWarning("AOE toggle hint: could not find 'Copy' build-hint template.");
                return false;
            }

            _hintObj = Object.Instantiate(template.gameObject, keyboardRoot);
            _hintObj.name = "AOEToggleHint";

            Transform hintRoot = _hintObj.transform;
            Transform labelTf = hintRoot.GetChild(0);
            TextMeshProUGUI label = labelTf.GetComponent<TextMeshProUGUI>();
            if (label != null) { label.text = Localization.instance.Localize("$aoe_toggle_hint"); }
            // Widen the label so "Toggle AOE" is not truncated to the narrow "Copy" width.
            LayoutElement labelLayout = labelTf.GetComponent<LayoutElement>();
            if (labelLayout != null) { labelLayout.preferredWidth = 75; }

            _keyText = hintRoot.GetChild(1).GetChild(0).GetComponent<TextMeshProUGUI>();
            if (_keyText == null) {
                Logger.LogWarning("AOE toggle hint: cloned row is missing its key text component.");
                Object.Destroy(_hintObj);
                _hintObj = null;
                return false;
            }
            return true;
        }
    }
}
