using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
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
    private static readonly byte[] EntrySeparator = { 0 };
    private static readonly Encoding Utf8 = new UTF8Encoding(false);
    private static readonly IComparer<string> KeyComparer = new UnicodeCodePointComparer();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    private readonly string _cdn;
    private readonly string _cacheDirectory;
    private readonly string _language;
    private readonly bool _preferLocalFiles;
    private readonly HttpClient _client;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly object _manifestLock = new();

    private Task _manifestTask;
    private TranslationManifest _manifest;

    public TranslationCache(
        string cdn,
        string cacheDirectory,
        string language,
        bool preferLocalFiles,
        HttpClient client
    )
    {
        _cdn = cdn.TrimEnd('/');
        _cacheDirectory = cacheDirectory;
        _language = language;
        _preferLocalFiles = preferLocalFiles;
        _client = client;
    }

    public Task<NameTranslationTables> LoadNamesAsync() =>
        LoadWithCacheAsync<NameTranslationTables>(TranslationPaths.Names, null, GetNestedHash);

    public Task<MasterTranslationTables> LoadStaticAsync() =>
        LoadWithCacheAsync<MasterTranslationTables>(TranslationPaths.Static, null, GetBundleHash);

    public Task<Dictionary<string, string>> LoadSceneAsync(long sceneId) =>
        LoadWithCacheAsync<Dictionary<string, string>>(
            TranslationPaths.Scenes,
            sceneId.ToString(),
            GetFlatHash
        );

    private Task EnsureManifestLoadedAsync()
    {
        lock (_manifestLock)
            return _manifestTask ??= FetchManifestAsync();
    }

    private async Task FetchManifestAsync()
    {
        string relativePath = TranslationPaths.BuildRelativePath(
            TranslationPaths.Manifest,
            _language
        );
        string remoteUrl = TranslationPaths.BuildRemoteUrl(_cdn, relativePath);
        string cachePath = TranslationPaths.BuildCachePath(_cacheDirectory, relativePath);
        string cachedHash = TryReadManifestHash(cachePath);

        try
        {
            using var response = await _client.GetAsync(remoteUrl).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var manifest = JsonSerializer.Deserialize<TranslationManifest>(json);
                if (manifest != null)
                {
                    _manifest = manifest;
                    if (
                        !string.IsNullOrEmpty(cachedHash)
                        && !string.Equals(cachedHash, manifest.Hash, StringComparison.Ordinal)
                    )
                        Logger.Info("Translation manifest has been updated");

                    TryWriteTextFile(cachePath, json);
                    Logger.Info($"Translation manifest loaded. Hash: {manifest.Hash}");
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

        _manifest = LoadJsonFile<TranslationManifest>(
            cachePath,
            "Failed to load cached translation manifest"
        );
        if (_manifest != null)
            Logger.Info($"Cached translation manifest loaded. Hash: {_manifest.Hash}");
    }

    private async Task<T> LoadWithCacheAsync<T>(string type, string id, Func<T, string> computeHash)
        where T : class
    {
        await EnsureManifestLoadedAsync().ConfigureAwait(false);

        string relativePath = TranslationPaths.BuildRelativePath(type, _language, id);
        string remoteUrl = TranslationPaths.BuildRemoteUrl(_cdn, relativePath);
        string cachePath = TranslationPaths.BuildCachePath(_cacheDirectory, relativePath);
        string expectedHash = GetManifestHash(type, id);
        var semaphore = _locks.GetOrAdd(relativePath, CreateSemaphore);

        await semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            bool localExists = File.Exists(cachePath);
            T localData = null;

            if (_preferLocalFiles && localExists)
            {
                localData = LoadJsonFile<T>(
                    cachePath,
                    "Failed to load preferred local translation"
                );
                if (localData != null)
                {
                    Logger.Info($"Preferred local translation: {relativePath}");
                    return localData;
                }
            }

            if (expectedHash != null && localExists)
            {
                localData ??= LoadJsonFile<T>(cachePath, "Failed to load translation cache");
                string localHash = TryComputeHash(localData, computeHash);
                if (HashesEqual(localHash, expectedHash))
                {
                    Logger.Info($"Translation cache hit: {relativePath}");
                    return localData;
                }

                Logger.Info($"Translation cache is outdated: {relativePath}");
            }

            if (_manifest != null && expectedHash == null)
            {
                if (localExists)
                {
                    Logger.Info($"Using unlisted local translation: {relativePath}");
                    return localData
                        ?? LoadJsonFile<T>(cachePath, "Failed to load unlisted translation");
                }

                Logger.Info($"Translation manifest has no entry for {relativePath}");
                return null;
            }

            Logger.Info($"Downloading translation: {relativePath}");
            T remoteData = await GetAsync<T>(remoteUrl).ConfigureAwait(false);
            if (remoteData != null)
            {
                if (
                    expectedHash == null
                    || HashesEqual(TryComputeHash(remoteData, computeHash), expectedHash)
                )
                {
                    TrySaveJsonFile(cachePath, remoteData);
                    return remoteData;
                }

                Logger.Warn($"Downloaded translation hash mismatch: {relativePath}");
            }

            if (localExists)
            {
                Logger.Warn($"Using stale translation cache: {relativePath}");
                return localData
                    ?? LoadJsonFile<T>(cachePath, "Failed to load stale translation cache");
            }

            return null;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private string GetManifestHash(string type, string id) =>
        type switch
        {
            TranslationPaths.Names => _manifest?.Names,
            TranslationPaths.Static => _manifest?.Static,
            TranslationPaths.Scenes when id != null => _manifest?.Scenes?.TryGetValue(
                id,
                out var hash
            ) == true
                ? hash
                : null,
            _ => null,
        };

    private async Task<T> GetAsync<T>(string url)
        where T : class
    {
        try
        {
            using var response = await _client.GetAsync(url).ConfigureAwait(false);
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

    private static string TryReadManifestHash(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            return JsonSerializer
                .Deserialize<TranslationManifest>(File.ReadAllText(path, Utf8))
                ?.Hash;
        }
        catch
        {
            return null;
        }
    }

    private static T LoadJsonFile<T>(string path, string errorPrefix)
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

    private static string TryComputeHash<T>(T data, Func<T, string> computeHash)
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

    private static bool HashesEqual(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static void TrySaveJsonFile<T>(string path, T data)
        where T : class => TryWriteTextFile(path, JsonSerializer.Serialize(data, JsonOptions));

    private static void TryWriteTextFile(string path, string content)
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

    private static string GetFlatHash(Dictionary<string, string> translations) =>
        ComputeMd5Hex(
            translations
                .Keys.OrderBy(key => key, KeyComparer)
                .Select(key => ((string Key, string Value))(key, translations[key]))
        );

    private static SemaphoreSlim CreateSemaphore(string key) => new(1, 1);

    private static string GetNestedHash(NameTranslationTables tables) =>
        ComputeMd5Hex(EnumerateNestedEntries(tables));

    private static string GetBundleHash(MasterTranslationTables tables) =>
        ComputeMd5Hex(EnumerateBundleEntries(tables));

    private static IEnumerable<(string Key, string Value)> EnumerateNestedEntries(
        NameTranslationTables tables
    )
    {
        foreach (string tableName in tables.Keys.OrderBy(key => key, KeyComparer))
        {
            var translations = tables[tableName];
            if (translations == null)
                continue;

            foreach (string source in translations.Keys.OrderBy(key => key, KeyComparer))
                yield return ($"{tableName}\x01{source}", translations[source]);
        }
    }

    private static IEnumerable<(string Key, string Value)> EnumerateBundleEntries(
        MasterTranslationTables tables
    )
    {
        foreach (string typeName in tables.Keys.OrderBy(key => key, KeyComparer))
        {
            var properties = tables[typeName];
            if (properties == null)
                continue;

            foreach (string propertyName in properties.Keys.OrderBy(key => key, KeyComparer))
            {
                var translations = properties[propertyName];
                if (translations == null)
                    continue;

                foreach (string source in translations.Keys.OrderBy(key => key, KeyComparer))
                    yield return (
                        $"{typeName}\x01{propertyName}\x01{source}",
                        translations[source]
                    );
            }
        }
    }

    private static string ComputeMd5Hex(IEnumerable<(string Key, string Value)> entries)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        foreach (var (key, value) in entries)
        {
            AppendUtf8(hash, key);
            hash.AppendData(EntrySeparator);
            AppendUtf8(hash, value);
            hash.AppendData(EntrySeparator);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendUtf8(IncrementalHash hash, string value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        int byteCount = Utf8.GetByteCount(value);
        byte[] rented = null;
        Span<byte> buffer =
            byteCount <= 512
                ? stackalloc byte[byteCount]
                : (rented = ArrayPool<byte>.Shared.Rent(byteCount));
        try
        {
            int written = Utf8.GetBytes(value.AsSpan(), buffer);
            hash.AppendData(buffer[..written]);
        }
        finally
        {
            if (rented != null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private sealed class UnicodeCodePointComparer : IComparer<string>
    {
        public int Compare(string left, string right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            int leftIndex = 0;
            int rightIndex = 0;
            while (leftIndex < left.Length && rightIndex < right.Length)
            {
                int leftCodePoint = char.ConvertToUtf32(left, leftIndex);
                int rightCodePoint = char.ConvertToUtf32(right, rightIndex);
                int difference = leftCodePoint.CompareTo(rightCodePoint);
                if (difference != 0)
                    return difference;

                leftIndex += char.IsHighSurrogate(left[leftIndex]) ? 2 : 1;
                rightIndex += char.IsHighSurrogate(right[rightIndex]) ? 2 : 1;
            }

            return (left.Length - leftIndex).CompareTo(right.Length - rightIndex);
        }
    }
}
