using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Utility.Notifications;

namespace MuvluvMod.Services;

using MasterTranslationTables = Dictionary<string, Dictionary<string, Dictionary<string, string>>>;
using NameTranslationTables = Dictionary<string, Dictionary<string, string>>;

/// <summary>
/// Coordinates translation downloads, in-memory caching, and master-data translation.
/// </summary>
internal sealed class TranslationManager
{
    private readonly TranslationCache _translationCache;
    private readonly MasterDataTranslator _masterDataTranslator = new();
    private readonly ConcurrentDictionary<long, Dictionary<string, string>> _sceneTranslations =
        new();
    private readonly ConcurrentDictionary<long, Lazy<Task>> _pendingSceneLoads = new();
    private readonly object _sharedTranslationsLoadLock = new();

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

    internal TranslationManager(TranslationCache translationCache)
    {
        _translationCache = translationCache;
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

        try
        {
            await Task.WhenAll(namesTask, masterDataTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

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
            Toast.Warning("加载失败", "角色名称翻译加载失败");
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
            Toast.Warning("加载失败", "MasterData翻译加载失败");
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
            ((ICollection<KeyValuePair<long, Lazy<Task>>>)_pendingSceneLoads).Remove(
                new(sceneId, pendingLoad)
            );
        }
    }

    private async Task LoadSceneTranslationAsync(long sceneId)
    {
        Dictionary<string, string> translations;
        try
        {
            translations = await _translationCache
                .LoadSceneTranslationsAsync(sceneId)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (translations == null)
        {
            Logger.Warn($"Scenario translation load failed: {sceneId}");
            Toast.Warning("加载失败", $"剧本ID: {sceneId}");
            return;
        }

        _sceneTranslations[sceneId] = translations;
        Logger.Info($"Scenario translation loaded [{sceneId}]. Entries: {translations.Count}");
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
