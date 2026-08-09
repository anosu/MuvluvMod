using System;
using System.Net.Http;
using System.Threading.Tasks;
using Utility.Toast;

namespace MuvluvMod.Services;

/// <summary>
/// Checks the latest GitHub release redirect without using the rate-limited API.
/// </summary>
internal static class UpdateChecker
{
    private const string LatestReleaseUrl = "https://github.com/anosu/MuvluvMod/releases/latest";
    private const string ReleaseTagPath = "/releases/tag/";

    public static async Task CheckAsync(HttpClient httpClient, string currentVersion)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, LatestReleaseUrl);
            using var response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warn(
                    $"Update check failed: {(int)response.StatusCode} {response.StatusCode}"
                );
                return;
            }

            Uri releaseUri = response.RequestMessage?.RequestUri;
            if (!TryGetVersion(releaseUri, out string latestVersion))
            {
                Logger.Warn($"Update check returned an unexpected URL: {releaseUri}");
                return;
            }

            if (
                string.Equals(
                    NormalizeVersion(currentVersion),
                    latestVersion,
                    StringComparison.OrdinalIgnoreCase
                )
            )
                return;

            Logger.Info(
                $"New version available: {currentVersion} -> {latestVersion}. "
                    + $"Release: {releaseUri}"
            );
            Toast.Info("发现新版本", $"最新版本：{latestVersion}");
        }
        catch (TaskCanceledException)
        {
            Logger.Warn("Update check timed out");
        }
        catch (Exception e)
        {
            Logger.Warn($"Update check failed: {e.Message}");
        }
    }

    private static bool TryGetVersion(Uri releaseUri, out string version)
    {
        version = null;
        if (releaseUri == null)
            return false;

        string path = releaseUri.AbsolutePath.TrimEnd('/');
        int tagIndex = path.IndexOf(ReleaseTagPath, StringComparison.OrdinalIgnoreCase);
        if (tagIndex < 0)
            return false;

        string tag = Uri.UnescapeDataString(path[(tagIndex + ReleaseTagPath.Length)..]);
        if (string.IsNullOrWhiteSpace(tag) || tag.Contains('/'))
            return false;

        version = NormalizeVersion(tag);
        return !string.IsNullOrEmpty(version);
    }

    private static string NormalizeVersion(string version)
    {
        version = version?.Trim();
        return !string.IsNullOrEmpty(version) && (version[0] == 'v' || version[0] == 'V')
            ? version[1..]
            : version;
    }
}
