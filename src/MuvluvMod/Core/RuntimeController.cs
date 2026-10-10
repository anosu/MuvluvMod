using System;
using System.Threading;
using BepInEx.Configuration;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MuvluvMod;

public sealed class RuntimeController : MonoBehaviour
{
    private int _loadTranslation;

    private void OnEnable() => Config.TranslationEnabled.SettingChanged += OnTranslationChanged;

    private void OnDisable()
    {
        Config.TranslationEnabled.SettingChanged -= OnTranslationChanged;
        Interlocked.Exchange(ref _loadTranslation, 0);
    }

    [HideFromIl2Cpp]
    private void OnTranslationChanged(object sender, EventArgs e) =>
        Interlocked.Exchange(ref _loadTranslation, 1);

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            Toggle(keyboard, Key.F3, Config.EnableSkipButton);
            Toggle(keyboard, Key.F4, Config.VoiceInterruption);
            Toggle(keyboard, Key.F5, Config.AutoSkipBattle);
        }
        if (Interlocked.Exchange(ref _loadTranslation, 0) != 0)
            Plugin.LoadTranslationResources();
    }

    [HideFromIl2Cpp]
    private static void Toggle(Keyboard keyboard, Key key, ConfigEntry<bool> entry)
    {
        if (keyboard[key].wasPressedThisFrame)
            entry.Value = !entry.Value;
    }
}
