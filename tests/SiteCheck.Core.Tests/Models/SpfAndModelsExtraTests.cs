using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Stress;

namespace SiteCheck.Core.Tests.Models;

public sealed class SpfAndModelsExtraTests
{
    [Fact]
    public void SpfRecord_Parse_ReadsMechanismsAndQualifier()
    {
        SpfRecord spf = SpfRecord.Parse("v=spf1 a mx ip4:203.0.113.9 include:_spf.anbieter.de a:mail.beispiel.de -all", "beispiel.de");

        Assert.Equal(SpfAllQualifier.Fail, spf.AllQualifier);
        Assert.Contains("_spf.anbieter.de", spf.IncludedDomains);
        Assert.Contains("203.0.113.9", spf.DirectIps);
        Assert.Contains("beispiel.de", spf.DirectHosts);         // aus bloßem a/mx
        Assert.Contains("mail.beispiel.de", spf.DirectHosts);    // aus a:host
        Assert.True(spf.DnsLookupMechanismCount >= 4);
    }

    [Fact]
    public void SpfRecord_Parse_KeepsSingleIpButDropsRanges()
    {
        // Ein Bereich (…/21) nennt keinen einzelnen Host und darf nicht als
        // Versand-IP für die Rückwärtsauflösung gelten; eine Einzel-IP (oder /32) schon.
        SpfRecord spf = SpfRecord.Parse("v=spf1 ip4:37.17.224.0/21 ip4:185.30.32.183 ip4:203.0.113.9/32 -all", "beispiel.de");

        Assert.DoesNotContain("37.17.224.0", spf.DirectIps);
        Assert.Contains("185.30.32.183", spf.DirectIps);
        Assert.Contains("203.0.113.9", spf.DirectIps);
    }

    [Theory]
    [InlineData("v=spf1 +all", SpfAllQualifier.Pass)]
    [InlineData("v=spf1 all", SpfAllQualifier.Pass)]
    [InlineData("v=spf1 ~all", SpfAllQualifier.SoftFail)]
    [InlineData("v=spf1 ?all", SpfAllQualifier.Neutral)]
    [InlineData("v=spf1 include:x.de", SpfAllQualifier.None)]
    public void SpfRecord_Parse_ClassifiesAllQualifier(string raw, SpfAllQualifier expected)
    {
        Assert.Equal(expected, SpfRecord.Parse(raw, "beispiel.de").AllQualifier);
    }

    [Fact]
    public void AppSettings_Default_IsEmptyAndUnacknowledged()
    {
        Assert.Null(AppSettings.Default.WindowWidth);
        Assert.False(AppSettings.Default.ScopeNoticeAcknowledged);
    }

    [Fact]
    public void StressTestOptions_Default_IsWithinBounds()
    {
        StressTestOptions clamped = StressTestOptions.Default.Clamp();
        Assert.Equal(StressTestOptions.Default, clamped);
    }

    [Fact]
    public void StressTestProgress_Fraction_IsClamped()
    {
        StressTestProgress progress = new(10, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(20));
        Assert.Equal(1, progress.Fraction);
    }

    [Fact]
    public void Finding_IsProblem_OnlyForWarningAndAbove()
    {
        Assert.False(new Finding("a", CheckCategory.Web, Severity.Info, "x").IsProblem);
        Assert.True(new Finding("a", CheckCategory.Web, Severity.Warning, "x").IsProblem);
    }

    [Fact]
    public void DkimKey_WithInvalidBase64_ReturnsNull()
    {
        Assert.Null(DkimKey.RsaKeySizeBits("v=DKIM1; k=rsa; p=nicht-base64!!"));
    }

    [Fact]
    public void DkimKey_WithEmptyKey_ReturnsNull()
    {
        Assert.Null(DkimKey.RsaKeySizeBits("v=DKIM1; k=rsa; p="));
    }
}
