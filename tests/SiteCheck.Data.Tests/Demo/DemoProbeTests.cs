using System.Net;
using Microsoft.Extensions.Time.Testing;
using SiteCheck.Core.Models.Probes;
using SiteCheck.Core.Models.Stress;
using SiteCheck.Core.Models.Update;
using SiteCheck.Data.Demo;

namespace SiteCheck.Data.Tests.Demo;

/// <summary>
/// Die Vorführwelt darf nur erfundene Namen kennen und muss die Lagen liefern, die die
/// Vorführung zeigen soll: Vergleich, Halt, Abbruch des Lasttests.
/// </summary>
public sealed class DemoProbeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void Sites_UseOnlyReservedNamesAndDocumentationAddresses()
    {
        Assert.All(DemoWorld.Sites, site =>
        {
            Uri address = new(site.Address);
            Assert.EndsWith(".example", address.Host, StringComparison.Ordinal);
            string? domain = DemoWorld.DomainOf(address.Host);
            Assert.NotNull(domain);
            Assert.StartsWith("192.0.2.", DemoWorld.AddressOf(domain), StringComparison.Ordinal);
            Assert.True(IPAddress.TryParse(DemoWorld.AddressOf(domain), out _));
        });
    }

    [Theory]
    [InlineData("mail.meine-seite.example", "meine-seite.example")]
    [InlineData("SHOP.EXAMPLE.", "shop.example")]
    [InlineData("beispiel.de", null)]
    [InlineData("nicht-meine-seite.example", null)]
    public void DomainOf_MatchesOnlyTheDemoDomainsAndTheirSubdomains(string host, string? expected)
    {
        Assert.Equal(expected, DemoWorld.DomainOf(host));
        Assert.Equal(expected is not null, DemoWorld.IsKnown(host));
    }

    [Fact]
    public async Task Http_UnknownHost_StaysUnanswered()
    {
        DemoHttpProbe http = new(DemoEra.Current);

        Assert.Null(await http.FetchAsync(new Uri("https://beispiel.de/"), Token));
    }

    [Fact]
    public async Task Http_PlainAddress_RedirectsToHttps()
    {
        HttpProbeResponse? response = await new DemoHttpProbe(DemoEra.Current)
            .FetchWithoutRedirectAsync(new Uri("http://blog.example/"), Token);

        Assert.NotNull(response);
        Assert.Equal(301, response.StatusCode);
        Assert.Equal(new Uri("https://blog.example/"), response.Location);
    }

    [Theory]
    [InlineData(DemoEra.Current, 429)]
    [InlineData(DemoEra.Earlier, 404)]
    public async Task Http_ShopRejectsSecurityTxtOnlyToday(DemoEra era, int expected)
    {
        HttpProbeResponse? response = await new DemoHttpProbe(era)
            .FetchAsync(new Uri("https://shop.example/.well-known/security.txt"), Token);

        Assert.Equal(expected, response?.StatusCode);
    }

    [Theory]
    [InlineData(DemoEra.Earlier, false, 200)]
    [InlineData(DemoEra.Current, true, 404)]
    public async Task Http_MainSiteGainedHstsAndClosedItsEnvFile(DemoEra era, bool hasHsts, int envStatus)
    {
        DemoHttpProbe http = new(era);

        HttpProbeResponse? home = await http.FetchAsync(new Uri("https://meine-seite.example/"), Token);
        HttpProbeResponse? env = await http.FetchAsync(new Uri("https://meine-seite.example/.env"), Token);

        Assert.Equal(hasHsts, home?.GetHeader("Strict-Transport-Security") is not null);
        Assert.Equal(envStatus, env?.StatusCode);
    }

    [Theory]
    [InlineData(DemoEra.Earlier, 0)]
    [InlineData(DemoEra.Current, 1)]
    public async Task Dns_MainSiteDmarcFollowsTheEra(DemoEra era, int expectedRecords)
    {
        IReadOnlyList<string> records = await new DemoDnsProbe(era).QueryTextRecordsAsync("_dmarc.meine-seite.example", Token);

        Assert.Equal(expectedRecords, records.Count);
    }

    [Fact]
    public async Task Dns_AnswersOnlyForDemoNames()
    {
        DemoDnsProbe dns = new(DemoEra.Current);

        Assert.Empty(await dns.ResolveIpv4Async("beispiel.de", Token));
        Assert.Equal(["192.0.2.30"], await dns.ResolveIpv4Async("mail.blog.example", Token));
        Assert.Equal("mail.shop.example", await dns.QueryPointerAsync("192.0.2.20", Token));
        Assert.Null(await dns.QueryPointerAsync("203.0.113.5", Token));
    }

    [Fact]
    public async Task Dns_DkimRecordCarriesAReadableKey()
    {
        IReadOnlyList<string> records = await new DemoDnsProbe(DemoEra.Current).QueryTextRecordsAsync("default._domainkey.blog.example", Token);

        string record = Assert.Single(records);
        Assert.Equal(2048, Core.Models.DkimKey.RsaKeySizeBits(record));
    }

    [Fact]
    public async Task Tls_CertificateRunsTwoMonthsFromTheGivenClock()
    {
        FakeTimeProvider clock = new(new DateTimeOffset(2026, 11, 17, 8, 0, 0, TimeSpan.Zero));

        TlsHandshakeResult? shop = await new DemoTlsProbe(clock).HandshakeAsync("shop.example", 443, Token);

        Assert.NotNull(shop);
        Assert.Equal(TlsVersion.Tls12, shop.Version);
        Assert.Equal(clock.GetUtcNow().AddDays(64), shop.CertificateNotAfter);
        Assert.Null(await new DemoTlsProbe(clock).HandshakeAsync("beispiel.de", 443, Token));
    }

    [Fact]
    public async Task Load_ShopRejectsAfterTheFortiethRequest()
    {
        DemoLoadProbe load = new();
        Uri shop = new("https://shop.example/");
        List<RequestOutcome> outcomes = [];
        for (int index = 0; index < 41; index++)
        {
            outcomes.Add(await load.SendAsync(shop, Token));
        }

        Assert.All(outcomes[..40], outcome => Assert.Equal(200, outcome.StatusCode));
        Assert.Equal(429, outcomes[40].StatusCode);
        Assert.All(outcomes, outcome => Assert.InRange(outcome.ElapsedMilliseconds, 90, 250));
    }

    [Fact]
    public async Task Updates_NeverReportANewVersion()
    {
        UpdateInfo info = await new DemoUpdateChecker(new Version(0, 2, 0)).CheckForUpdateAsync(Token);

        Assert.False(info.IsUpdateAvailable);
    }

    [Fact]
    public async Task OfflineClient_RefusesEveryDownload()
    {
        OfflineHttpGetClient client = new();
        Uri address = new("https://api.github.com/");

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetStringAsync(address, Token));
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => client.DownloadFileAsync(address, "x", Token));
    }
}
