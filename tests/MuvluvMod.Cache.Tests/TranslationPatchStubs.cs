using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Assets.Api.MemoryDB;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

// 仅替换游戏宿主与 UniTask 调度；等待和配置开关判断执行实际补丁代码。
namespace MuvluvMod
{
    internal static class Plugin
    {
        internal static CoroutineHost Instance;
        internal static TestTranslations Translations;
    }

    internal sealed class CoroutineHost
    {
        private IEnumerator _routine;

        internal void StartCoroutine(IEnumerator routine) => _routine = routine;

        internal bool Step() => _routine.MoveNext();
    }

    internal sealed class TestTranslations
    {
        internal readonly TaskCompletionSource Ready = new();
        internal readonly Dictionary<string, string> MasterDataTranslations = new()
        {
            ["jp"] = "zh",
        };
        internal readonly Dictionary<string, string> SpeakerNames = new();
        internal readonly Dictionary<string, string> TeamNames = new();
        internal int Applications;

        internal Task EnsureSharedTranslationsLoadedAsync() => Ready.Task;

        internal Task EnsureSceneTranslationsLoadedAsync(long id) => Ready.Task;

        internal bool TryGetSceneTranslation(long id, out Dictionary<string, string> translations)
        {
            translations = null;
            return false;
        }

        internal (int MatchedObjects, int TranslatedFields) TranslateMasterData(
            Il2CppReferenceArray<IDataObject> data
        )
        {
            Applications++;
            foreach (var row in data)
                row.Text = "translated";
            return (data.Count, data.Count);
        }
    }
}

namespace Cysharp.Threading.Tasks
{
    public readonly struct UniTask<T>(Task<T> task)
    {
        public TaskAwaiter<T> GetAwaiter() => task.GetAwaiter();
    }

    public sealed class UniTaskCompletionSource<T>
    {
        private readonly TaskCompletionSource<T> _source = new();
        public UniTask<T> Task => new(_source.Task);

        public bool TrySetResult(T value) => _source.TrySetResult(value);

        public bool TrySetException(Il2CppSystem.Exception error) => _source.TrySetException(error);
    }
}

namespace Il2CppSystem
{
    public sealed class Exception(string message) : System.Exception(message) { }
}

namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{
    public sealed class Il2CppReferenceArray<T> : List<T> { }
}

namespace BepInEx.Unity.IL2CPP.Utils
{
    internal static class NamespaceMarker { }
}

namespace Assets.Api.Client
{
    public sealed class SceneFrameMaster
    {
        public string ConfigurationJson;
    }
}

namespace Assets.Api.MemoryDB
{
    public interface IDataObject
    {
        string Text { get; set; }
    }

    public sealed class MemoryDB
    {
        public void LoadMasterData() { }
    }
}

namespace Assets.GameUi.Service
{
    public sealed class EpisodeService
    {
        public void DownloadSceneFrameMasters() { }
    }
}

namespace Assets.GameUi.Scenario
{
    public sealed class ScenarioController
    {
        public void GenerateFrames() { }
    }
}

namespace Assets.GameUi.Scenario.History
{
    public sealed class ScenarioHistoryCell
    {
        public void ApplyText() { }
    }
}

namespace Assets.GameUi.Scenario.Choice
{
    public sealed class ScenarioChoiceElementComponent
    {
        public sealed class Args
        {
            public string Text;
        }

        public void Apply() { }
    }
}
