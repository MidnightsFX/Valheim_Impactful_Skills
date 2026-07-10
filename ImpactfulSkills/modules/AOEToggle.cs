using HarmonyLib;
using ImpactfulSkills.modules.Multiplant;
using Jotunn.Configs;
using Jotunn.Managers;

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
    /// Registers the "Toggle AOE" key hint with Jötunn's <see cref="KeyHintManager"/> so it is
    /// rendered in the build HUD whenever the Cultivator is equipped, replacing the previous
    /// hand-rolled clone of a vanilla build-hint row.
    /// <para>
    /// Note: a Jötunn key hint fully replaces the vanilla build hints for its item, so while the
    /// Cultivator is equipped only this "Toggle AOE" hint is shown (the vanilla Place/Remove/Rotate
    /// hints are hidden).
    /// </para>
    /// </summary>
    public static class AOEToggleKeyHint {
        // Base button name; Jötunn appends "!<PluginGUID>" when the button is registered.
        private const string ButtonName = "AOEToggle";

        public static void Setup() {
            // The hint's key text is resolved from ZInput via the button's (rebindable) shortcut
            // config, so registering the button is what lets the hint show the correct key.
            ButtonConfig toggleButton = new ButtonConfig {
                Name = ButtonName,
                ShortcutConfig = ValConfig.AOEToggleHotkey,
                HintToken = "$aoe_toggle_hint",
            };
            InputManager.Instance.AddButton(ImpactfulSkills.PluginGUID, toggleButton);

            KeyHintManager.Instance.AddKeyHint(new KeyHintConfig {
                Item = "Cultivator",
                ButtonConfigs = new[] { toggleButton },
            });
        }
    }
}
