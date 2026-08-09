using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using MuvluvMod.Patches;
using MuvluvMod.Services;
using UnityEngine;
using Utility.Fonts;
using Utility.Toast;

namespace MuvluvMod;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    private const int HttpTimeoutSeconds = 10;
    private const int PooledConnectionLifetimeMinutes = 5;
    private const int PooledConnectionIdleTimeoutMinutes = 2;
    private HttpClient _httpClient;

    public static new ManualLogSource Log;
    public static MonoBehaviour Instance;
    public static TranslationManager Translations;
    public static MissingSceneReporter MissingSceneReporter;

    public override void Load()
    {
        TrySetUtf8Console();

        Log = base.Log;
        Logger.Info($"Plugin {MyPluginInfo.PLUGIN_GUID} is loading");

        AddComponent<ToastUI>();
        MuvluvMod.Config.Initialize(base.Config);
        Instance = AddComponent<Hotkey>();

        InitializeServices();
        if (MuvluvMod.Config.CheckForUpdates.Value)
            _ = UpdateChecker.CheckAsync(_httpClient, MyPluginInfo.PLUGIN_VERSION);
        MissingSceneReporter.Initialize();
        PatchManager.Initialize();
        Translations.Initialize();

        Logger.Info($"Plugin {MyPluginInfo.PLUGIN_GUID} loaded successfully");

        Toast.Success(
            MyPluginInfo.PLUGIN_NAME,
            $"Mod加载成功，版本: {MyPluginInfo.PLUGIN_VERSION}"
        );
    }

    private void InitializeServices()
    {
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
            _httpClient
        );

        Translations = new TranslationManager(
            translationCache,
            new FontHelper(ResolvePluginPath(MuvluvMod.Config.FontBundlePath.Value))
        );
        MissingSceneReporter = new MissingSceneReporter(_httpClient);
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
        PatchManager.Shutdown();
        MuvluvMod.Config.Shutdown();
        Toast.Clear();
        _httpClient?.Dispose();
        Logger.Info($"Plugin {MyPluginInfo.PLUGIN_GUID} unloaded");
        return base.Unload();
    }
}
