using Microsoft.Extensions.Time.Testing;
using SiteCheck.Core.Checks;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Probes;
using SiteCheck.Core.Tests.Fakes;

namespace SiteCheck.Core.Tests.Checks;

public sealed class WebChecksTests
{
    private static CheckTarget Target(string input = "https://beispiel.de/")
    {
        Assert.True(CheckTarget.TryParse(input, out CheckTarget? target, out _));
        return target!;
    }

    [Fact]
    public async Task Redirect_WithPermanentRedirectToHttps_IsOk()
    {
        FakeHttpProbe http = new();
        http.SetNonRedirectResponse(
            "http://beispiel.de/",
            FakeHttpProbe.Response(301, location: new Uri("https://beispiel.de/")));
        RedirectCheck check = new(http);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target(), TestContext.Current.CancellationToken);

        Finding finding = Assert.Single(findings);
        Assert.Equal(Severity.Ok, finding.Severity);
    }

    [Fact]
    public async Task Redirect_WithNoRedirect_IsWarningWithRecommendation()
    {
        FakeHttpProbe http = new();
        http.SetNonRedirectResponse("http://beispiel.de/", FakeHttpProbe.Response(200));
        RedirectCheck check = new(http);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target(), TestContext.Current.CancellationToken);

        Finding finding = Assert.Single(findings);
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.NotNull(finding.Recommendation);
    }

    [Fact]
    public async Task Tls_WithExpiredCertificate_IsCritical()
    {
        FakeTimeProvider clock = new(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
        FakeTlsProbe tls = new(new TlsHandshakeResult(
            TlsVersion.Tls13,
            new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
            "CN=Test",
            IsChainTrusted: true,
            IsNameMatch: true));
        TlsCheck check = new(tls, clock);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target(), TestContext.Current.CancellationToken);

        Assert.Contains(findings, finding => finding.Key == "web.tls.cert.expiry" && finding.Severity == Severity.Critical);
    }

    [Fact]
    public async Task Tls_WithValidCertificateAndTls13_IsOk()
    {
        FakeTimeProvider clock = new(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
        FakeTlsProbe tls = new(new TlsHandshakeResult(
            TlsVersion.Tls13,
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            "CN=Test",
            IsChainTrusted: true,
            IsNameMatch: true));
        TlsCheck check = new(tls, clock);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target(), TestContext.Current.CancellationToken);

        Assert.All(findings, finding => Assert.Equal(Severity.Ok, finding.Severity));
    }

    [Fact]
    public async Task Tls_WithNameMismatch_IsCritical()
    {
        FakeTimeProvider clock = new(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
        FakeTlsProbe tls = new(new TlsHandshakeResult(
            TlsVersion.Tls12,
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            "CN=Test",
            IsChainTrusted: false,
            IsNameMatch: false));
        TlsCheck check = new(tls, clock);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target(), TestContext.Current.CancellationToken);

        Assert.Contains(findings, finding => finding.Key == "web.tls.cert.name" && finding.Severity == Severity.Critical);
    }

    [Fact]
    public async Task SecurityHeaders_WhenAllPresent_AreAllOk()
    {
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Security-Policy"] = "default-src 'self'",
            ["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains",
            ["X-Frame-Options"] = "SAMEORIGIN",
            ["X-Content-Type-Options"] = "nosniff",
            ["Referrer-Policy"] = "strict-origin-when-cross-origin",
            ["Permissions-Policy"] = "camera=()",
            ["Cross-Origin-Opener-Policy"] = "same-origin",
            ["Cross-Origin-Resource-Policy"] = "same-origin",
        };
        FakeHttpProbe http = new();
        http.SetResponse("https://beispiel.de/", FakeHttpProbe.Response(200, headers: headers));
        SecurityHeadersCheck check = new(http);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target(), TestContext.Current.CancellationToken);

        Assert.All(findings, finding => Assert.Equal(Severity.Ok, finding.Severity));
    }

    [Fact]
    public async Task SecurityHeaders_WhenMissing_BaselineAreWarningsMaturityAreHints()
    {
        FakeHttpProbe http = new();
        http.SetResponse("https://beispiel.de/", FakeHttpProbe.Response(200));
        SecurityHeadersCheck check = new(http);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target(), TestContext.Current.CancellationToken);

        Assert.Equal(5, findings.Count(finding => finding.Severity == Severity.Warning));
        Assert.Equal(3, findings.Count(finding => finding.Severity == Severity.Info));
    }

    [Fact]
    public async Task SecurityHeaders_ShortHsts_IsHint()
    {
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Strict-Transport-Security"] = "max-age=3600",
        };
        FakeHttpProbe http = new();
        http.SetResponse("https://beispiel.de/", FakeHttpProbe.Response(200, headers: headers));
        SecurityHeadersCheck check = new(http);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target(), TestContext.Current.CancellationToken);

        Assert.Contains(findings, finding => finding.Key == "web.headers.hsts.maxage" && finding.Severity == Severity.Info);
    }

    [Fact]
    public async Task SecurityTxt_WhenPresent_IsOk()
    {
        FakeHttpProbe http = new();
        http.SetResponse("https://beispiel.de/", FakeHttpProbe.Response(200));
        http.SetResponse("https://beispiel.de/.well-known/security.txt", FakeHttpProbe.Response(200, body: "Contact: mailto:security@beispiel.de"));
        SecurityTxtCheck check = new(http);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target(), TestContext.Current.CancellationToken);

        Finding finding = Assert.Single(findings);
        Assert.Equal(Severity.Ok, finding.Severity);
    }

    [Fact]
    public async Task SecurityTxt_WhenMissing_IsHint()
    {
        FakeHttpProbe http = new();
        http.SetResponse("https://beispiel.de/", FakeHttpProbe.Response(200));
        SecurityTxtCheck check = new(http);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target(), TestContext.Current.CancellationToken);

        Finding finding = Assert.Single(findings);
        Assert.Equal(Severity.Info, finding.Severity);
    }

    [Fact]
    public async Task DirectoryListing_WhenOpen_IsWarning()
    {
        FakeHttpProbe http = new();
        http.SetResponse("https://beispiel.de/", FakeHttpProbe.Response(200));
        http.SetResponse("https://beispiel.de/uploads/", FakeHttpProbe.Response(200, body: "<html><head><title>Index of /uploads</title></head>"));
        DirectoryListingCheck check = new(http);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target(), TestContext.Current.CancellationToken);

        Finding finding = Assert.Single(findings);
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Contains("/uploads/", finding.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SensitiveFiles_WhenHomeUnreachable_IsSkipped()
    {
        // Keine Startseiten-Antwort hinterlegt → nicht erreichbar → Pfad-Anfragen überspringen.
        FakeHttpProbe http = new();
        SensitiveFilesCheck check = new(http);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target(), TestContext.Current.CancellationToken);

        Finding finding = Assert.Single(findings);
        Assert.Equal("web.files.skipped", finding.Key);
    }

    [Fact]
    public async Task DirectoryListing_WhenClosed_IsOk()
    {
        FakeHttpProbe http = new();
        http.SetResponse("https://beispiel.de/", FakeHttpProbe.Response(200));
        DirectoryListingCheck check = new(http);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target(), TestContext.Current.CancellationToken);

        Finding finding = Assert.Single(findings);
        Assert.Equal(Severity.Ok, finding.Severity);
    }

    [Fact]
    public async Task Cookie_WithoutSecureFlags_IsWarning()
    {
        FakeHttpProbe http = new();
        http.SetResponse("https://beispiel.de/", FakeHttpProbe.Response(200, setCookies: ["SESSION=abc; Path=/"]));
        CookieCheck check = new(http);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target(), TestContext.Current.CancellationToken);

        Finding finding = Assert.Single(findings);
        Assert.Equal(Severity.Warning, finding.Severity);
    }

    [Fact]
    public async Task VersionLeak_WhenServerRevealsVersion_IsWarning()
    {
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase) { ["Server"] = "nginx/1.24.0" };
        FakeHttpProbe http = new();
        http.SetResponse("https://beispiel.de/", FakeHttpProbe.Response(200, headers: headers));
        VersionLeakCheck check = new(http);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target(), TestContext.Current.CancellationToken);

        Finding finding = Assert.Single(findings);
        Assert.Equal(Severity.Warning, finding.Severity);
    }

    [Fact]
    public async Task VersionLeak_WhenServerNameWithoutDigits_IsOk()
    {
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase) { ["Server"] = "nginx" };
        FakeHttpProbe http = new();
        http.SetResponse("https://beispiel.de/", FakeHttpProbe.Response(200, headers: headers));
        VersionLeakCheck check = new(http);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target(), TestContext.Current.CancellationToken);

        Finding finding = Assert.Single(findings);
        Assert.Equal(Severity.Ok, finding.Severity);
    }

    [Fact]
    public async Task SensitiveFiles_WhenEnvExposed_IsCritical()
    {
        FakeHttpProbe http = new();
        http.SetResponse("https://beispiel.de/", FakeHttpProbe.Response(200));
        http.SetResponse("https://beispiel.de/.env", FakeHttpProbe.Response(200, body: "APP_KEY=secret\nDB_PASSWORD=123"));
        SensitiveFilesCheck check = new(http);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target(), TestContext.Current.CancellationToken);

        Finding finding = Assert.Single(findings);
        Assert.Equal(Severity.Critical, finding.Severity);
        Assert.Contains(".env", finding.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SensitiveFiles_WhenSoftNotFoundReturnsHtml_IsNotFlagged()
    {
        // Weiche 404: Status 200, aber HTML-Fehlerseite ohne die erwartete Signatur.
        FakeHttpProbe http = new();
        http.SetResponse("https://beispiel.de/", FakeHttpProbe.Response(200));
        http.SetResponse("https://beispiel.de/.env", FakeHttpProbe.Response(200, body: "<!DOCTYPE html><title>404</title>"));
        http.SetResponse("https://beispiel.de/config.php", FakeHttpProbe.Response(200, body: "<!DOCTYPE html>"));
        SensitiveFilesCheck check = new(http);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target(), TestContext.Current.CancellationToken);

        Finding finding = Assert.Single(findings);
        Assert.Equal(Severity.Ok, finding.Severity);
    }
}
