using SiteCheck.Core.Models;

namespace SiteCheck.Core.Tests.Models;

public sealed class CheckTargetTests
{
    [Theory]
    [InlineData("beispiel.de", "https://beispiel.de/")]
    [InlineData("http://beispiel.de", "https://beispiel.de/")]
    [InlineData("https://www.beispiel.de/start", "https://www.beispiel.de/start")]
    [InlineData("  beispiel.de  ", "https://beispiel.de/")]
    public void TryParse_WithValidInput_ProducesHttpsTarget(string input, string expected)
    {
        bool parsed = CheckTarget.TryParse(input, out CheckTarget? target, out TargetParseError error);

        Assert.True(parsed);
        Assert.Equal(TargetParseError.None, error);
        Assert.Equal(expected, target!.ToString());
    }

    [Theory]
    [InlineData("", TargetParseError.Empty)]
    [InlineData("   ", TargetParseError.Empty)]
    [InlineData("intranet", TargetParseError.InvalidAddress)]
    [InlineData("ftp://beispiel.de", TargetParseError.UnsupportedScheme)]
    [InlineData("https://name:geheim@beispiel.de", TargetParseError.ContainsCredentials)]
    public void TryParse_WithInvalidInput_ReportsReason(string input, TargetParseError expected)
    {
        bool parsed = CheckTarget.TryParse(input, out CheckTarget? target, out TargetParseError error);

        Assert.False(parsed);
        Assert.Null(target);
        Assert.Equal(expected, error);
    }

    [Fact]
    public void MailDomain_ForSubdomain_StripsToRegistrableDomain()
    {
        Assert.True(CheckTarget.TryParse("music.vreka-music.com", out CheckTarget? target, out _));

        Assert.Equal("vreka-music.com", target!.MailDomain);
        Assert.True(target.IsSubdomain);
    }

    [Fact]
    public void MailDomain_ForApexDomain_IsNotSubdomain()
    {
        Assert.True(CheckTarget.TryParse("vreka-music.com", out CheckTarget? target, out _));

        Assert.Equal("vreka-music.com", target!.MailDomain);
        Assert.False(target.IsSubdomain);
    }

    [Fact]
    public void IpAddress_HasNoMailDomain()
    {
        Assert.True(CheckTarget.TryParse("https://203.0.113.5", out CheckTarget? target, out _));

        Assert.True(target!.IsIpAddress);
        Assert.Equal(string.Empty, target.MailDomain);
    }
}
