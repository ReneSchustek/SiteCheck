using SiteCheck.Data.Probes;

namespace SiteCheck.Data.Tests.Probes;

public sealed class DnsProbeLiteralTests
{
    [Fact]
    public async Task ResolveIpv4_WithIpv4Literal_ReturnsItWithoutLookup()
    {
        DnsProbe probe = new();

        IReadOnlyList<string> result = await probe.ResolveIpv4Async("203.0.113.9", TestContext.Current.CancellationToken);

        Assert.Equal(["203.0.113.9"], result);
    }

    [Fact]
    public async Task ResolveIpv4_WithIpv6Literal_ReturnsEmpty()
    {
        DnsProbe probe = new();

        IReadOnlyList<string> result = await probe.ResolveIpv4Async("2001:db8::1", TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task QueryPointer_WithInvalidIp_ReturnsNull()
    {
        DnsProbe probe = new();

        Assert.Null(await probe.QueryPointerAsync("kein-ip", TestContext.Current.CancellationToken));
    }
}
