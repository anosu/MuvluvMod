namespace MuvluvMod;

/// <summary>
/// Provides a single logging entry point for the mod.
/// </summary>
public static class Logger
{
    public static void Info(string message) => Plugin.Log.LogInfo(message);

    public static void Warn(string message) => Plugin.Log.LogWarning(message);

    public static void Error(string message) => Plugin.Log.LogError(message);
}
