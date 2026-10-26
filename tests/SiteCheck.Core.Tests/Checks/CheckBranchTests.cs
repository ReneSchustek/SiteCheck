using Microsoft.Extensions.Time.Testing;
using SiteCheck.Core.Checks;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Probes;
using SiteCheck.Core.Tests.Fakes;

namespace SiteCheck.Core.Tests.Checks;

/// <summary>Deckt die selteneren Zweige der Prüfungen ab (Fehlerfälle, Grenzwerte, Varianten).</summary>
public sealed class CheckBranchTests
{
    private static CheckTarget Target(string input = "https://beispiel.de/")
    {
        Assert.True(CheckTarget.TryParse(input, out CheckTarget? target, out _));
        return target!;
    }

    private static FakeTimeProvider At(int year, int month, int day) =>
        new(new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Redirect_WhenHttpUnreachable_IsInfo()
    {
        FakeHttpProbe http = new();
        IReadOnlyList<Finding> findings = await new RedirectCheck(http).RunAsync(Target(), TestContext.Current.CancellationToken);
        Finding finding = Assert.Single(findings);
        Assert.Equal("web.redirect.unreachable", finding.Key);
    }

    [Fact]
    public async Task Redirect_WithTemporaryRedirect_IsInfo()
    {
        FakeHttpProbe http = new();
        http.SetNonRedirectResponse("http://beispiel.de/", FakeHttpProbe.Response(302, location: new Uri("https://beispiel.de/")));
        IReadOnlyList<Finding> findings = await new RedirectCheck(http).RunAsync(Target(), TestContext.Current.CancellationToken);
        Finding finding = Assert.Single(findings);
        Assert.Equal(Severity.Info, finding.Severity);
    }

    [Fact]
    public async Task Tls_WhenUnreachable_IsCritical()
    {
        FakeTlsProbe tls = new(null);
        IReadOnlyList<Finding> findings = await new TlsCheck(tls, At(2026, 3, 1)).RunAsync(Target(), TestContext.Current.CancellationToken);
        Assert.Contains(findings, f => f.Key == "web.tls.unreachable" && f.Severity == Severity.Critical);
    }

    [Fact]
    public async Task Tls_WithOutdatedVersion_IsCritical()
    {
        FakeTlsProbe tls = new(new TlsHandshakeResult(TlsVersion.Outdated, new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), "CN=X", true, true));
        IReadOnlyList<Finding> findings = await new TlsCheck(tls, At(2026, 3, 1)).RunAsync(Target(), TestContext.Current.CancellationToken);
        Assert.Contains(findings, f => f.Key == "web.tls.version" && f.Severity == Severity.Critical);
    }

    [Fact]
    public async Task Tls_WithSoonExpiringCertificate_IsWarning()
    {
        FakeTlsProbe tls = new(new TlsHandshakeResult(TlsVersion.Tls13, new DateTimeOffset(2026, 3, 8, 0, 0, 0, TimeSpan.Zero), "CN=X", true, true));
        IReadOnlyList<Finding> findings = await new TlsCheck(tls, At(2026, 3, 1)).RunAsync(Target(), TestContext.Current.CancellationToken);
        Assert.Contains(findings, f => f.Key == "web.tls.cert.expiry" && f.Severity == Severity.Warning);
    }

