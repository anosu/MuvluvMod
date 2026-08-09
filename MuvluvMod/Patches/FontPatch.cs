using System;
using HarmonyLib;
using TMPro;

namespace MuvluvMod.Patches;

/// <summary>
/// Prevents the game's primary font from generating missing glyphs at render time.
/// </summary>
[HarmonyPatch(typeof(TMP_FontAsset), nameof(TMP_FontAsset.TryAddCharacterInternal))]
internal static class FontPatch
{
    private const string GameFontName = "FOT-RODINNTLGPRO-B SDF";

    [HarmonyPrefix]
    private static bool DisableDynamicGeneration(TMP_FontAsset __instance, ref bool __result)
    {
        if (
            __instance == null
            || !string.Equals(__instance.name, GameFontName, StringComparison.Ordinal)
            || !Config.TranslationEnabled.Value
        )
            return true;

        __instance.atlasPopulationMode = AtlasPopulationMode.Static;
        __result = false;
        return false;
    }
}
