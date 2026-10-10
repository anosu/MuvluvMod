namespace MuvluvMod;

// 字段名称与 anosu/BepInEx.ConfigurationManager 的模板保持一致。
// https://github.com/anosu/BepInEx.ConfigurationManager/blob/master/ConfigurationManagerAttributes.cs
internal sealed class ConfigurationManagerAttributes
{
    public string DispName;
    public int? Order;
    public bool? IsAdvanced;
}
