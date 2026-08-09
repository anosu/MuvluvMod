using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MuvluvMod.Services;

using MasterTranslationTables = Dictionary<string, Dictionary<string, Dictionary<string, string>>>;
using NameTranslationTables = Dictionary<string, Dictionary<string, string>>;

/// <summary>
/// Loads translation resources through a manifest-verified local disk cache.
/// </summary>
internal sealed class TranslationCache
{
    private static readonly Encoding Utf8 = new UTF8Encoding(false);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    private readonly string _cdnBaseUrl;
    private readonly string _cacheRootDirectory;
    private readonly string _language;
    private readonly bool _preferLocalFiles;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _resourceLocks = new();
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
        _httpClient = httpClient;
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
        string cachedHash = ReadCachedManifestHash(cachePath);

        try
        {
            using var response = await _httpClient.GetAsync(downloadUrl).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var manifest = JsonSerializer.Deserialize<TranslationManifest>(json);
                if (manifest != null)
                {
                    _manifest = manifest;
                    if (
                        !string.IsNullOrEmpty(cachedHash)
                        && !string.Equals(
                            cachedHash,
                            manifest.ContentHash,
                            StringComparison.Ordinal
                        )
                    )
                        Logger.Info("Translation manifest has been updated");

                    SaveTextFile(cachePath, json);
                    Logger.Info($"Translation manifest loaded. Hash: {manifest.ContentHash}");
                    return;
                }

                Logger.Warn("Translation manifest response was empty");
            }
            else
            {
                Logger.Warn(
                    $"Translation manifest request failed: "
                        + $"{(int)response.StatusCode} {response.StatusCode}"
                );
            }
        }
        catch (TaskCanceledException)
        {
            Logger.Warn("Translation manifest request timed out");
        }
        catch (Exception e)
        {
            Logger.Error($"Translation manifest request failed: {e.Message}");
        }

        _manifest = ReadJsonFile<TranslationManifest>(
            cachePath,
            "Failed to load cached translation manifest"
        );
        if (_manifest != null)
            Logger.Info($"Cached translation manifest loaded. Hash: {_manifest.ContentHash}");
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
        var resourceLock = _resourceLocks.GetOrAdd(relativePath, CreateResourceLock);

        await resourceLock.WaitAsync().ConfigureAwait(false);
        try
        {
            T cachedData = ReadJsonFile<T>(cachePath, "Failed to load translation cache");
            if (_preferLocalFiles && cachedData != null)
            {
                Logger.Info($"Preferred local translation: {relativePath}");
                return cachedData;
            }

            if (expectedHash != null && cachedData != null)
            {
                string cachedHash = ComputeHashSafely(cachedData, computeHash);
                if (HashEquals(cachedHash, expectedHash))
                {
                    Logger.Info($"Translation cache hit: {relativePath}");
                    return cachedData;
                }

                Logger.Info($"Translation cache is outdated: {relativePath}");
            }

            if (_manifest != null && expectedHash == null)
            {
                if (cachedData != null)
                {
                    Logger.Info($"Using unlisted local translation: {relativePath}");
                    return cachedData;
                }

                Logger.Info($"Translation manifest has no entry for {relativePath}");
                return null;
            }

            T downloadedData = await DownloadResourceAsync(
                    downloadUrl,
                    cachePath,
                    relativePath,
                    expectedHash,
                    computeHash
                )
                .ConfigureAwait(false);
            if (downloadedData != null)
                return downloadedData;

            if (cachedData != null)
            {
                Logger.Warn($"Using stale translation cache: {relativePath}");
                return cachedData;
            }

            return null;
        }
        finally
        {
            resourceLock.Release();
        }
    }

    private async Task<T> DownloadResourceAsync<T>(
        string downloadUrl,
        string cachePath,
        string relativePath,
        string expectedHash,
        Func<T, string> computeHash
    )
        where T : class
    {
        Logger.Info($"Downloading translation: {relativePath}");
        T data = await DownloadJsonAsync<T>(downloadUrl).ConfigureAwait(false);
        if (data == null)
            return null;

        if (expectedHash != null)
        {
            string downloadedHash = ComputeHashSafely(data, computeHash);
            if (!HashEquals(downloadedHash, expectedHash))
            {
                Logger.Warn($"Downloaded translation hash mismatch: {relativePath}");
                return null;
            }
        }

        SaveJsonFile(cachePath, data);
        return data;
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

    private async Task<T> DownloadJsonAsync<T>(string url)
        where T : class
    {
        try
        {
            using var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<T>().ConfigureAwait(false);

            Logger.Warn($"GET {url} {(int)response.StatusCode} {response.StatusCode}");
        }
        catch (TaskCanceledException)
        {
            Logger.Warn($"GET timed out: {url}");
        }
        catch (Exception e)
        {
            Logger.Error($"GET failed [{url}]: {e.Message}");
        }

        return null;
    }

    private static string ReadCachedManifestHash(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            return JsonSerializer
                .Deserialize<TranslationManifest>(File.ReadAllText(path, Utf8))
                ?.ContentHash;
        }
        catch
        {
            return null;
        }
    }

    private static T ReadJsonFile<T>(string path, string errorPrefix)
        where T : class
    {
        if (!File.Exists(path))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path, Utf8));
        }
        catch (Exception e)
        {
            Logger.Error($"{errorPrefix} [{path}]: {e.Message}");
            return null;
        }
    }

    private static string ComputeHashSafely<T>(T data, Func<T, string> computeHash)
        where T : class
    {
        if (data == null)
            return null;

        try
        {
            return computeHash(data);
        }
        catch (Exception e)
        {
            Logger.Warn($"Failed to hash translation data: {e.Message}");
            return null;
        }
    }

    private static bool HashEquals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

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

    private static void SaveJsonFile<T>(string path, T data)
        where T : class => SaveTextFile(path, JsonSerializer.Serialize(data, JsonOptions));

    private static void SaveTextFile(string path, string content)
    {
        string tempPath = path + ".tmp";
        try
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(tempPath, content, Utf8);
            File.Move(tempPath, path, overwrite: true);
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to write translation cache [{path}]: {e.Message}");
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch { }
        }
    }

    private static SemaphoreSlim CreateResourceLock(string relativePath) => new(1, 1);
}
