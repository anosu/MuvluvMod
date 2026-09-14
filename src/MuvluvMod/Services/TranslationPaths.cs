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
    public const string MasterData = "static";

    public static string BuildRelativePath(
        string category,
        string language,
        string resourceId = null
    ) =>
        category switch
        {
            Scenes when resourceId == null => throw new ArgumentException(
                "Scene ID is required for scene translations",
                nameof(resourceId)
            ),
            Scenes => $"{Scenes}/{resourceId}/{language}.json",
            _ => $"{category}/{language}.json",
        };

    public static string BuildDownloadUrl(string cdnBaseUrl, string relativePath) =>
        $"{cdnBaseUrl.TrimEnd('/')}/translation/{relativePath}";

    public static string BuildLocalPath(string cacheRootDirectory, string relativePath) =>
        Path.Combine(cacheRootDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
