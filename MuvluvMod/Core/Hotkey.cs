using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MuvluvMod;

/// <summary>
/// Handles runtime configuration hotkeys.
/// </summary>
public sealed class Hotkey : MonoBehaviour
{
    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        CheckToggle(keyboard, Key.F3, Config.EnableSkipButton);
        CheckToggle(keyboard, Key.F4, Config.VoiceInterruption);
        CheckToggle(keyboard, Key.F5, Config.AutoSkipBattle);
    }

    private static void CheckToggle(Keyboard keyboard, Key key, ConfigEntry<bool> entry)
    {
        if (keyboard[key].wasPressedThisFrame)
            Toggle(entry);
    }

    private static void Toggle(ConfigEntry<bool> entry) => entry.Value = !entry.Value;
}
