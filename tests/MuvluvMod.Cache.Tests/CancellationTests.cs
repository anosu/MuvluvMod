using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MuvluvMod.Services;
using Xunit;

namespace MuvluvMod.Tests;

public sealed class CancellationTests
{
    [Fact]
    public async Task CancelingSessionStopsManifestDownloadAndCacheWrites()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "MuvluvMod.Tests",
            Guid.NewGuid().ToString("N")
        );
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var shutdown = new CancellationTokenSource();
        using var client = new HttpClient(
            new Handler(
                async (_, token) =>
                {
                    started.SetResult();
                    await Task.Delay(Timeout.Infinite, token);
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }
            )
        );
        var cache = new TranslationCache(
            "https://example.test",
            directory,
            "zh_Hans",
            false,
            client,
            shutdown.Token
        );
        var loading = cache.LoadSceneTranslationsAsync(1);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        shutdown.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            loading.WaitAsync(TimeSpan.FromSeconds(5))
        );
        Assert.False(Directory.Exists(directory));
    }

    private sealed class Handler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send
    ) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken token
        ) => send(request, token);
    }
}
