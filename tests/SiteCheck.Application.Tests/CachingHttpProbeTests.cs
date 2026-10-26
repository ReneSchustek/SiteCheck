using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models.Probes;

namespace SiteCheck.Application.Tests;

public sealed class CachingHttpProbeTests
{
    [Fact]
    public async Task FetchAsync_SameUrlTwice_CallsInnerOnce()
    {
        CountingHttpProbe inner = new(FakeResponse());
        CachingHttpProbe caching = new(inner);
        Uri url = new("https://beispiel.de/");

        _ = await caching.FetchAsync(url, TestContext.Current.CancellationToken);
        _ = await caching.FetchAsync(url, TestContext.Current.CancellationToken);

        Assert.Equal(1, inner.FollowingCalls);
    }

    [Fact]
    public async Task FetchAsync_CachesNullResponse_SoDeadSiteIsNotRetried()
    {
        CountingHttpProbe inner = new(response: null);
        CachingHttpProbe caching = new(inner);
        Uri url = new("https://tot.beispiel.de/");

        Assert.Null(await caching.FetchAsync(url, TestContext.Current.CancellationToken));
        Assert.Null(await caching.FetchAsync(url, TestContext.Current.CancellationToken));

        Assert.Equal(1, inner.FollowingCalls);
    }

    [Fact]
    public async Task FetchAsync_DifferentUrls_CallInnerSeparately()
    {
        CountingHttpProbe inner = new(FakeResponse());
        CachingHttpProbe caching = new(inner);

        _ = await caching.FetchAsync(new Uri("https://beispiel.de/a"), TestContext.Current.CancellationToken);
        _ = await caching.FetchAsync(new Uri("https://beispiel.de/b"), TestContext.Current.CancellationToken);

        Assert.Equal(2, inner.FollowingCalls);
    }

    [Fact]
    public async Task FetchWithoutRedirectAsync_SameUrlTwice_CallsInnerOnce()
    {
        CountingHttpProbe inner = new(FakeResponse());
        CachingHttpProbe caching = new(inner);
        Uri url = new("https://beispiel.de/");

        _ = await caching.FetchWithoutRedirectAsync(url, TestContext.Current.CancellationToken);
        _ = await caching.FetchWithoutRedirectAsync(url, TestContext.Current.CancellationToken);

        Assert.Equal(1, inner.NonFollowingCalls);
    }

    private static HttpProbeResponse FakeResponse() =>
        new(200, null, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), [], string.Empty);

    private sealed class CountingHttpProbe(HttpProbeResponse? response) : IHttpProbe
    {
        public int FollowingCalls { get; private set; }

        public int NonFollowingCalls { get; private set; }

        public Task<HttpProbeResponse?> FetchAsync(Uri address, CancellationToken cancellationToken)
        {
            FollowingCalls++;
            return Task.FromResult(response);
        }

        public Task<HttpProbeResponse?> FetchWithoutRedirectAsync(Uri address, CancellationToken cancellationToken)
        {
            NonFollowingCalls++;
            return Task.FromResult(response);
        }
    }
}
