using System;
using System.IO;
using BepInEx.Configuration;
using Utility.Notifications;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace MuvluvMod.Tests;

public sealed class ConfigurationTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "MuvluvMod.Tests",
        Guid.NewGuid().ToString("N")
    );
    private readonly ConfigFile _file;

    public ConfigurationTests()
    {
        Directory.CreateDirectory(_directory);
        _file = new ConfigFile(Path.Combine(_directory, "legacy.cfg"), false)
        {
            SaveOnConfigSet = false,
        };
        Config.Initialize(_file);
        Toast.Count = 0;
    }

    [Fact]
    public void LegacyValuesRoundTripThroughBepInEx()
    {
        File.WriteAllText(
            _file.ConfigFilePath,
            "[General]\nVoiceInterruption = false\n[Translation]\nEnable = false\nCdnURL = https://example.test/translations\n[Translation.Cache]\nPreferLocalFiles = true\n"
        );
        _file.Reload();
        Assert.False(Config.VoiceInterruption.Value);
        Assert.False(Config.TranslationEnabled.Value);
        Assert.True(Config.TranslationPreferLocalFiles.Value);
        Assert.Equal("https://example.test/translations", Config.TranslationCdnUrl.Value);
        _file.Save();
        Assert.Contains(
            "CdnURL = https://example.test/translations",
            File.ReadAllText(_file.ConfigFilePath)
        );
    }

    [Theory]
    [InlineData("  https://example.test/assets  ", "https://example.test/assets")]
    [InlineData("file:///tmp/assets", null)]
    [InlineData("invalid", null)]
    [InlineData("   ", null)]
    public void ResourceUrlIsNormalizedBeforeBeingSaved(string input, string expected)
    {
        var entry = Config.TranslationCdnUrl;
        entry.Value = input;
        Assert.Equal(expected ?? (string)entry.DefaultValue, entry.Value);
        Assert.Equal(0, Toast.Count);
    }

    [Fact]
    public void EmptyFontPathFallsBackAndBooleanNotificationsStopOnShutdown()
    {
        Config.FontBundlePath.Value = "  ";
        Assert.Equal(Config.FontBundlePath.DefaultValue, Config.FontBundlePath.Value);
        Assert.Equal(0, Toast.Count);
        var enabled = Config.TranslationEnabled;
        enabled.Value = !enabled.Value;
        Assert.Equal(1, Toast.Count);
        Config.Shutdown();
        enabled.Value = !enabled.Value;
        Assert.Equal(1, Toast.Count);
    }

    public void Dispose()
    {
        Config.Shutdown();
        Directory.Delete(_directory, true);
    }
}
