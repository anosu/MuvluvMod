using BepInEx.Configuration;
using Utility.Notifications;

namespace MuvluvMod;

/// <summary>集中定义 BepInEx 配置、显示元数据和变更通知。</summary>
internal static class Config
{
    private const string DefaultTranslationCdnUrl =
        "https://raw.githubusercontent.com/anosu/muvluvgg-translation/refs/heads/main";
    private const string DefaultTranslationCacheDirectory =
        MyPluginInfo.PLUGIN_GUID + "/translation";
    private const string DefaultFontBundlePath =
        MyPluginInfo.PLUGIN_GUID + "/fonts/sarasagothicsc-bold";
    private static ConfigFile _config;

    public static ConfigEntry<bool> DynamicMosaic { get; private set; }
    public static ConfigEntry<bool> EnableSkipButton { get; private set; }
    public static ConfigEntry<bool> VoiceInterruption { get; private set; }
    public static ConfigEntry<bool> AutoSkipBattle { get; private set; }
    public static ConfigEntry<bool> CheckForUpdates { get; private set; }
    public static ConfigEntry<bool> TranslationEnabled { get; private set; }
    public static ConfigEntry<string> TranslationCdnUrl { get; private set; }
    public static ConfigEntry<string> TranslationLanguage { get; private set; }
    public static ConfigEntry<string> TranslationCacheDirectory { get; private set; }
    public static ConfigEntry<bool> TranslationPreferLocalFiles { get; private set; }
    public static ConfigEntry<string> FontBundlePath { get; private set; }
    public static ConfigEntry<bool> SubmitMissingScenes { get; private set; }

    public static void Initialize(ConfigFile config)
    {
        _config = config;
        BindEntries(config);
        config.SettingChanged += OnSettingChanged;
    }

    public static void Shutdown()
    {
        if (_config != null)
            _config.SettingChanged -= OnSettingChanged;
        _config = null;
    }

    private static void BindEntries(ConfigFile config)
    {
        DynamicMosaic = config.Bind(
            "General",
            "DynamicMosaic",
            false,
            new ConfigDescription(
                "显示游戏内动态马赛克；下次载入对应场景时生效。",
                null,
                new ConfigurationManagerAttributes { DispName = "动态马赛克", Order = 100 }
            )
        );
        EnableSkipButton = config.Bind(
            "General",
            "EnableSkipButton",
            false,
            new ConfigDescription(
                "始终开启战斗跳过按钮；下一次更新按钮状态时生效。快捷键：F3。",
                null,
                new ConfigurationManagerAttributes { DispName = "始终允许跳过", Order = 99 }
            )
        );
        VoiceInterruption = config.Bind(
            "General",
            "VoiceInterruption",
            true,
            new ConfigDescription(
                "剧情中播放下一句话时中断当前语音；即时生效。快捷键：F4。",
                null,
                new ConfigurationManagerAttributes { DispName = "语音中断", Order = 98 }
            )
        );
        AutoSkipBattle = config.Bind(
            "General",
            "AutoSkipBattle",
            false,
            new ConfigDescription(
                "自动跳过战斗，不受跳过按钮开关影响；下一次战斗触发时生效。快捷键：F5。",
                null,
                new ConfigurationManagerAttributes { DispName = "自动跳过战斗", Order = 97 }
            )
        );
        CheckForUpdates = config.Bind(
            "Update",
            "CheckForUpdates",
            true,
            new ConfigDescription(
                "启动时检查插件更新；修改后需重启游戏。",
                null,
                new ConfigurationManagerAttributes { DispName = "检查更新", Order = 96 }
            )
        );
        TranslationEnabled = config.Bind(
            "Translation",
            "Enable",
            true,
            new ConfigDescription(
                "启用翻译；开启时预加载数据和字体，对后续载入的剧情及主数据生效，已译内容需重启还原。",
                null,
                new ConfigurationManagerAttributes { DispName = "启用翻译", Order = 95 }
            )
        );
        TranslationCdnUrl = config.Bind(
            "Translation",
            "CdnURL",
            DefaultTranslationCdnUrl,
            new ConfigDescription(
                "翻译 CDN 根地址；提交时去除首尾空格，无效值使用默认值；修改后需重启游戏。",
                AcceptableResourceString.HttpUrl(DefaultTranslationCdnUrl),
                new ConfigurationManagerAttributes
                {
                    DispName = "翻译 CDN",
                    Order = 94,
                    IsAdvanced = true,
                }
            )
        );
        TranslationLanguage = config.Bind(
            "Translation",
            "Language",
            "zh_Hans",
            new ConfigDescription(
                "翻译语言，目前支持简体中文；修改后需重启游戏。",
                new AcceptableValueList<string>("zh_Hans"),
                new ConfigurationManagerAttributes { DispName = "翻译语言", Order = 93 }
            )
        );
        TranslationCacheDirectory = config.Bind(
            "Translation.Cache",
            "Directory",
            DefaultTranslationCacheDirectory,
            new ConfigDescription(
                "缓存目录；相对路径以 BepInEx/plugins 为基准，也可填写绝对路径；提交时去除首尾空格，无效值使用默认值；修改后需重启游戏。",
                AcceptableResourceString.Path(DefaultTranslationCacheDirectory),
                new ConfigurationManagerAttributes
                {
                    DispName = "缓存目录",
                    Order = 92,
                    IsAdvanced = true,
                }
            )
        );
        TranslationPreferLocalFiles = config.Bind(
            "Translation.Cache",
            "PreferLocalFiles",
            false,
            new ConfigDescription(
                "优先使用已有本地翻译，不校验远程哈希（manifest 除外）；修改后需重启游戏。",
                null,
                new ConfigurationManagerAttributes
                {
                    DispName = "优先使用本地翻译",
                    Order = 91,
                    IsAdvanced = true,
                }
            )
        );
        FontBundlePath = config.Bind(
            "Translation.Font",
            "AssetBundlePath",
            DefaultFontBundlePath,
            new ConfigDescription(
                "TMP 字体 AssetBundle 路径；相对路径以 BepInEx/plugins 为基准；提交时去除首尾空格，无效值使用默认值；修改后需重启游戏。",
                AcceptableResourceString.Path(DefaultFontBundlePath),
                new ConfigurationManagerAttributes
                {
                    DispName = "字体资源路径",
                    Order = 90,
                    IsAdvanced = true,
                }
            )
        );
        SubmitMissingScenes = config.Bind(
            "Translation.Debug",
            "SubmitMissingScenes",
            true,
            new ConfigDescription(
                "向调试服务提交翻译缺失的剧本资源地址；对之后捕获的剧本生效。",
                null,
                new ConfigurationManagerAttributes
                {
                    DispName = "提交缺失剧本",
                    Order = 89,
                    IsAdvanced = true,
                }
            )
        );
    }

    private static void OnSettingChanged(object sender, SettingChangedEventArgs e)
    {
        var setting = e.ChangedSetting;
        object value = setting.BoxedValue;
        Logger.Info($"[{setting.Definition.Section}] {setting.Definition.Key} => {value}");
        if (setting.SettingType == typeof(bool))
            Toast.Info($"[{setting.Definition.Section}]", $"{setting.Definition.Key} => {value}");
    }
}
