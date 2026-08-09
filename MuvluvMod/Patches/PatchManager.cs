using System.Collections.Generic;
using HarmonyLib;

namespace MuvluvMod.Patches;

/// <summary>
/// Registers Harmony patches and owns shared scenario state.
/// </summary>
public static class PatchManager
{
    private static Harmony _harmony;

    public static long CurrentSceneId { get; private set; }
    public static bool IsPlayingScenario { get; private set; }

    public static void Initialize()
    {
        if (_harmony != null)
            return;

        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll(typeof(PatchManager).Assembly);
        Logger.Info("Harmony patches applied");
    }

    public static void Shutdown()
    {
        if (_harmony == null)
            return;

        _harmony.UnpatchSelf();
        _harmony = null;
        Logger.Info("Harmony patches removed");
    }

    public static void SetCurrentScene(long sceneId) => CurrentSceneId = sceneId;

    public static void SetScenarioPlaying(bool playing) => IsPlayingScenario = playing;

    public static bool TryGetCurrentSceneTranslation(out Dictionary<string, string> translation)
    {
        translation = null;
        return Config.TranslationEnabled.Value
            && Plugin.Translations != null
            && Plugin.Translations.TryGetSceneTranslation(CurrentSceneId, out translation);
    }
}
