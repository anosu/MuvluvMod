using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Utility.Caching;

namespace MuvluvMod.Services;

using MasterTranslationTables = Dictionary<string, Dictionary<string, Dictionary<string, string>>>;
using NameTranslationTables = Dictionary<string, Dictionary<string, string>>;

/// <summary>
/// Loads translation resources through a manifest-verified local disk cache.
/// </summary>
internal sealed class TranslationCache
{
    private readonly string _cdnBaseUrl;
    private readonly string _cacheRootDirectory;
    private readonly string _language;
    private readonly bool _preferLocalFiles;
    private readonly JsonResourceCache _resources;
    private readonly object _manifestLoadLock = new();

    private Task _manifestLoadTask;
    private TranslationManifest _manifest;

    public TranslationCache(
        string cdnBaseUrl,
        string cacheRootDirectory,
        string language,
        bool preferLocalFiles,
        HttpClient httpClient
    )
    {
        _cdnBaseUrl = cdnBaseUrl.TrimEnd('/');
        _cacheRootDirectory = cacheRootDirectory;
        _language = ValidateLanguage(language);
        _preferLocalFiles = preferLocalFiles;
        // Explicit callback parameter types avoid nullable metadata from the Unity proxy's incomplete attribute.
        _resources = new JsonResourceCache(
            httpClient,
            (string message) => Logger.Info(message),
            (string message) => Logger.Warn(message)
        );
    }

    public Task<NameTranslationTables> LoadNameTranslationsAsync() =>
        LoadResourceAsync<NameTranslationTables>(
            TranslationPaths.Names,
            null,
            TranslationHash.ComputeNames
        );

    public Task<MasterTranslationTables> LoadMasterDataTranslationsAsync() =>
        LoadResourceAsync<MasterTranslationTables>(
            TranslationPaths.MasterData,
            null,
            TranslationHash.ComputeMasterData
        );

    public Task<Dictionary<string, string>> LoadSceneTranslationsAsync(long sceneId) =>
        LoadResourceAsync<Dictionary<string, string>>(
            TranslationPaths.Scenes,
            sceneId.ToString(CultureInfo.InvariantCulture),
            TranslationHash.ComputeScene
        );

    private Task EnsureManifestLoadedAsync()
    {
        lock (_manifestLoadLock)
            return _manifestLoadTask ??= LoadManifestAsync();
    }

    private async Task LoadManifestAsync()
    {
        string relativePath = TranslationPaths.BuildRelativePath(
            TranslationPaths.Manifest,
            _language
        );
        string downloadUrl = TranslationPaths.BuildDownloadUrl(_cdnBaseUrl, relativePath);
        string cachePath = TranslationPaths.BuildLocalPath(_cacheRootDirectory, relativePath);
        var previous = await _resources
            .LoadLocalAsync<TranslationManifest>(cachePath)
            .ConfigureAwait(false);
        _manifest = await _resources
            .RefreshAsync<TranslationManifest>(relativePath, cachePath, downloadUrl)
            .ConfigureAwait(false);
        if (_manifest != null)
        {
            if (
                !string.IsNullOrEmpty(previous?.ContentHash)
                && previous.ContentHash != _manifest.ContentHash
            )
                Logger.Info("Translation manifest has been updated");
            Logger.Info($"Translation manifest loaded. Hash: {_manifest.ContentHash}");
        }
    }

    private async Task<T> LoadResourceAsync<T>(
        string category,
        string resourceId,
        Func<T, string> computeHash
    )
        where T : class
    {
        await EnsureManifestLoadedAsync().ConfigureAwait(false);

        string relativePath = TranslationPaths.BuildRelativePath(category, _language, resourceId);
        string downloadUrl = TranslationPaths.BuildDownloadUrl(_cdnBaseUrl, relativePath);
        string cachePath = TranslationPaths.BuildLocalPath(_cacheRootDirectory, relativePath);
        string expectedHash = GetManifestHash(category, resourceId);
        var policy =
            _manifest != null && expectedHash == null ? JsonCachePolicy.LocalOnly
            : _preferLocalFiles ? JsonCachePolicy.PreferLocal
            : JsonCachePolicy.Refresh;
        return await _resources
            .RefreshAsync<T>(
                relativePath,
                cachePath,
                downloadUrl,
                policy,
                expectedHash == null
                    ? null
                    : (T value) =>
                        string.Equals(
                            computeHash(value),
                            expectedHash,
                            StringComparison.OrdinalIgnoreCase
                        )
            )
            .ConfigureAwait(false);
    }

    private string GetManifestHash(string category, string resourceId) =>
        category switch
        {
            TranslationPaths.Names => _manifest?.NamesHash,
            TranslationPaths.MasterData => _manifest?.MasterDataHash,
            TranslationPaths.Scenes when resourceId != null => _manifest?.SceneHashes?.TryGetValue(
                resourceId,
                out var hash
            ) == true
                ? hash
                : null,
            _ => null,
        };

    private static string ValidateLanguage(string language)
    {
        if (
            string.IsNullOrWhiteSpace(language)
            || language.Contains('/')
            || language.Contains('\\')
        )
            throw new ArgumentException(
                "Translation language cannot be empty or contain path separators",
                nameof(language)
            );

        return language;
    }
}
