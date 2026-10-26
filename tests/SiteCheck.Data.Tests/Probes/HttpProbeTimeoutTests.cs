using SiteCheck.Core.Models.Probes;
using SiteCheck.Data.Probes;

namespace SiteCheck.Data.Tests.Probes;

public sealed class HttpProbeTimeoutTests
{
    [Fact]
    public async Task Fetch_WhenClientTimesOut_ReturnsNullNotThrows()
    {
        // Der HttpClient-Timeout wirft eine TaskCanceledException (eine OperationCanceledException),
        // obwohl der übergebene Token nicht abgebrochen ist. Das muss als „keine Antwort“ gelten,
        // nicht als Abbruch — sonst stürzt ein ganzer Lauf an einer langsamen Seite ab.
        using HttpClient following = new(new ThrowingHandler(new TaskCanceledException("timeout")));
        using HttpClient nonFollowing = new(new ThrowingHandler(new TaskCanceledException("timeout")));
        HttpProbe probe = new(following, nonFollowing);

        HttpProbeResponse? response = await probe.FetchAsync(new Uri("https://beispiel.de/"), TestContext.Current.CancellationToken);

        Assert.Null(response);
    }

    [Fact]
    public async Task Fetch_WhenCallerCancels_Throws()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();
        using HttpClient following = new(new ThrowingHandler(new TaskCanceledException("cancel")));
        using HttpClient nonFollowing = new(new ThrowingHandler(new TaskCanceledException("cancel")));
        HttpProbe probe = new(following, nonFollowing);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => probe.FetchAsync(new Uri("https://beispiel.de/"), cts.Token));
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw exception;
        }
    }
}
