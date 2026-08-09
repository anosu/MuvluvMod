using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using TMPro;
using Utility.Fonts;
using Utility.Toast;

namespace MuvluvMod.Services;

using MasterTranslationTables = Dictionary<string, Dictionary<string, Dictionary<string, string>>>;
using NameTranslationTables = Dictionary<string, Dictionary<string, string>>;

/// <summary>
/// Coordinates translation downloads, in-memory caching, and font loading.
/// </summary>
public sealed class TranslationManager
{
    private readonly TranslationCache _translationCache;
    private readonly FontHelper _fallbackFont;
    private readonly MasterDataTranslator _masterDataTranslator = new();
    private readonly ConcurrentDictionary<long, Dictionary<string, string>> _sceneTranslations =
        new();
    private readonly ConcurrentDictionary<long, Lazy<Task>> _pendingSceneLoads = new();
    private readonly object _sharedTranslationsLoadLock = new();

    private int _fontLoadStarted;
    private Task _sharedTranslationsLoadTask;
    private volatile bool _sharedTranslationsLoaded;

    public IReadOnlyDictionary<string, string> SpeakerNames { get; private set; } =
        new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> TeamNames { get; private set; } =
        new Dictionary<string, string>();
    public IReadOnlyDictionary<
        string,
        Dictionary<string, Dictionary<string, string>>
    > MasterDataTranslations { get; private set; } = new MasterTranslationTables();

    internal TranslationManager(TranslationCache translationCache, FontHelper fallbackFont)
    {
        _translationCache = translationCache;
        _fallbackFont = fallbackFont;
    }

    public void Initialize()
    {
        if (!Config.TranslationEnabled.Value)
            return;

        _ = EnsureSharedTranslationsLoadedAsync();
        StartFallbackFontLoad();
    }

    public bool TryGetSceneTranslation(long sceneId, out Dictionary<string, string> translation) =>
        _sceneTranslations.TryGetValue(sceneId, out translation);

    public MasterDataTranslationResult TranslateMasterData(IEnumerable objects) =>
        _masterDataTranslator.Translate(objects, MasterDataTranslations);

    public Task EnsureSceneTranslationsLoadedAsync(long sceneId)
    {
        var sharedTranslationsTask = EnsureSharedTranslationsLoadedAsync();
        return _sceneTranslations.ContainsKey(sceneId)
            ? sharedTranslationsTask
            : Task.WhenAll(sharedTranslationsTask, EnsureSceneTranslationLoadedAsync(sceneId));
    }

    public Task EnsureSharedTranslationsLoadedAsync()
    {
        if (!Config.TranslationEnabled.Value)
            return Task.CompletedTask;

        lock (_sharedTranslationsLoadLock)
        {
            if (
                _sharedTranslationsLoadTask == null
                || (_sharedTranslationsLoadTask.IsCompleted && !_sharedTranslationsLoaded)
            )
                _sharedTranslationsLoadTask = LoadSharedTranslationsAsync();

            return _sharedTranslationsLoadTask;
        }
    }

    private async Task LoadSharedTranslationsAsync()
    {
        var namesTask = _translationCache.LoadNameTranslationsAsync();
        var masterDataTask = _translationCache.LoadMasterDataTranslationsAsync();

        await Task.WhenAll(namesTask, masterDataTask).ConfigureAwait(false);

        bool namesLoaded = ApplyNameTranslations(await namesTask.ConfigureAwait(false));
        bool masterDataLoaded = ApplyMasterDataTranslations(
            await masterDataTask.ConfigureAwait(false)
        );
        _sharedTranslationsLoaded = namesLoaded && masterDataLoaded;
    }

    private bool ApplyNameTranslations(NameTranslationTables tables)
    {
        if (tables == null || tables.Count == 0)
        {
            Logger.Warn("Names translation load failed");
            Toast.Warn("加载失败", "角色名称翻译加载失败");
            return false;
        }

        SpeakerNames = GetNameTable(tables, "speakerNames");
        TeamNames = GetNameTable(tables, "teamNames");
        Logger.Info($"Character names translation loaded. Total: {SpeakerNames.Count}");
        Logger.Info($"Team names translation loaded. Total: {TeamNames.Count}");
        return true;
    }

