using System;
using System.IO;

namespace MuvluvMod.Services;

/// <summary>
/// Builds remote and cached translation resource paths.
/// </summary>
internal static class TranslationPaths
{
    public const string Manifest = "manifest";
    public const string Names = "names";
    public const string Scenes = "scenes";
    public const string Static = "static";

    public static string BuildRelativePath(string type, string language, string id = null) =>
        type switch
        {
            Scenes when id == null => throw new ArgumentException(
                "Scene ID is required for scene translations",
                nameof(id)
            ),
            Scenes => $"{Scenes}/{id}/{language}.json",
            _ => $"{type}/{language}.json",
        };

    public static string BuildRemoteUrl(string cdn, string relativePath) =>
        $"{cdn.TrimEnd('/')}/translation/{relativePath}";

    public static string BuildCachePath(string cacheDirectory, string relativePath) =>
        Path.Combine(cacheDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
