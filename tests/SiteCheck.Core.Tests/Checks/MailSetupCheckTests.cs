using SiteCheck.Core.Checks;
using SiteCheck.Core.Models;
using SiteCheck.Core.Tests.Fakes;

namespace SiteCheck.Core.Tests.Checks;

public sealed class MailSetupCheckTests
{
    private static CheckTarget Target(string input)
    {
        Assert.True(CheckTarget.TryParse(input, out CheckTarget? target, out _));
        return target!;
    }

    [Fact]
    public async Task MissingMx_IsWarningNotCritical()
    {
        // Fehlender MX ist nach echter Erfahrung ein Warnung-wert Mangel (häufiger
        // Grund für ausbleibende Mails), aber von außen nicht als Ursache beweisbar → nie „Kritisch“.
        FakeDnsProbe dns = new FakeDnsProbe()
            .SetTxt("example.com", "v=spf1 include:secureserver.net -all");
        MailSetupCheck check = new(dns);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target("https://music.example.com"), TestContext.Current.CancellationToken);

        Finding mx = findings.Single(finding => finding.Key == "mail.setup.mx.missing");
        Assert.Equal(Severity.Warning, mx.Severity);
    }

    [Fact]
    public async Task ReverseLookup_UsesSpfSenderHosts_NotWebHost()
    {
        // Relay-Fall: SPF nennt per include einen Anbieter und per ip4 eine Versand-IP. Geprüft
        // wird die Rückwärtsauflösung dieser IP, nicht die der Web-Adresse.
        FakeDnsProbe dns = new FakeDnsProbe()
            .SetMx("beispiel.de", "mx.beispiel.de")
            .SetTxt("beispiel.de", "v=spf1 include:_spf.anbieter.de ip4:203.0.113.9 -all")
            .SetPointer("203.0.113.9", "mail.beispiel.de")
            .SetTxt("default._domainkey.beispiel.de", "v=DKIM1; p=KEY")
            .SetTxt("_dmarc.beispiel.de", "v=DMARC1; p=reject");
        MailSetupCheck check = new(dns);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target("https://www.beispiel.de"), TestContext.Current.CancellationToken);

        Assert.Contains(findings, finding => finding.Key == "mail.setup.ptr.relay");
        Assert.Contains(findings, finding => finding.Key == "mail.setup.ptr.203.0.113.9" && finding.Severity == Severity.Ok);
    }

    [Fact]
    public async Task ReverseMismatch_IsInferredAndNotCritical()
    {
        FakeDnsProbe dns = new FakeDnsProbe()
            .SetTxt("beispiel.de", "v=spf1 ip4:92.205.227.37 -all")
            .SetPointer("92.205.227.37", "host.secureserver.net");
        MailSetupCheck check = new(dns);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target("https://beispiel.de"), TestContext.Current.CancellationToken);

        Finding mismatch = findings.Single(finding => finding.Key == "mail.setup.ptr.mismatch.92.205.227.37");
        Assert.Equal(Severity.Info, mismatch.Severity);
        Assert.Equal(Confidence.Inferred, mismatch.Confidence);
    }

    [Fact]
    public async Task OpenSpf_IsCritical()
    {
        FakeDnsProbe dns = new FakeDnsProbe()
            .SetMx("beispiel.de", "mx.beispiel.de")
            .SetTxt("beispiel.de", "v=spf1 +all");
        MailSetupCheck check = new(dns);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target("https://beispiel.de"), TestContext.Current.CancellationToken);

        Assert.Contains(findings, finding => finding.Key == "mail.setup.spf.open" && finding.Severity == Severity.Critical);
    }

    [Fact]
    public async Task MissingDkim_IsInferred()
    {
        FakeDnsProbe dns = new FakeDnsProbe()
            .SetTxt("beispiel.de", "v=spf1 -all");
        MailSetupCheck check = new(dns);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target("https://beispiel.de"), TestContext.Current.CancellationToken);

        Finding dkim = findings.Single(finding => finding.Key == "mail.setup.dkim.missing");
        Assert.Equal(Confidence.Inferred, dkim.Confidence);
    }

    [Fact]
    public async Task MaturityRecords_RecognizedWhenPresent()
    {
        FakeDnsProbe dns = new FakeDnsProbe()
            .SetTxt("beispiel.de", "v=spf1 -all")
            .SetTxt("_mta-sts.beispiel.de", "v=STSv1; id=20260101")
            .SetTxt("_smtp._tls.beispiel.de", "v=TLSRPTv1; rua=mailto:tls@beispiel.de")
            .SetTxt("default._bimi.beispiel.de", "v=BIMI1; l=https://beispiel.de/logo.svg");
        MailSetupCheck check = new(dns);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target("https://beispiel.de"), TestContext.Current.CancellationToken);

        Assert.Contains(findings, finding => finding.Key == "mail.setup.mtasts.ok");
        Assert.Contains(findings, finding => finding.Key == "mail.setup.tlsrpt.ok");
        Assert.Contains(findings, finding => finding.Key == "mail.setup.bimi.ok");
    }

    [Fact]
    public async Task Report_NamesGmailAndIcloudCrossCheck()
    {
        FakeDnsProbe dns = new FakeDnsProbe().SetTxt("beispiel.de", "v=spf1 -all");
        MailSetupCheck check = new(dns);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target("https://beispiel.de"), TestContext.Current.CancellationToken);

        Finding verify = findings.Single(finding => finding.Key == "mail.setup.verify");
        Assert.Contains("iCloud", verify.Detail, StringComparison.Ordinal);
        Assert.Contains("Gmail", verify.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoInferredFinding_ClaimsCauseAssertively()
    {
        // Vermutungen dürfen keine Ursache behaupten.
        FakeDnsProbe dns = new FakeDnsProbe()
            .SetTxt("beispiel.de", "v=spf1 ip4:92.205.227.37 -all")
            .SetPointer("92.205.227.37", "host.fremd.net");
        MailSetupCheck check = new(dns);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target("https://music.beispiel.de"), TestContext.Current.CancellationToken);

        foreach (Finding finding in findings.Where(finding => finding.Confidence == Confidence.Inferred))
        {
            string text = finding.Title + " " + (finding.Detail ?? string.Empty);
            Assert.DoesNotContain("ist die Ursache", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("behoben", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Dmarc_WithoutRua_EmitsHint()
    {
        FakeDnsProbe dns = new FakeDnsProbe()
            .SetTxt("beispiel.de", "v=spf1 -all")
            .SetTxt("_dmarc.beispiel.de", "v=DMARC1; p=reject");
        MailSetupCheck check = new(dns);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target("https://beispiel.de"), TestContext.Current.CancellationToken);

        Assert.Contains(findings, finding => finding.Key == "mail.setup.dmarc.norua");
    }

    [Fact]
    public async Task Dmarc_WithPartialPct_EmitsHint()
    {
        FakeDnsProbe dns = new FakeDnsProbe()
            .SetTxt("beispiel.de", "v=spf1 -all")
            .SetTxt("_dmarc.beispiel.de", "v=DMARC1; p=reject; rua=mailto:d@beispiel.de; pct=50");
        MailSetupCheck check = new(dns);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target("https://beispiel.de"), TestContext.Current.CancellationToken);

        Assert.Contains(findings, finding => finding.Key == "mail.setup.dmarc.pct");
    }

    [Theory]
    [InlineData(2048, Severity.Ok)]
    [InlineData(1024, Severity.Info)]
    public async Task DkimKeyStrength_IsClassified(int keySizeBits, Severity expected)
    {
        using System.Security.Cryptography.RSA rsa = System.Security.Cryptography.RSA.Create(keySizeBits);
        string publicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
        FakeDnsProbe dns = new FakeDnsProbe()
            .SetTxt("beispiel.de", "v=spf1 -all")
            .SetTxt("default._domainkey.beispiel.de", $"v=DKIM1; k=rsa; p={publicKey}");
        MailSetupCheck check = new(dns);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target("https://beispiel.de"), TestContext.Current.CancellationToken);

        Finding key = findings.Single(finding => finding.Key == "mail.setup.dkim.key");
        Assert.Equal(expected, key.Severity);
    }

    [Fact]
    public async Task Dane_WithDnssecAndTlsa_IsOk()
    {
        FakeDnsProbe dns = new FakeDnsProbe()
            .SetMx("beispiel.de", "mx.beispiel.de")
            .SetTxt("beispiel.de", "v=spf1 -all")
            .SetDnssec("beispiel.de")
            .SetTlsa("_25._tcp.mx.beispiel.de");
        MailSetupCheck check = new(dns);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target("https://beispiel.de"), TestContext.Current.CancellationToken);

        Assert.Contains(findings, finding => finding.Key == "mail.setup.dane" && finding.Severity == Severity.Ok);
    }

    [Fact]
    public async Task Dane_WithoutDnssec_IsSkipped()
    {
        FakeDnsProbe dns = new FakeDnsProbe()
            .SetMx("beispiel.de", "mx.beispiel.de")
            .SetTxt("beispiel.de", "v=spf1 -all");
        MailSetupCheck check = new(dns);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target("https://beispiel.de"), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(findings, finding => finding.Key.StartsWith("mail.setup.dane", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BulkSenderNote_IsPresent()
    {
        FakeDnsProbe dns = new FakeDnsProbe().SetTxt("beispiel.de", "v=spf1 -all");
        MailSetupCheck check = new(dns);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target("https://beispiel.de"), TestContext.Current.CancellationToken);

        Finding bulk = findings.Single(finding => finding.Key == "mail.setup.bulk");
        Assert.Contains("List-Unsubscribe", bulk.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NullMx_IsRecognizedNotReportedAsMissing()
    {
        // Der echte Resolver kürzt den Null-MX "." zu einem leeren Austauscher; so bildet der Fake es ab.
        FakeDnsProbe dns = new FakeDnsProbe()
            .SetMx("beispiel.de", string.Empty)
            .SetTxt("beispiel.de", "v=spf1 -all");
        MailSetupCheck check = new(dns);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target("https://beispiel.de"), TestContext.Current.CancellationToken);

        Assert.Contains(findings, finding => finding.Key == "mail.setup.mx.null" && finding.Severity == Severity.Info);
        Assert.DoesNotContain(findings, finding => finding.Key == "mail.setup.mx.missing");
    }

    [Fact]
    public async Task Spf_SoftFail_IsHint()
    {
        FakeDnsProbe dns = new FakeDnsProbe().SetTxt("beispiel.de", "v=spf1 include:x.de ~all");
        IReadOnlyList<Finding> findings = await new MailSetupCheck(dns).RunAsync(Target("https://beispiel.de"), TestContext.Current.CancellationToken);
        Assert.Contains(findings, f => f.Key == "mail.setup.spf.soft" && f.Severity == Severity.Info);
    }

    [Fact]
    public async Task Spf_WithoutAll_IsWarning()
    {
        FakeDnsProbe dns = new FakeDnsProbe().SetTxt("beispiel.de", "v=spf1 include:x.de");
        IReadOnlyList<Finding> findings = await new MailSetupCheck(dns).RunAsync(Target("https://beispiel.de"), TestContext.Current.CancellationToken);
        Assert.Contains(findings, f => f.Key == "mail.setup.spf.noall" && f.Severity == Severity.Warning);
    }

    [Fact]
    public async Task Spf_WithTooManyLookups_IsWarning()
    {
        string spf = "v=spf1 " + string.Join(' ', Enumerable.Range(0, 11).Select(i => $"include:p{i}.de")) + " -all";
        FakeDnsProbe dns = new FakeDnsProbe().SetTxt("beispiel.de", spf);
        IReadOnlyList<Finding> findings = await new MailSetupCheck(dns).RunAsync(Target("https://beispiel.de"), TestContext.Current.CancellationToken);
        Assert.Contains(findings, f => f.Key == "mail.setup.spf.lookups" && f.Severity == Severity.Warning);
    }

    [Fact]
    public async Task NoSpf_ReportsPtrNotDeterminable()
    {
        FakeDnsProbe dns = new FakeDnsProbe().SetMx("beispiel.de", "mx.beispiel.de");
        IReadOnlyList<Finding> findings = await new MailSetupCheck(dns).RunAsync(Target("https://beispiel.de"), TestContext.Current.CancellationToken);
        Assert.Contains(findings, f => f.Key == "mail.setup.ptr.unknown" && f.Confidence == Confidence.Inferred);
    }

    [Fact]
    public async Task SenderIp_WithoutPointer_IsInferredHint()
    {
        FakeDnsProbe dns = new FakeDnsProbe().SetTxt("beispiel.de", "v=spf1 ip4:203.0.113.9 -all");
        IReadOnlyList<Finding> findings = await new MailSetupCheck(dns).RunAsync(Target("https://beispiel.de"), TestContext.Current.CancellationToken);
        Assert.Contains(findings, f => f.Key == "mail.setup.ptr.missing.203.0.113.9" && f.Confidence == Confidence.Inferred);
    }

    [Fact]
    public async Task DkimTooShortKey_IsWarning()
    {
        using System.Security.Cryptography.RSA rsa = System.Security.Cryptography.RSA.Create(512);
        string publicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
        FakeDnsProbe dns = new FakeDnsProbe()
            .SetTxt("beispiel.de", "v=spf1 -all")
            .SetTxt("default._domainkey.beispiel.de", $"v=DKIM1; p={publicKey}");
        IReadOnlyList<Finding> findings = await new MailSetupCheck(dns).RunAsync(Target("https://beispiel.de"), TestContext.Current.CancellationToken);
        Assert.Contains(findings, f => f.Key == "mail.setup.dkim.key" && f.Severity == Severity.Warning);
    }

    [Fact]
    public async Task SpfRange_DoesNotProduceReverseFinding()
    {
        // Netzbereiche aus dem SPF bekommen keinen PTR-Befund.
        FakeDnsProbe dns = new FakeDnsProbe().SetTxt("beispiel.de", "v=spf1 ip4:37.17.224.0/21 -all");
        IReadOnlyList<Finding> findings = await new MailSetupCheck(dns).RunAsync(Target("https://beispiel.de"), TestContext.Current.CancellationToken);
        Assert.DoesNotContain(findings, f => f.Key.StartsWith("mail.setup.ptr.missing", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ForwardConfirmedReverse_IsNotFlagged()
    {
        // PTR-Name zeigt vorwärts auf dieselbe IP (FCrDNS) → sauber, keine Warnung.
        FakeDnsProbe dns = new FakeDnsProbe()
            .SetTxt("beispiel.de", "v=spf1 ip4:185.30.32.183 -all")
            .SetPointer("185.30.32.183", "s183.goserver.host")
            .SetIpv4("s183.goserver.host", "185.30.32.183");
        IReadOnlyList<Finding> findings = await new MailSetupCheck(dns).RunAsync(Target("https://beispiel.de"), TestContext.Current.CancellationToken);

        Finding ptr = findings.Single(f => f.Key.StartsWith("mail.setup.ptr.fcrdns", StringComparison.Ordinal));
        Assert.Equal(Severity.Ok, ptr.Severity);
        Assert.DoesNotContain(findings, f => f.Key.StartsWith("mail.setup.ptr.mismatch", StringComparison.Ordinal));
    }

    [Fact]
    public async Task IpAddress_SkipsMailChecks()
    {
        FakeDnsProbe dns = new();
        MailSetupCheck check = new(dns);

        IReadOnlyList<Finding> findings = await check.RunAsync(Target("https://203.0.113.5"), TestContext.Current.CancellationToken);

        Finding finding = Assert.Single(findings);
        Assert.Equal("mail.setup.noip", finding.Key);
    }
}