    private bool ApplyMasterDataTranslations(MasterTranslationTables tables)
    {
        if (tables == null || tables.Count == 0)
        {
            Logger.Warn("MasterData translation load failed");
            Toast.Warn("加载失败", "MasterData翻译加载失败");
            return false;
        }

        var filtered = FilterMasterDataTranslations(tables);
        MasterDataTranslations = filtered.Tables;
        Logger.Info(
            $"MasterData translation loaded. Types: {filtered.Tables.Count}, "
                + $"Entries: {filtered.EntryCount}, "
                + $"Skipped identity entries: {filtered.SkippedIdentityCount}, "
                + $"Skipped empty entries: {filtered.SkippedEmptyCount}"
        );
        return true;
    }

    private async Task EnsureSceneTranslationLoadedAsync(long sceneId)
    {
        if (_sceneTranslations.ContainsKey(sceneId))
            return;

        var pendingLoad = _pendingSceneLoads.GetOrAdd(
            sceneId,
            id => new Lazy<Task>(() => LoadSceneTranslationAsync(id))
        );

        try
        {
            await pendingLoad.Value.ConfigureAwait(false);
        }
        finally
        {
            _pendingSceneLoads.TryRemove(sceneId, out _);
        }
    }

    private async Task LoadSceneTranslationAsync(long sceneId)
    {
        var translations = await _translationCache
            .LoadSceneTranslationsAsync(sceneId)
            .ConfigureAwait(false);

        if (translations == null)
        {
            Logger.Warn($"Scenario translation load failed: {sceneId}");
            Toast.Warn("加载失败", $"剧本ID: {sceneId}");
            return;
        }

        _sceneTranslations[sceneId] = translations;
        Logger.Info($"Scenario translation loaded [{sceneId}]. Entries: {translations.Count}");
    }

    private void StartFallbackFontLoad()
    {
        if (Plugin.Instance == null || Interlocked.Exchange(ref _fontLoadStarted, 1) != 0)
            return;

        Plugin.Instance.StartCoroutine(LoadFallbackFontCoroutine().WrapToIl2Cpp());
    }

    private IEnumerator LoadFallbackFontCoroutine()
    {
        var loader = _fallbackFont.LoadAsync();
        while (true)
        {
            object current;
            try
            {
                if (!loader.MoveNext())
                    break;
                current = loader.Current;
            }
            catch (Exception e)
            {
                Logger.Error($"Font load failed: {e.Message}");
                Toast.Error("字体加载失败", e.Message);
                yield break;
            }

            yield return current;
        }

        if (!_fallbackFont.Valid)
        {
            Logger.Error("Font load failed: loaded asset is invalid");
            Toast.Error("字体加载失败", "字体资源无效");
            yield break;
        }

        if (!TMP_Settings.fallbackFontAssets.Contains(_fallbackFont.Asset))
            TMP_Settings.fallbackFontAssets.Add(_fallbackFont.Asset);

        Logger.Info($"Fallback font registered: {_fallbackFont.Asset.name}");
    }

    private static IReadOnlyDictionary<string, string> GetNameTable(
        NameTranslationTables tables,
        string name
    ) =>
        tables.TryGetValue(name, out var table) && table != null
            ? table
            : new Dictionary<string, string>();

    private static (
        MasterTranslationTables Tables,
        int EntryCount,
        int SkippedIdentityCount,
        int SkippedEmptyCount
    ) FilterMasterDataTranslations(MasterTranslationTables source)
    {
        var filteredTables = new MasterTranslationTables(source.Count);
        int entryCount = 0;
        int skippedIdentityCount = 0;
        int skippedEmptyCount = 0;

        foreach (var (typeName, propertyTables) in source)
        {
            if (propertyTables == null)
                continue;

            var filteredProperties = new Dictionary<string, Dictionary<string, string>>(
                propertyTables.Count
            );
            foreach (var (path, translations) in propertyTables)
            {
                if (translations == null)
                    continue;

                var filteredTranslations = new Dictionary<string, string>(translations.Count);
                foreach (var (original, translated) in translations)
                {
                    if (string.IsNullOrEmpty(translated))
                    {
                        skippedEmptyCount++;
                        continue;
                    }

                    if (string.Equals(original, translated, StringComparison.Ordinal))
                    {
                        skippedIdentityCount++;
                        continue;
                    }

                    filteredTranslations[original] = translated;
                    entryCount++;
                }

                if (filteredTranslations.Count > 0)
                    filteredProperties[path] = filteredTranslations;
            }

            if (filteredProperties.Count > 0)
                filteredTables[typeName] = filteredProperties;
        }

        return (filteredTables, entryCount, skippedIdentityCount, skippedEmptyCount);
    }
}
