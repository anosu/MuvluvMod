using System;
using System.IO;
using System.Threading.Tasks;
using Assets.Api.MemoryDB;
using BepInEx.Configuration;
using Cysharp.Threading.Tasks;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MuvluvMod.Patches;
using Xunit;

namespace MuvluvMod.Tests;

public sealed class TranslationPatchTests : IDisposable
{
    public TranslationPatchTests()
    {
        Config.Initialize(
            new ConfigFile(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".cfg"), false)
            {
                SaveOnConfigSet = false,
            }
        );
        Config.TranslationEnabled.Value = true;
        Plugin.Instance = new();
        Plugin.Translations = new();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingMasterDataUsesTheSwitchAtApplicationTime(bool enabledAtCompletion)
    {
        var row = new Row { Text = "original" };
        var data = new Il2CppReferenceArray<IDataObject> { row };
        var result = new UniTask<Il2CppReferenceArray<IDataObject>>(Task.FromResult(data));
        TranslationPatch.TranslateMasterDataAfterLoad(ref result);
        try
        {
            Assert.True(Plugin.Instance.Step());
            Assert.False(result.GetAwaiter().IsCompleted);
            Assert.True(TranslationPatch.HasPendingLoads);

            Config.TranslationEnabled.Value = enabledAtCompletion;
        }
        finally
        {
            Plugin.Translations.Ready.TrySetResult();
            Plugin.Instance.Step();
        }

        Assert.Same(data, await result);
        Assert.False(TranslationPatch.HasPendingLoads);
        Assert.Equal(enabledAtCompletion ? 1 : 0, Plugin.Translations.Applications);
        Assert.Equal(enabledAtCompletion ? "translated" : "original", row.Text);
    }

    private sealed class Row : IDataObject
    {
        public string Text { get; set; }
    }

    public void Dispose()
    {
        Config.Shutdown();
        TranslationPatch.Reset();
        Plugin.Instance = null;
        Plugin.Translations = null;
    }
}
