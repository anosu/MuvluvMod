using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Assets.Api.Client;
using Assets.Api.MemoryDB;
using Assets.GameUi.Scenario;
using Assets.GameUi.Scenario.Choice;
using Assets.GameUi.Scenario.History;
using Assets.GameUi.Service;
using BepInEx.Unity.IL2CPP.Utils;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Utility.Toast;

namespace MuvluvMod.Patches;

/// <summary>
/// Loads scenario translations and applies translated text.
/// </summary>
[HarmonyPatch]
public static class TranslationPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(EpisodeService), nameof(EpisodeService.DownloadSceneFrameMasters))]
    public static void PrepareTranslation(
        EpisodeService __instance,
        long sceneMasterId,
        out Task __state
    )
    {
        Logger.Info($"Scene: {sceneMasterId}");
        PatchManager.SetScene(sceneMasterId);
        __instance?.sceneFrameMastersCache?.Remove(sceneMasterId);

        if (!Config.Translation.Value || Plugin.Trans == null)
        {
            __state = null;
            return;
        }

        __state = Plugin.Trans.EnsureSceneReadyAsync(sceneMasterId);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EpisodeService), nameof(EpisodeService.DownloadSceneFrameMasters))]
    public static void AwaitTranslation(
        long sceneMasterId,
        Task __state,
        ref UniTask<Il2CppReferenceArray<SceneFrameMaster>> __result
    )
    {
        if (__state == null || Plugin.Instance == null)
            return;

        __result = AwaitTranslation(
            __result,
            __state,
            null,
            $"Scenario translation [{sceneMasterId}]"
        );
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ScenarioController), nameof(ScenarioController.GenerateFrames))]
    public static void ReplaceTranslation(Il2CppReferenceArray<SceneFrameMaster> masters)
    {
        if (!PatchManager.TryGetCurrentScene(out var scene) || masters == null)
            return;

        try
        {
            foreach (var frame in masters)
            {
                if (frame == null || string.IsNullOrEmpty(frame.ConfigurationJson))
                    continue;

                var configuration = JsonNode.Parse(frame.ConfigurationJson);
                if (configuration?["Phrase"] is JsonObject phrase)
                    TranslatePhrase(phrase, scene);

                frame.ConfigurationJson = configuration?.ToJsonString() ?? frame.ConfigurationJson;
            }
        }
        catch (Exception e)
        {
            Logger.Error($"Error in ReplaceTranslation: {e}");
        }
    }

    private static void TranslatePhrase(JsonObject phrase, Dictionary<string, string> scene)
    {
        TranslateJsonProperty(phrase, "SpeakerName", Plugin.Trans.Names);
        TranslateJsonProperty(phrase, "TeamName", Plugin.Trans.TeamNames);
        TranslateJsonProperty(phrase, "Text", scene);
    }

    private static void TranslateJsonProperty(
        JsonObject json,
        string name,
        IReadOnlyDictionary<string, string> translations
    )
    {
        if (
            json.TryGetPropertyValue(name, out var node)
            && node is JsonValue value
            && value.TryGetValue<string>(out var original)
            && translations.TryGetValue(original, out var translated)
        )
            json[name] = translated;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ScenarioHistoryCell), nameof(ScenarioHistoryCell.ApplyText))]
    public static void ReplaceHistoryChoice(ref string phrase, bool isAnswer)
    {
        if (
            isAnswer
            && PatchManager.TryGetCurrentScene(out var scene)
            && scene.TryGetValue(phrase, out var translatedText)
        )
            phrase = translatedText;
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(ScenarioChoiceElementComponent),
        nameof(ScenarioChoiceElementComponent.Apply)
    )]
    public static void ReplaceChoice(ScenarioChoiceElementComponent.Args args)
    {
        if (
            PatchManager.TryGetCurrentScene(out var scene)
            && scene.TryGetValue(args.Text, out var translatedText)
        )
            args.Text = translatedText;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MemoryDB), nameof(MemoryDB.LoadMasterData))]
    public static void ReplaceMasterData(ref UniTask<Il2CppReferenceArray<IDataObject>> __result)
    {
        if (!Config.Translation.Value || Plugin.Trans == null || Plugin.Instance == null)
            return;

        __result = AwaitTranslation(
            __result,
            Plugin.Trans.EnsureStaticTranslationsLoadedAsync(),
            ApplyMasterDataTranslation,
            "MasterData translation"
        );
    }

    private static void ApplyMasterDataTranslation(Il2CppReferenceArray<IDataObject> masterData)
    {
        if (Plugin.Trans.MasterTranslations.Count == 0)
            return;

        var result = Plugin.Trans.TranslateMasterData(masterData);
        Logger.Info(
            $"MasterData translated. Objects: {masterData.Count}, "
                + $"Matched: {result.MatchedObjects}, Fields: {result.TranslatedFields}"
        );

        if (result.MatchedObjects > 0 && result.TranslatedFields == 0)
        {
            Logger.Warn("MasterData translation matched objects but changed no fields");
            Toast.Warn("MasterData翻译", "已匹配数据类型，但没有字段被翻译");
        }
    }

    private static UniTask<T> AwaitTranslation<T>(
        UniTask<T> sourceTask,
        Task translationTask,
        Action<T> applyTranslation,
        string operation
    )
    {
        var completion = new UniTaskCompletionSource<T>();
        Plugin.Instance.StartCoroutine(
            AwaitTranslationCoroutine(
                sourceTask,
                translationTask,
                applyTranslation,
                operation,
                completion
            )
        );
        return completion.Task;
    }

    private static IEnumerator AwaitTranslationCoroutine<T>(
        UniTask<T> sourceTask,
        Task translationTask,
        Action<T> applyTranslation,
        string operation,
        UniTaskCompletionSource<T> completion
    )
    {
        var sourceAwaiter = sourceTask.GetAwaiter();
        while (!sourceAwaiter.IsCompleted)
            yield return null;

        T result;
        try
        {
            result = sourceAwaiter.GetResult();
        }
        catch (Exception e)
        {
            Logger.Error($"{operation} source task failed: {e.Message}");
            completion.TrySetException(new Il2CppSystem.Exception(e.Message));
            yield break;
        }

        while (!translationTask.IsCompleted && Config.Translation.Value)
            yield return null;

        if (!Config.Translation.Value)
        {
            completion.TrySetResult(result);
            yield break;
        }

        try
        {
            translationTask.GetAwaiter().GetResult();
            applyTranslation?.Invoke(result);
        }
        catch (Exception e)
        {
            Logger.Error($"{operation} failed: {e}");
        }

        completion.TrySetResult(result);
    }
}
