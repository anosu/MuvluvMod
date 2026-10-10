using HarmonyLib;

namespace MuvluvMod.Patches;

/// <summary>
/// Registers Harmony patches and prevents unloading during wrapped game loads.
/// </summary>
public static class PatchManager
{
    private static Harmony _harmony;

    public static void Initialize()
    {
        if (_harmony != null)
            return;

        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll(typeof(PatchManager).Assembly);
        Logger.Info("Harmony patches applied");
    }

    public static bool Shutdown()
    {
        if (TranslationPatch.HasPendingLoads)
            return false;
        if (_harmony == null)
            return true;

        _harmony.UnpatchSelf();
        _harmony = null;
        TranslationPatch.Reset();
        EnhancePatch.Reset();
        Logger.Info("Harmony patches removed");
        return true;
    }
}
