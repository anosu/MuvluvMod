using System.Collections.Generic;
using HarmonyLib;

namespace MuvluvMod.Patches;

/// <summary>
/// Registers Harmony patches and owns shared scenario state.
/// </summary>
public static class PatchManager
{
    private static Harmony _harmony;

    public static long SceneId { get; private set; }
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

    public static void SetScene(long sceneId) => SceneId = sceneId;

    public static void SetScenarioPlaying(bool playing) => IsPlayingScenario = playing;

    public static bool TryGetCurrentScene(out Dictionary<string, string> translation)
    {
        translation = null;
        return Config.Translation.Value
            && Plugin.Trans != null
            && Plugin.Trans.TryGetScene(SceneId, out translation);
    }
}
