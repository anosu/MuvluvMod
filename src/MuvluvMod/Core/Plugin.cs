using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using MuvluvMod.Patches;
using MuvluvMod.Services;
using TMPro;
using UnityEngine;
using Utility.Assets;
using Utility.Notifications;

namespace MuvluvMod;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public sealed class Plugin : BasePlugin
{
    private const int HttpTimeoutSeconds = 10;
    private const int PooledConnectionLifetimeMinutes = 5;
    private const int PooledConnectionIdleTimeoutMinutes = 2;
    private HttpClient _httpClient;
    private CancellationTokenSource _shutdown;
    private static AssetBundleLoader<TMP_FontAsset> _font;
    private static IEnumerator _fontLoad;
    private static Coroutine _fontCoroutine;

    internal static new ManualLogSource Log { get; private set; }
    internal static MonoBehaviour Instance { get; private set; }
    internal static TranslationManager Translations { get; private set; }
    internal static MissingSceneReporter MissingSceneReporter { get; private set; }

    public override void Load()
    {
        TrySetUtf8Console();

        Log = base.Log;
        Logger.Info($"Plugin {MyPluginInfo.PLUGIN_GUID} is loading");

        Toast.Initialize();
        MuvluvMod.Config.Initialize(base.Config);
        Instance = AddComponent<RuntimeController>();

        try
        {
            InitializeServices();
            if (MuvluvMod.Config.CheckForUpdates.Value)
                _ = UpdateChecker.CheckAsync(
                    _httpClient,
                    MyPluginInfo.PLUGIN_VERSION,
                    _shutdown.Token
                );
            MissingSceneReporter.Initialize();
            PatchManager.Initialize();
            LoadTranslationResources();
        }
        catch
        {
            Unload();
            throw;
        }

        Logger.Info($"Plugin {MyPluginInfo.PLUGIN_GUID} loaded successfully");

        Toast.Success(
            MyPluginInfo.PLUGIN_NAME,
            $"Mod加载成功，版本: {MyPluginInfo.PLUGIN_VERSION}"
        );
    }

    private void InitializeServices()
    {
        _shutdown = new CancellationTokenSource();
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression =
                DecompressionMethods.GZip
                | DecompressionMethods.Deflate
                | DecompressionMethods.Brotli,
            PooledConnectionLifetime = TimeSpan.FromMinutes(PooledConnectionLifetimeMinutes),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(PooledConnectionIdleTimeoutMinutes),
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(HttpTimeoutSeconds),
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"{MyPluginInfo.PLUGIN_GUID}/{MyPluginInfo.PLUGIN_VERSION}"
        );

        string cacheDirectory = ResolvePluginPath(MuvluvMod.Config.TranslationCacheDirectory.Value);
        var translationCache = new TranslationCache(
            MuvluvMod.Config.TranslationCdnUrl.Value,
            cacheDirectory,
            MuvluvMod.Config.TranslationLanguage.Value,
            MuvluvMod.Config.TranslationPreferLocalFiles.Value,
            _httpClient,
            _shutdown.Token
        );

        Translations = new TranslationManager(translationCache);
        _font = new AssetBundleLoader<TMP_FontAsset>(
            ResolvePluginPath(MuvluvMod.Config.FontBundlePath.Value)
        );
        MissingSceneReporter = new MissingSceneReporter(
            _httpClient,
            () => MuvluvMod.Config.SubmitMissingScenes.Value,
            _shutdown.Token
        );
    }

    internal static void LoadTranslationResources()
    {
        if (!MuvluvMod.Config.TranslationEnabled.Value || Translations == null)
            return;
        _ = Translations.EnsureSharedTranslationsLoadedAsync();
        if (Instance == null || _font == null || _font.IsLoading || _font.IsLoaded)
            return;
        _fontLoad = _font.Load(
            () =>
            {
                if (!TMP_Settings.fallbackFontAssets.Contains(_font.Asset))
                    TMP_Settings.fallbackFontAssets.Add(_font.Asset);
                Logger.Info($"Fallback font registered: {_font.Asset.name}");
            },
            (Exception error) =>
            {
                Logger.Error($"Font load failed: {error.Message}");
                Toast.Error("字体加载失败", error.Message);
            }
        );
        _fontCoroutine = Instance.StartCoroutine(_fontLoad.WrapToIl2Cpp());
    }

    private static string ResolvePluginPath(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(Paths.PluginPath, path);

    private static void TrySetUtf8Console()
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch { }
    }

    public override bool Unload()
    {
        if (!PatchManager.Shutdown())
        {
            Logger.Warn(
                "Cannot unload while a scenario or master-data load is active; retry when loading finishes"
            );
            return false;
        }
        if (Instance != null)
        {
            Instance.enabled = false;
            if (_fontCoroutine != null)
                Instance.StopCoroutine(_fontCoroutine);
        }
        (_fontLoad as IDisposable)?.Dispose();
        if (_font?.IsLoaded == true)
            TMP_Settings.fallbackFontAssets.Remove(_font.Asset);
        _font = null;
        _fontLoad = null;
        _fontCoroutine = null;
        if (Instance != null)
            UnityEngine.Object.Destroy(Instance);
        Instance = null;
        _shutdown?.Cancel();
        _shutdown?.Dispose();
        _shutdown = null;
        MuvluvMod.Config.Shutdown();
        Toast.Clear();
        _httpClient?.Dispose();
        _httpClient = null;
        Translations = null;
        MissingSceneReporter = null;
        Logger.Info($"Plugin {MyPluginInfo.PLUGIN_GUID} unloaded");
        return true;
    }
}
