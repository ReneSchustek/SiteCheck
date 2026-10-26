using System.Net;
using System.Text;
using SiteCheck.Core.Models.Probes;
using SiteCheck.Data.Probes;

namespace SiteCheck.Data.Tests.Probes;

public sealed class HttpProbeResponseTests
{
    [Fact]
    public async Task Fetch_ParsesStatusHeadersCookiesAndBody()
    {
        HttpResponseMessage canned = new(HttpStatusCode.OK)
        {
            Content = new StringContent("<?php echo 1;", Encoding.UTF8, "text/html"),
        };
        _ = canned.Headers.TryAddWithoutValidation("X-Powered-By", "PHP/8.5");
        _ = canned.Headers.TryAddWithoutValidation("Set-Cookie", "SID=abc; Secure; HttpOnly; SameSite=Lax");

        using HttpClient client = new(new CannedHandler(canned));
        using HttpClient unused = new(new CannedHandler(new HttpResponseMessage(HttpStatusCode.OK)));
        HttpProbe probe = new(client, unused);

        HttpProbeResponse? response = await probe.FetchAsync(new Uri("https://beispiel.de/"), TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.Equal(200, response!.StatusCode);
        Assert.Equal("PHP/8.5", response.GetHeader("x-powered-by"));
        _ = Assert.Single(response.SetCookieHeaders);
        Assert.Contains("<?php", response.BodyPrefix, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fetch_WithRedirectLocation_IsReadWithoutFollowing()
    {
        HttpResponseMessage canned = new(HttpStatusCode.MovedPermanently);
        canned.Headers.Location = new Uri("https://beispiel.de/");

        using HttpClient following = new(new CannedHandler(new HttpResponseMessage(HttpStatusCode.OK)));
        using HttpClient nonFollowing = new(new CannedHandler(canned));
        HttpProbe probe = new(following, nonFollowing);

        HttpProbeResponse? response = await probe.FetchWithoutRedirectAsync(new Uri("http://beispiel.de/"), TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.Equal(301, response!.StatusCode);
        Assert.Equal(new Uri("https://beispiel.de/"), response.Location);
    }

    private sealed class CannedHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}
