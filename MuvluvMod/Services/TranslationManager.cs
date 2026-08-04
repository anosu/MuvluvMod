using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
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
    private const string Language = "zh_Hans";

    private readonly HttpClient _client;
    private readonly FontHelper _font;
    private readonly MasterDataTranslator _masterDataTranslator = new();
    private readonly ConcurrentDictionary<long, Dictionary<string, string>> _scenes = new();
    private readonly ConcurrentDictionary<long, Lazy<Task>> _sceneLoads = new();
    private readonly Lazy<Task> _staticLoad;

    private int _fontLoadStarted;

    public IReadOnlyDictionary<string, string> Names { get; private set; } =
        new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> TeamNames { get; private set; } =
        new Dictionary<string, string>();
    public IReadOnlyDictionary<
        string,
        Dictionary<string, Dictionary<string, string>>
    > MasterTranslations { get; private set; } = new MasterTranslationTables();

    public TranslationManager(HttpClient client, FontHelper font)
    {
        _client = client;
        _font = font;
        _staticLoad = new Lazy<Task>(LoadStaticTranslationsAsync);
    }

    public void Enable()
    {
        if (!Config.Translation.Value)
            return;

        _ = EnsureStaticTranslationsLoadedAsync();
        EnsureFallbackFontLoaded();
    }

    public bool TryGetScene(long sceneId, out Dictionary<string, string> translation) =>
        _scenes.TryGetValue(sceneId, out translation);

    public MasterDataTranslationResult TranslateMasterData(
        System.Collections.IEnumerable objects
    ) => _masterDataTranslator.Translate(objects, MasterTranslations);

    public Task EnsureSceneReadyAsync(long sceneId)
    {
        var staticTask = EnsureStaticTranslationsLoadedAsync();
        return _scenes.ContainsKey(sceneId)
            ? staticTask
            : Task.WhenAll(staticTask, EnsureSceneTranslationLoadedAsync(sceneId));
    }

    public Task EnsureStaticTranslationsLoadedAsync()
    {
        if (!Config.Translation.Value)
            return Task.CompletedTask;

        return _staticLoad.Value;
    }

    private async Task LoadStaticTranslationsAsync()
    {
        string cdn = GetCdn();
        var namesTask = GetAsync<NameTranslationTables>($"{cdn}/translation/names/{Language}.json");
        var masterTask = GetAsync<MasterTranslationTables>(
            $"{cdn}/translation/static/{Language}.json"
        );

        await Task.WhenAll(namesTask, masterTask).ConfigureAwait(false);

        var names = await namesTask.ConfigureAwait(false);
        if (names != null)
        {
            Names = GetTable(names, "speakerNames");
            TeamNames = GetTable(names, "teamNames");
            Logger.Info($"Character names translation loaded. Total: {Names.Count}");
            Logger.Info($"Team names translation loaded. Total: {TeamNames.Count}");
        }
        else
        {
            Logger.Warn("Names translation load failed");
            Toast.Warn("加载失败", "角色名称翻译加载失败");
        }

        var master = await masterTask.ConfigureAwait(false);
        if (master != null)
        {
            MasterTranslations = master;
            Logger.Info($"MasterData translation loaded. Types: {master.Count}");
        }
        else
        {
            Logger.Warn("MasterData translation load failed");
            Toast.Warn("加载失败", "MasterData翻译加载失败");
        }
    }

    private async Task EnsureSceneTranslationLoadedAsync(long sceneId)
    {
        if (_scenes.ContainsKey(sceneId))
            return;

        var lazy = _sceneLoads.GetOrAdd(
            sceneId,
            id => new Lazy<Task>(() => LoadSceneTranslationAsync(id))
        );

        try
        {
            await lazy.Value.ConfigureAwait(false);
        }
        finally
        {
            _sceneLoads.TryRemove(sceneId, out _);
        }
    }

    private async Task LoadSceneTranslationAsync(long sceneId)
    {
        var translations = await GetAsync<Dictionary<string, string>>(
                $"{GetCdn()}/translation/scenes/{sceneId}/{Language}.json"
            )
            .ConfigureAwait(false);

        if (translations == null)
        {
            Logger.Warn($"Scenario translation load failed: {sceneId}");
            Toast.Warn("加载失败", $"剧本ID: {sceneId}");
            return;
        }

        _scenes[sceneId] = translations;
        Logger.Info($"Scenario translation loaded [{sceneId}]. Entries: {translations.Count}");
    }

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

    private void EnsureFallbackFontLoaded()
    {
        if (Plugin.Instance == null || Interlocked.Exchange(ref _fontLoadStarted, 1) != 0)
            return;

        Plugin.Instance.StartCoroutine(LoadFallbackFont().WrapToIl2Cpp());
    }

    private IEnumerator LoadFallbackFont()
    {
        var loader = _font.LoadAsync();
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

        if (!_font.Valid)
        {
            Logger.Error("Font load failed: loaded asset is invalid");
            Toast.Error("字体加载失败", "字体资源无效");
            yield break;
        }

        if (!TMP_Settings.fallbackFontAssets.Contains(_font.Asset))
            TMP_Settings.fallbackFontAssets.Add(_font.Asset);

        Logger.Info($"Fallback font registered: {_font.Asset.name}");
    }

    private static IReadOnlyDictionary<string, string> GetTable(
        NameTranslationTables tables,
        string name
    ) =>
        tables.TryGetValue(name, out var table) && table != null
            ? table
            : new Dictionary<string, string>();

    private static string GetCdn() => Config.TranslationCDN.Value.TrimEnd('/');
}
