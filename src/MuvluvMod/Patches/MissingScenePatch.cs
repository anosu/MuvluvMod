using System;
using Assets.Api.Client.ConnectionManager;
using HarmonyLib;

namespace MuvluvMod.Patches;

/// <summary>
/// Detects scene asset requests and forwards their signed URLs to the debug reporter.
/// </summary>
[HarmonyPatch]
public static class MissingScenePatch
{
    private const string ScenePathMarker = "/master-data/scenes/";
    private const string SceneFileExtension = ".bin";

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(IzanamiNetworkUtilizationManager),
        nameof(IzanamiNetworkUtilizationManager.SendRequest)
    )]
    public static void CaptureSceneRequest(
        IzanamiNetworkUtilizationManager.RequestContext requestContext
    )
    {
        try
        {
            string url = requestContext?.Url;
            if (Plugin.MissingSceneReporter == null || !TryGetSceneId(url, out var sceneId))
                return;

            Plugin.MissingSceneReporter.SubmitIfMissing(sceneId, url);
        }
        catch (Exception e)
        {
            Logger.Warn($"Scene request capture failed: {e.Message}");
        }
    }

    private static bool TryGetSceneId(string url, out long sceneId)
    {
        sceneId = 0;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        string path = uri.AbsolutePath;
        int markerIndex = path.LastIndexOf(ScenePathMarker, StringComparison.OrdinalIgnoreCase);
        if (
            markerIndex < 0
            || !path.EndsWith(SceneFileExtension, StringComparison.OrdinalIgnoreCase)
        )
            return false;

        int idStart = markerIndex + ScenePathMarker.Length;
        int idLength = path.Length - idStart - SceneFileExtension.Length;
        return idLength > 0 && long.TryParse(path.Substring(idStart, idLength), out sceneId);
    }
}
