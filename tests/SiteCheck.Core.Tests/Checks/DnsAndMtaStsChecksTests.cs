using SiteCheck.Core.Checks;
using SiteCheck.Core.Models;
using SiteCheck.Core.Tests.Fakes;

namespace SiteCheck.Core.Tests.Checks;

public sealed class DnsAndMtaStsChecksTests
{
    private static CheckTarget Target(string input = "https://beispiel.de/")
    {
        Assert.True(CheckTarget.TryParse(input, out CheckTarget? target, out _));
        return target!;
    }

    [Fact]
    public async Task DnsHardening_WithDnssecAndCaa_IsOk()
    {
        FakeDnsProbe dns = new FakeDnsProbe()
            .SetDnssec("beispiel.de")
            .SetCaa("beispiel.de", "issue letsencrypt.org");
        DnsHardeningCheck check = new(dns);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target(), TestContext.Current.CancellationToken);

        Assert.Contains(findings, finding => finding.Key == "web.dns.dnssec" && finding.Severity == Severity.Ok);
        Assert.Contains(findings, finding => finding.Key == "web.dns.caa" && finding.Severity == Severity.Ok);
    }

    [Fact]
    public async Task DnsHardening_WhenAbsent_AreHints()
    {
        FakeDnsProbe dns = new();
        DnsHardeningCheck check = new(dns);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target(), TestContext.Current.CancellationToken);

        Assert.Equal(2, findings.Count);
        Assert.All(findings, finding => Assert.Equal(Severity.Info, finding.Severity));
    }

    [Fact]
    public async Task MtaStsPolicy_WithEnforce_IsOk()
    {
        FakeHttpProbe http = new();
        http.SetResponse("https://mta-sts.beispiel.de/.well-known/mta-sts.txt",
            FakeHttpProbe.Response(200, body: "version: STSv1\nmode: enforce\nmx: mx.beispiel.de\nmax_age: 604800"));
        MtaStsPolicyCheck check = new(http);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target(), TestContext.Current.CancellationToken);

        Finding finding = Assert.Single(findings);
        Assert.Equal(Severity.Ok, finding.Severity);
    }

    [Fact]
    public async Task MtaStsPolicy_WithTesting_IsHint()
    {
        FakeHttpProbe http = new();
        http.SetResponse("https://mta-sts.beispiel.de/.well-known/mta-sts.txt",
            FakeHttpProbe.Response(200, body: "version: STSv1\nmode: testing\nmx: mx.beispiel.de\nmax_age: 604800"));
        MtaStsPolicyCheck check = new(http);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target(), TestContext.Current.CancellationToken);

        Finding finding = Assert.Single(findings);
        Assert.Equal(Severity.Info, finding.Severity);
    }

    [Fact]
    public async Task MtaStsPolicy_WhenAbsent_StaysSilent()
    {
        FakeHttpProbe http = new();
        MtaStsPolicyCheck check = new(http);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target(), TestContext.Current.CancellationToken);

        Assert.Empty(findings);
    }
}
