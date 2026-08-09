using BepInEx.Configuration;
using Utility.Toast;

namespace MuvluvMod;

/// <summary>
/// Owns all BepInEx configuration entries and change notifications.
/// </summary>
public static class Config
{
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
        BindAllEntries(config);
        config.SettingChanged += OnSettingChanged;
    }

    public static void Shutdown()
    {
        if (_config != null)
            _config.SettingChanged -= OnSettingChanged;
        _config = null;
    }

    private static void BindAllEntries(ConfigFile config)
    {
        DynamicMosaic = config.Bind(
            "General",
            "DynamicMosaic",
            false,
            "是否开启游戏内动态马赛克（默认关闭）"
        );
        EnableSkipButton = config.Bind(
            "General",
            "EnableSkipButton",
            false,
            "是否总是开启跳过按钮（默认关闭）"
        );
        VoiceInterruption = config.Bind(
            "General",
            "VoiceInterruption",
            true,
            "剧情中播放下一句话时是否中断当前语音"
        );
        AutoSkipBattle = config.Bind(
            "General",
            "AutoSkipBattle",
            false,
            "自动跳过战斗（自动按跳过键，不受跳过键开关影响，默认关闭）"
        );
        CheckForUpdates = config.Bind(
            "Update",
            "CheckForUpdates",
            true,
            "启动时是否检查插件更新；修改后重启生效"
        );

        TranslationEnabled = config.Bind(
            "Translation",
            "Enable",
            true,
            "是否开启翻译；修改后重启生效"
        );
        TranslationCdnUrl = config.Bind(
            "Translation",
            "CdnURL",
            "https://raw.githubusercontent.com/anosu/muvluvgg-translation/refs/heads/main",
            "翻译加载的CDN；修改后重启生效"
        );
        TranslationLanguage = config.Bind(
            "Translation",
            "Language",
            "zh_Hans",
            "翻译语言，目前支持：zh_Hans；修改后重启生效"
        );
        TranslationCacheDirectory = config.Bind(
            "Translation.Cache",
            "Directory",
            $"{MyPluginInfo.PLUGIN_GUID}/translation",
            "翻译缓存目录，默认相对于插件目录，也可使用绝对路径；修改后重启生效"
        );
        TranslationPreferLocalFiles = config.Bind(
            "Translation.Cache",
            "PreferLocalFiles",
            false,
            "本地翻译文件存在时是否忽略清单哈希并优先使用本地文件（manifest除外）；修改后重启生效"
        );
        FontBundlePath = config.Bind(
            "Translation.Font",
            "AssetBundlePath",
            $"{MyPluginInfo.PLUGIN_GUID}/fonts/sarasagothicsc-bold",
            "TMP字体AssetBundle的路径，默认相对于插件目录，也可使用绝对路径"
        );
        SubmitMissingScenes = config.Bind(
            "Translation.Debug",
            "SubmitMissingScenes",
            true,
            "是否向调试服务器提交翻译缺失的剧本"
        );
    }

    private static void OnSettingChanged(object sender, SettingChangedEventArgs e)
    {
        var setting = e.ChangedSetting;
        Logger.Info(
            $"[{setting.Definition.Section}] {setting.Definition.Key} => {setting.BoxedValue}"
        );
        Toast.Info(
            $"[{setting.Definition.Section}]",
            $"{setting.Definition.Key} => {setting.BoxedValue}"
        );
    }
}
