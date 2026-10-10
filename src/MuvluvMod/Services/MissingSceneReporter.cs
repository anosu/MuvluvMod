using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MuvluvMod.Services;

/// <summary>
/// Loads untranslated scene IDs and submits their resolved data-source URLs.
/// </summary>
internal sealed class MissingSceneReporter
{
    private const string MissingScenesUrl = "https://api.ntr.best/debug/muvluvgg/scenes/missing";
    private const string SubmitSceneUrl = "https://api.ntr.best/debug/muvluvgg/scenes/submit";

    private readonly HttpClient _client;
    private readonly Func<bool> _isEnabled;
    private readonly CancellationToken _cancellationToken;
    private readonly object _loadLock = new();
    private readonly ConcurrentDictionary<long, byte> _missingSceneIds = new();
    private readonly ConcurrentDictionary<long, Lazy<Task>> _submissions = new();

    private Task _loadTask;

    public MissingSceneReporter(
        HttpClient client,
        Func<bool> isEnabled,
        CancellationToken cancellationToken = default
    )
    {
        _client = client;
        _isEnabled = isEnabled;
        _cancellationToken = cancellationToken;
    }

    public void Initialize()
    {
        if (_isEnabled())
        {
            Logger.Info("Missing scene reporting enabled; loading scene list");
            _ = EnsureMissingScenesLoadedAsync();
        }
        else
        {
            Logger.Info("Missing scene reporting disabled");
        }
    }

    public bool ShouldCapture(long sceneId)
    {
        if (_cancellationToken.IsCancellationRequested || !_isEnabled())
            return false;

        var loadTask = EnsureMissingScenesLoadedAsync();
        return !loadTask.IsCompleted || _missingSceneIds.ContainsKey(sceneId);
    }

    private Task EnsureMissingScenesLoadedAsync()
    {
        lock (_loadLock)
        {
            return _loadTask ??= LoadMissingScenesAsync();
        }
    }

    private async Task LoadMissingScenesAsync()
    {
        try
        {
            using var response = await _client
                .GetAsync(MissingScenesUrl, _cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warn(
                    $"Missing scene list request failed: {(int)response.StatusCode} {response.StatusCode}"
                );
                return;
            }

            var scenes = await response
                .Content.ReadFromJsonAsync<List<MissingScene>>(
                    cancellationToken: _cancellationToken
                )
                .ConfigureAwait(false);
            if (scenes == null)
            {
                Logger.Warn("Missing scene list response was empty");
                return;
            }

            foreach (var scene in scenes)
                _missingSceneIds.TryAdd(scene.Id, 0);

            Logger.Info($"Missing scene list loaded. Total: {_missingSceneIds.Count}");
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested) { }
        catch (OperationCanceledException)
        {
            Logger.Warn("Missing scene list request timed out");
        }
        catch (Exception e)
        {
            Logger.Error($"Missing scene list request failed: {e.Message}");
        }
    }

    public async Task SubmitIfMissingAsync(long sceneId, string sceneUrl)
    {
        if (!ShouldCapture(sceneId))
            return;
        if (!IsValidSceneUrl(sceneUrl))
        {
            Logger.Warn($"Missing scene URL is invalid [{sceneId}]");
            return;
        }
        await EnsureMissingScenesLoadedAsync().ConfigureAwait(false);
        if (
            _cancellationToken.IsCancellationRequested
            || !_isEnabled()
            || !_missingSceneIds.ContainsKey(sceneId)
        )
            return;

        var lazy = _submissions.GetOrAdd(
            sceneId,
            id => new Lazy<Task>(() => SubmitCoreAsync(id, sceneUrl.Trim()))
        );

        try
        {
            await lazy.Value.ConfigureAwait(false);
        }
        finally
        {
            ((ICollection<KeyValuePair<long, Lazy<Task>>>)_submissions).Remove(new(sceneId, lazy));
        }
    }

    private async Task SubmitCoreAsync(long sceneId, string sceneUrl)
    {
        try
        {
            var payload = new SceneSubmission { Id = sceneId, Url = sceneUrl };
            using var response = await _client
                .PostAsJsonAsync(SubmitSceneUrl, payload, _cancellationToken)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Conflict)
            {
                _missingSceneIds.TryRemove(sceneId, out _);
                Logger.Info(
                    $"Missing scene submitted [{sceneId}]: {(int)response.StatusCode} {response.StatusCode}"
                );
                return;
            }

            Logger.Warn(
                $"Missing scene submission failed [{sceneId}]: "
                    + $"{(int)response.StatusCode} {response.StatusCode}"
            );
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested) { }
        catch (OperationCanceledException)
        {
            Logger.Warn($"Missing scene submission timed out: {sceneId}");
        }
        catch (Exception e)
        {
            Logger.Error($"Missing scene submission failed [{sceneId}]: {e.Message}");
        }
    }

    private static bool IsValidSceneUrl(string url)
    {
        return !string.IsNullOrWhiteSpace(url)
            && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class MissingScene
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
    }

    private sealed class SceneSubmission
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }
    }
}
