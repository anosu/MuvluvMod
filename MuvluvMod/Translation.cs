using BepInEx;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using BepInEx.Unity.IL2CPP.Utils;


namespace MuvluvMod
{
    public class Translation
    {
        public static string cdn = "http://localhost:5000";
        public static HttpClient client = new();
        public static Dictionary<string, string> names = [];
        public static Dictionary<string, string> teamNames = [];
        public static Dictionary<string, string> titles = [];
        public static Dictionary<string, string> subTitles = [];
        public static Dictionary<long, Dictionary<string, string>> scenes = [];
        public static AssetBundle fontBundle = null;
        public static TMP_FontAsset fontAsset = null;
        public static Material outlineMaterial = null;

        public static TMP_FontAsset rawFontAsset = null;
        public static Material rawOutlineMaterial = null;

        public static bool IsTranslated => scenes.ContainsKey(Patch.sceneId);

        public static void Initialize()
        {
            cdn = Config.TranslationCDN.Value;
            Task.Run(LoadTranslation);
            Plugin.Instance.StartCoroutine(LoadFontAsset());
        }

        public static async Task<T> GetAsync<T>(string url) where T : class
        {
            try
            {
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<T>();
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Error: {e.Message}");
            }
            return null;
        }

        public static async Task LoadTranslation()
        {
            if (!Config.Translation.Value)
            {
                return;
            }
            var nameTask = GetAsync<Dictionary<string, Dictionary<string, string>>>($"{cdn}/translation/names/zh_Hans.json");
            var titleTask = GetAsync<Dictionary<string, Dictionary<string, string>>>($"{cdn}/translation/titles/zh_Hans.json");
            await Task.WhenAll(nameTask, titleTask);

            if (nameTask.Result != null)
            {
                names = nameTask.Result["speakerNames"];
                teamNames = nameTask.Result["teamNames"];
                Plugin.Log.LogInfo($"Character names translation loaded. Total: {names.Count}");
                Plugin.Log.LogInfo($"Team names translation loaded. Total: {teamNames.Count}");
            }
            else
            {
                Plugin.Log.LogWarning($"Names translation load failed");
            }

            if (titleTask.Result != null)
            {
                titles = titleTask.Result["titles"];
                subTitles = titleTask.Result["subTitles"];
                Plugin.Log.LogInfo($"Scenario titles translation loaded. Total: {titles.Count}");
                Plugin.Log.LogInfo($"Scenario subtitles translation loaded. Total: {subTitles.Count}");
            }
            else
            {
                Plugin.Log.LogWarning($"Titles translation load failed");
            }
        }

        public static void LoadFontBundle()
        {
            string path = Config.FontBundlePath.Value;
            string bundlePath = Path.IsPathRooted(path) ? path : Path.Combine(Paths.PluginPath, path);
            if (!File.Exists(bundlePath) || fontBundle != null)
            {
                return;
            }
            fontBundle = AssetBundle.LoadFromMemory(File.ReadAllBytes(bundlePath));
        }

        public static IEnumerator LoadFontAsset()
        {
            if (fontAsset != null || !Config.Translation.Value)
            {
                yield break;
            }
            LoadFontBundle();
            if (fontBundle == null)
            {
                Plugin.Log.LogWarning("Font bundle load failed");
                yield break;
            }
            var request = fontBundle.LoadAssetAsync(Config.FontAssetName.Value);
            yield return request;

            fontAsset = request.asset.TryCast<TMP_FontAsset>();
            Plugin.Log.LogInfo($"TMP_FontAsset {fontAsset.name} is loaded");

            var materialRequest = fontBundle.LoadAssetAsync($"{Config.FontAssetName.Value} Outline");
            yield return materialRequest;

            outlineMaterial = materialRequest.asset.TryCast<Material>();
            Plugin.Log.LogInfo($"Material {outlineMaterial.name} is loaded");
        }

        public static async Task GetScenarioTranslationAsync(long sceneId)
        {
            if (scenes.ContainsKey(sceneId))
            {
                return;
            }
            var translations = await GetAsync<Dictionary<string, string>>($"{cdn}/translation/scenes/{sceneId}/zh_Hans.json");
            if (translations != null)
            {
                scenes[sceneId] = translations;
                Plugin.Log.LogInfo($"Scenario translation loaded. Total: {translations.Count}");
            }
            else
            {
                Plugin.Log.LogWarning($"Translations loaded failed: {sceneId}");
            }
        }

    }
}
