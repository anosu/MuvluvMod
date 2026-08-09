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
    public static void BeginSceneTranslationLoad(long sceneMasterId, out Task __state)
    {
        Logger.Info($"Scene: {sceneMasterId}");
        PatchManager.SetCurrentScene(sceneMasterId);

        if (!Config.TranslationEnabled.Value || Plugin.Translations == null)
        {
            __state = null;
            return;
        }

        __state = Plugin.Translations.EnsureSceneTranslationsLoadedAsync(sceneMasterId);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EpisodeService), nameof(EpisodeService.DownloadSceneFrameMasters))]
    public static void WaitForSceneTranslation(
        long sceneMasterId,
        Task __state,
        ref UniTask<Il2CppReferenceArray<SceneFrameMaster>> __result
    )
    {
        if (__state == null || Plugin.Instance == null)
            return;

        __result = WaitForTranslation(
            __result,
            __state,
            null,
            $"Scenario translation [{sceneMasterId}]"
        );
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ScenarioController), nameof(ScenarioController.GenerateFrames))]
    public static void TranslateFrames(Il2CppReferenceArray<SceneFrameMaster> masters)
    {
        if (
            !PatchManager.TryGetCurrentSceneTranslation(out var sceneTranslations)
            || masters == null
        )
            return;

        try
        {
            foreach (var frame in masters)
            {
                if (frame == null || string.IsNullOrEmpty(frame.ConfigurationJson))
                    continue;

                var configuration = JsonNode.Parse(frame.ConfigurationJson);
                if (
                    configuration?["Phrase"] is JsonObject phrase
                    && TranslatePhrase(phrase, sceneTranslations)
                )
                    frame.ConfigurationJson = configuration.ToJsonString();
            }
        }
        catch (Exception e)
        {
            Logger.Error($"Error in ReplaceTranslation: {e}");
        }
    }

    private static bool TranslatePhrase(
        JsonObject phrase,
        Dictionary<string, string> sceneTranslations
    ) =>
        TranslateJsonProperty(phrase, "SpeakerName", Plugin.Translations.SpeakerNames)
        | TranslateJsonProperty(phrase, "TeamName", Plugin.Translations.TeamNames)
        | TranslateJsonProperty(phrase, "Text", sceneTranslations);

    private static bool TranslateJsonProperty(
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
            && !string.Equals(original, translated, StringComparison.Ordinal)
        )
        {
            json[name] = translated;
            return true;
        }

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ScenarioHistoryCell), nameof(ScenarioHistoryCell.ApplyText))]
    public static void TranslateHistoryChoice(ref string phrase, bool isAnswer)
    {
        if (
            isAnswer
            && PatchManager.TryGetCurrentSceneTranslation(out var sceneTranslations)
            && sceneTranslations.TryGetValue(phrase, out var translatedText)
        )
            phrase = translatedText;
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(ScenarioChoiceElementComponent),
        nameof(ScenarioChoiceElementComponent.Apply)
    )]
    public static void TranslateChoice(ScenarioChoiceElementComponent.Args args)
    {
        if (
            PatchManager.TryGetCurrentSceneTranslation(out var sceneTranslations)
            && sceneTranslations.TryGetValue(args.Text, out var translatedText)
        )
            args.Text = translatedText;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MemoryDB), nameof(MemoryDB.LoadMasterData))]
    public static void TranslateMasterDataAfterLoad(
        ref UniTask<Il2CppReferenceArray<IDataObject>> __result
    )
    {
        if (
            !Config.TranslationEnabled.Value
            || Plugin.Translations == null
            || Plugin.Instance == null
        )
            return;

        __result = WaitForTranslation(
            __result,
            Plugin.Translations.EnsureSharedTranslationsLoadedAsync(),
            ApplyMasterDataTranslation,
            "MasterData translation"
        );
    }

    private static void ApplyMasterDataTranslation(Il2CppReferenceArray<IDataObject> masterData)
    {
        if (Plugin.Translations.MasterDataTranslations.Count == 0)
            return;

        var result = Plugin.Translations.TranslateMasterData(masterData);
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

    private static UniTask<T> WaitForTranslation<T>(
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

        while (!translationTask.IsCompleted)
            yield return null;

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