    [Fact]
    public async Task Tls_WithUntrustedChain_IsCritical()
    {
        FakeTlsProbe tls = new(new TlsHandshakeResult(TlsVersion.Tls12, new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), "CN=X", IsChainTrusted: false, IsNameMatch: true));
        IReadOnlyList<Finding> findings = await new TlsCheck(tls, At(2026, 3, 1)).RunAsync(Target(), TestContext.Current.CancellationToken);
        Assert.Contains(findings, f => f.Key == "web.tls.cert.chain" && f.Severity == Severity.Critical);
    }

    [Fact]
    public async Task SecurityHeaders_WhenUnreachable_IsCritical()
    {
        FakeHttpProbe http = new();
        IReadOnlyList<Finding> findings = await new SecurityHeadersCheck(http).RunAsync(Target(), TestContext.Current.CancellationToken);
        Finding finding = Assert.Single(findings);
        Assert.Equal(Severity.Critical, finding.Severity);
    }

    [Fact]
    public async Task SecurityHeaders_WithLongHsts_IsOk()
    {
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase) { ["Strict-Transport-Security"] = "max-age=63072000; includeSubDomains" };
        FakeHttpProbe http = new();
        http.SetResponse("https://beispiel.de/", FakeHttpProbe.Response(200, headers: headers));
        IReadOnlyList<Finding> findings = await new SecurityHeadersCheck(http).RunAsync(Target(), TestContext.Current.CancellationToken);
        Assert.Contains(findings, f => f.Key == "web.headers.hsts.detail" && f.Severity == Severity.Ok);
    }

    [Fact]
    public async Task VersionLeak_WhenUnreachable_IsEmpty()
    {
        FakeHttpProbe http = new();
        IReadOnlyList<Finding> findings = await new VersionLeakCheck(http).RunAsync(Target(), TestContext.Current.CancellationToken);
        Assert.Empty(findings);
    }

    [Fact]
    public async Task VersionLeak_WithPoweredByVersion_IsWarning()
    {
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase) { ["X-Powered-By"] = "PHP/8.5.1" };
        FakeHttpProbe http = new();
        http.SetResponse("https://beispiel.de/", FakeHttpProbe.Response(200, headers: headers));
        IReadOnlyList<Finding> findings = await new VersionLeakCheck(http).RunAsync(Target(), TestContext.Current.CancellationToken);
        Finding finding = Assert.Single(findings);
        Assert.Equal(Severity.Warning, finding.Severity);
    }

    [Fact]
    public async Task Cookie_WithSameSiteNone_IsInfo()
    {
        FakeHttpProbe http = new();
        http.SetResponse("https://beispiel.de/", FakeHttpProbe.Response(200, setCookies: ["ID=1; Secure; HttpOnly; SameSite=None"]));
        IReadOnlyList<Finding> findings = await new CookieCheck(http).RunAsync(Target(), TestContext.Current.CancellationToken);
        Finding finding = Assert.Single(findings);
        Assert.Equal(Severity.Info, finding.Severity);
    }

    [Fact]
    public async Task Cookie_WhenHomeUnreachable_IsSkippedNotOk()
    {
        // Ohne Antwort darf nichts als „in Ordnung“ gelten.
        FakeHttpProbe http = new();
        IReadOnlyList<Finding> findings = await new CookieCheck(http).RunAsync(Target(), TestContext.Current.CancellationToken);
        Finding finding = Assert.Single(findings);
        Assert.Equal("web.cookies.skipped", finding.Key);
        Assert.NotEqual(Severity.Ok, finding.Severity);
    }

    [Fact]
    public async Task Cookie_WhenReachableWithoutCookie_IsOk()
    {
        FakeHttpProbe http = new();
        http.SetResponse("https://beispiel.de/", FakeHttpProbe.Response(200));
        IReadOnlyList<Finding> findings = await new CookieCheck(http).RunAsync(Target(), TestContext.Current.CancellationToken);
        Assert.Equal("web.cookies.none", Assert.Single(findings).Key);
    }

    [Fact]
    public async Task DirectoryListing_WhenHomeUnreachable_IsSkipped()
    {
        FakeHttpProbe http = new();
        IReadOnlyList<Finding> findings = await new DirectoryListingCheck(http).RunAsync(Target(), TestContext.Current.CancellationToken);
        Assert.Equal("web.listing.skipped", Assert.Single(findings).Key);
    }

    [Fact]
    public async Task SecurityTxt_WhenHomeUnreachable_IsSkipped()
    {
        FakeHttpProbe http = new();
        IReadOnlyList<Finding> findings = await new SecurityTxtCheck(http).RunAsync(Target(), TestContext.Current.CancellationToken);
        Assert.Equal("web.securitytxt.skipped", Assert.Single(findings).Key);
    }

    [Fact]
    public async Task DnsHardening_ForIpTarget_IsEmpty()
    {
        IReadOnlyList<Finding> findings = await new DnsHardeningCheck(new FakeDnsProbe()).RunAsync(Target("https://203.0.113.5"), TestContext.Current.CancellationToken);
        Assert.Empty(findings);
    }
}
