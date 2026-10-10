// 此游戏的 interop 含有不完整的 NullableAttribute，沿用项目的非可空注解编译方式。
using System;
using BepInEx.Configuration;

namespace MuvluvMod;

internal sealed class AcceptableResourceString : AcceptableValueBase
{
    private readonly string _defaultValue;
    private readonly Func<string, bool> _isValid;
    private readonly string _description;

    private AcceptableResourceString(
        string defaultValue,
        Func<string, bool> isValid,
        string description
    )
        : base(typeof(string))
    {
        _defaultValue = defaultValue;
        _isValid = isValid;
        _description = description;
    }

    internal static AcceptableResourceString HttpUrl(string defaultValue) =>
        new(
            defaultValue,
            text =>
                Uri.TryCreate(text, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
            "An absolute HTTP or HTTPS URL"
        );

    internal static AcceptableResourceString Path(string defaultValue) =>
        new(defaultValue, text => text.Length > 0, "A non-empty resource path");

    public override object Clamp(object value)
    {
        if (value is string text)
        {
            text = text.Trim();
            if (_isValid(text))
                return text;
        }
        return _defaultValue;
    }

    public override bool IsValid(object value) => value is string text && _isValid(text.Trim());

    public override string ToDescriptionString() =>
        $"# Acceptable values: {_description}\n"
        + "# Leading/trailing whitespace is trimmed; invalid values use the default.";
}
