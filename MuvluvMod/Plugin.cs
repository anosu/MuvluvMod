using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using System.Text;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

namespace MuvluvMod;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    public static new ConfigFile Config;
    public static new ManualLogSource Log;
    public static MonoBehaviour Instance;

    public override void Load()
    {
        try {
            Console.OutputEncoding = Encoding.UTF8;
        } catch (Exception)
        {
        }

        // Plugin startup logic
        Log = base.Log;
        Config = base.Config;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        MuvluvMod.Config.Initialize();
        Patch.Initialize();
        Instance = AddComponent<PluginBehaviour>();
        Translation.Initialize();
    }
}
