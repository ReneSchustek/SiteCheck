using System.Text.RegularExpressions;
using SiteCheck.Core.Models;

namespace SiteCheck.Application.Tests;

public sealed class ReportBuilderTests
{
    private static CheckReport SampleReport() => new(
        "https://beispiel.de/",
        new DateTimeOffset(2026, 10, 26, 8, 30, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 10, 26, 8, 30, 5, TimeSpan.Zero),
        [
            new Finding("web.tls", CheckCategory.Web, Severity.Ok, "Verschlüsselung: TLS 1.3"),
            new Finding(
                "mail.setup.mx.missing",
                CheckCategory.Mail,
                Severity.Critical,
                "Kein MX-Eintrag",
                "Ohne MX keine Mail.",
                new Recommendation("MX-Eintrag setzen.", "beispiel.de. IN MX 10 mail.beispiel.de.")),
        ]);

    [Fact]
    public void BuildText_ContainsAddressSummaryAndRecommendation()
    {
        string text = ReportBuilder.BuildText(SampleReport());

        Assert.Contains("https://beispiel.de/", text, StringComparison.Ordinal);
        Assert.Contains("1 kritisch", text, StringComparison.Ordinal);
        Assert.Contains("MX-Eintrag setzen.", text, StringComparison.Ordinal);
        Assert.Contains("WEB", text, StringComparison.Ordinal);
        Assert.Contains("MAIL-EINRICHTUNG", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildHtml_IsWellFormedAndEncodesContent()
    {
        string html = ReportBuilder.BuildHtml(SampleReport());

        Assert.StartsWith("<!DOCTYPE html>", html, StringComparison.Ordinal);
        Assert.Contains("prefers-color-scheme: dark", html, StringComparison.Ordinal);
        Assert.Contains("beispiel.de", html, StringComparison.Ordinal);
        Assert.Contains("class=\"finding critical\"", html, StringComparison.Ordinal);
        Assert.EndsWith("</html>" + Environment.NewLine, html, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildTextAndHtml_CoverAllSeveritiesInferredAndEmptyCategory()
    {
        CheckReport report = new(
            "https://beispiel.de/",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            [
                new Finding("w.ok", CheckCategory.Web, Severity.Ok, "ok"),
                new Finding("w.info", CheckCategory.Web, Severity.Info, "hinweis"),
                new Finding("w.warn", CheckCategory.Web, Severity.Warning, "warnung"),
                new Finding("w.crit", CheckCategory.Web, Severity.Critical, "kritisch"),
                new Finding("w.inf", CheckCategory.Web, Severity.Info, "vermutet") { Confidence = Confidence.Inferred },
            ]);

        string text = ReportBuilder.BuildText(report);
        string html = ReportBuilder.BuildHtml(report);

        // Alle Schweregrad-Wörter und das Vermutungs-Kennzeichen erscheinen …
        Assert.Contains("[Hinweis]", text, StringComparison.Ordinal);
        Assert.Contains("[!]", text, StringComparison.Ordinal);
        Assert.Contains("aus Sicht von außen", text, StringComparison.Ordinal);
        Assert.Contains("class=\"finding warning\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"finding info\"", html, StringComparison.Ordinal);
        Assert.Contains("aus Sicht von außen", html, StringComparison.Ordinal);
        // … und die leere Mail-Kategorie wird übersprungen (kein Abschnitt).
        Assert.DoesNotContain("MAIL-EINRICHTUNG", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildHtml_EscapesAngleBrackets()
    {
        CheckReport report = new(
            "https://beispiel.de/",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            [new Finding("x", CheckCategory.Web, Severity.Info, "<script>alert(1)</script>")]);

        string html = ReportBuilder.BuildHtml(report);

        Assert.DoesNotContain("<script>alert(1)</script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildHtml_EmbedsBothFontsAndUsesThemFirst()
    {
        string html = ReportBuilder.BuildHtml(SampleReport());

        Assert.Contains("@font-face { font-family: \"Roboto Flex\"", html, StringComparison.Ordinal);
        Assert.Contains("@font-face { font-family: \"Roboto Mono\"", html, StringComparison.Ordinal);
        Assert.Contains("body { font-family: \"Roboto Flex\",", html, StringComparison.Ordinal);
        Assert.Contains("font-family: \"Roboto Mono\", ui-monospace", html, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildHtml_LoadsNothingFromOtherHosts()
    {
        string html = ReportBuilder.BuildHtml(SampleReport());

        // Jede Quelle im Bericht ist eingebettet. Ein Verweis nach außen würde beim Öffnen die
        // IP-Adresse des Lesers an einen fremden Host geben.
        MatchCollection sources = Regex.Matches(html, @"url\((?<source>[^)]*)\)");
        Assert.Equal(2, sources.Count);
        Assert.All(sources, source => Assert.StartsWith("data:font/woff2;base64,", source.Groups["source"].Value, StringComparison.Ordinal));
        Assert.DoesNotContain("<link", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@import", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ReportFonts.FlexResourceName)]
    [InlineData(ReportFonts.MonoResourceName)]
    public void ReportFonts_AreEmbeddedAsWoff2(string resourceName)
    {
        byte[] font = ReportFonts.ReadResource(resourceName);

        Assert.True(font.Length > 1024, $"{resourceName} ist verdächtig klein ({font.Length} Bytes).");
        Assert.Equal("wOF2"u8.ToArray(), font[..4]);
    }

    [Fact]
    public void ReportFonts_MissingResource_FailsWithItsName()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => ReportFonts.ReadResource("SiteCheck.Fonts.fehlt.woff2"));

        Assert.Contains("SiteCheck.Fonts.fehlt.woff2", error.Message, StringComparison.Ordinal);
    }
}
