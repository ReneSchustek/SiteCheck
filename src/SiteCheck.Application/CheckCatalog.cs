using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Checks;
using SiteCheck.Core.Localization;

namespace SiteCheck.Application;

/// <summary>
/// Stellt die Standard-Prüfungen in fester Reihenfolge zusammen: erst die Web-Prüfungen, dann
/// die Mail-Einrichtung. An einer Stelle, damit Konsole und Oberfläche denselben Satz in
/// derselben Ordnung ausführen.
/// </summary>
public static class CheckCatalog
{
    /// <summary>Baut die Standard-Prüfungen aus den Sonden.</summary>
    /// <param name="http">Sonde für HTTP-Abfragen.</param>
    /// <param name="tls">Sonde für den TLS-Handschlag.</param>
    /// <param name="dns">Sonde für DNS-Abfragen.</param>
    /// <param name="clock">Zeitquelle für die Zertifikatsprüfung.</param>
    /// <param name="options">Was der Lauf umfasst.</param>
    /// <returns>Die Prüfungen in Ausführungsreihenfolge.</returns>
    public static IReadOnlyList<ICheck> CreateDefault(IHttpProbe http, ITlsProbe tls, IDnsProbe dns, TimeProvider clock, CheckRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(tls);
        ArgumentNullException.ThrowIfNull(dns);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);

        ICheck sensitiveFiles = new SensitiveFilesCheck(http);
        ICheck directoryListing = new DirectoryListingCheck(http);

        return
        [
            new RedirectCheck(http),
            new TlsCheck(tls, clock),
            new SecurityHeadersCheck(http),
            new CookieCheck(http),
            new VersionLeakCheck(http),
            options.IncludeSensitivePaths ? sensitiveFiles : new OptInCheck(sensitiveFiles, DomainText.Get("Nicht geprüft: heikle Dateien (.git, .env und andere)")),
            options.IncludeSensitivePaths ? directoryListing : new OptInCheck(directoryListing, DomainText.Get("Nicht geprüft: offene Verzeichnis-Auflistung")),
            new SecurityTxtCheck(http),
            new DnsHardeningCheck(dns),
            new MailSetupCheck(dns),
            new MtaStsPolicyCheck(http),
        ];
    }
}
