using BepInEx.Configuration;

namespace MuvluvMod
{
    public static class Config
    {
        public static ConfigEntry<bool> DynamicMosaic;
        public static ConfigEntry<bool> EnableSkipButton;
        public static ConfigEntry<bool> VoiceInterruption;
        public static ConfigEntry<bool> AutoSkipBattle;

        public static ConfigEntry<bool> Translation;
        public static ConfigEntry<string> TranslationCDN;
        public static ConfigEntry<string> FontBundlePath;
        public static ConfigEntry<string> FontAssetName;

        public static void Initialize()
        {
            DynamicMosaic = Plugin.Config.Bind("Gerneral", "DynamicMosaic", false, "是否开启游戏内动态马赛克");
            EnableSkipButton = Plugin.Config.Bind("Gerneral", "EnableSkipButton", false, "是否总是开启跳过按钮");
            VoiceInterruption = Plugin.Config.Bind("Gerneral", "VoiceInterruption", true, "剧情中播放下一句话时是否中断当前语音");
            AutoSkipBattle = Plugin.Config.Bind("Gerneral", "AutoSkipBattle", false, "自动跳过战斗（自动按跳过键，不受跳过键开关影响）");

            Translation = Plugin.Config.Bind("Translation", "Enable", true, "是否开启汉化");
            TranslationCDN = Plugin.Config.Bind("Translation", "CdnURL", "https://muvluvgg.ntr.best", "翻译加载的CDN");
            FontBundlePath = Plugin.Config.Bind("Translation", "FontBundlePath", "font/sarasagothicsc-bold", "TMP字体AssetBundle的路径");
            FontAssetName = Plugin.Config.Bind("Translation", "FontAssetName", "SarasaGothicSC-Bold SDF", "AssetBundle中TMP_FontAsset的名称");

            Plugin.Log.LogInfo("Translation: " + (Translation.Value ? "Enabled" : "Disabled"));
            Plugin.Log.LogInfo("Translation CDN: " + TranslationCDN.Value);
            Plugin.Log.LogInfo("Font Bundle Path: " + FontBundlePath.Value);
            Plugin.Log.LogInfo("Font Asset Name: " + FontAssetName.Value);

            Plugin.Config.SettingChanged += (object sender, SettingChangedEventArgs e) =>
            {
                var config = e.ChangedSetting;
                Plugin.Log.LogInfo($"[{config.Definition.Section}] {config.Definition.Key} => {config.BoxedValue}");
            };
        }
    }
}
