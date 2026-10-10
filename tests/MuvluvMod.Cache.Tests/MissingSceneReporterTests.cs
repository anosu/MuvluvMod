using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MuvluvMod.Services;
using Xunit;

namespace MuvluvMod.Tests;

public sealed class MissingSceneReporterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisableOrUnloadWhileListLoadsPreventsSubmission(bool unload)
    {
        bool enabled = true;
        int submissions = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var shutdown = new CancellationTokenSource();
        using var client = new HttpClient(
            new Handler(
                async (request, token) =>
                {
                    if (request.Method == HttpMethod.Post)
                    {
                        Interlocked.Increment(ref submissions);
                        return new HttpResponseMessage(HttpStatusCode.OK);
                    }
                    await release.Task.WaitAsync(token);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "[{\"id\":42}]",
                            System.Text.Encoding.UTF8,
                            "application/json"
                        ),
                    };
                }
            )
        );
        var reporter = new MissingSceneReporter(client, () => enabled, shutdown.Token);
        Task pending = reporter.SubmitIfMissingAsync(
            42,
            "https://example.test/master-data/scenes/42.bin"
        );
        if (unload)
            shutdown.Cancel();
        else
            enabled = false;
        release.SetResult();
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, submissions);
        if (!unload)
        {
            enabled = true;
            await reporter.SubmitIfMissingAsync(
                42,
                "https://example.test/master-data/scenes/42.bin"
            );
            Assert.Equal(1, submissions);
            Assert.False(reporter.ShouldCapture(42));
        }
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
