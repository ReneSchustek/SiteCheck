using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Probes;

namespace SiteCheck.Core.Checks;

/// <summary>
/// Prüft die Schutz-Kopfzeilen. Fünf gehören zur Grundausstattung (fehlend = zu verbessern);
/// drei weitere sind Reifegrad-Zeichen (fehlend = Hinweis). Jede Kopfzeile ergibt einen eigenen
/// Befund mit stabiler Kennung, damit der Verlaufsvergleich einzelne als behoben oder neu
/// markieren kann. HSTS wird zusätzlich im Detail gelesen.
/// </summary>
public sealed class SecurityHeadersCheck(IHttpProbe http) : ICheck
{
    // Grundausstattung: fehlend ist ein Mangel (Warnung).
    private static readonly SecurityHeader[] BaselineHeaders =
    [
        new("Content-Security-Policy",
            "begrenzt, welche Skripte und Quellen laden dürfen (Schutz gegen eingeschleuste Skripte)",
            "Mit einer eng gefassten Richtlinie beginnen und schrittweise öffnen.",
            "Content-Security-Policy: default-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'self'"),
        new("Strict-Transport-Security",
            "erzwingt https auch beim nächsten Besuch (HSTS)",
            "HSTS erst setzen, wenn https dauerhaft steht; der Browser merkt es sich lange.",
            "Strict-Transport-Security: max-age=31536000; includeSubDomains"),
        new("X-Frame-Options",
            "verhindert das Einbetten der Seite in fremde Rahmen (Clickjacking)",
            "Auf SAMEORIGIN setzen, wenn die Seite nicht fremd eingebettet werden soll.",
            "X-Frame-Options: SAMEORIGIN"),
        new("X-Content-Type-Options",
            "verbietet dem Browser das Raten von Dateitypen (nosniff)",
            "Ohne Ausnahme auf nosniff setzen.",
            "X-Content-Type-Options: nosniff"),
        new("Referrer-Policy",
            "steuert, wie viel Herkunft an fremde Seiten weitergegeben wird",
            "Eine zurückhaltende Richtlinie wählen.",
            "Referrer-Policy: strict-origin-when-cross-origin"),
    ];

    // Reifegrad: fehlend ist kein Mangel, nur ein Hinweis für fortgeschrittene Absicherung.
    private static readonly SecurityHeader[] MaturityHeaders =
    [
        new("Permissions-Policy",
            "schaltet Browser-Funktionen ab, die die Seite nicht braucht (Kamera, Mikrofon, Ortung)",
            "Nicht genutzte Funktionen leeren.",
            "Permissions-Policy: camera=(), microphone=(), geolocation=()"),
        new("Cross-Origin-Opener-Policy",
            "trennt das Browser-Fenster von fremden Seiten ab",
            "Auf same-origin setzen, wenn keine fremde Einbettung nötig ist.",
            "Cross-Origin-Opener-Policy: same-origin"),
        new("Cross-Origin-Resource-Policy",
            "begrenzt, wer die Ressourcen der Seite einbinden darf",
            "Auf same-origin oder same-site setzen.",
            "Cross-Origin-Resource-Policy: same-origin"),
    ];

    /// <inheritdoc/>
    public string Id => "web.headers";

    /// <inheritdoc/>
    public CheckCategory Category => CheckCategory.Web;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Finding>> RunAsync(CheckTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        HttpProbeResponse? response = await http.FetchAsync(target.Address, cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            return
            [
                new Finding(
                    Id + ".unreachable",
                    Category,
                    Severity.Critical,
                    "Seite nicht erreichbar",
                    $"{target.Address} hat auf eine gewöhnliche Anfrage nicht geantwortet."),
            ];
        }

        List<Finding> findings = [];
        foreach (SecurityHeader header in BaselineHeaders)
        {
            findings.Add(Evaluate(response, header, Severity.Warning));
        }

        // HSTS im Detail, sobald vorhanden.
        if (response.GetHeader("Strict-Transport-Security") is { } hsts)
        {
            findings.Add(DescribeHsts(hsts));
        }

        foreach (SecurityHeader header in MaturityHeaders)
        {
            findings.Add(Evaluate(response, header, Severity.Info));
        }

        return findings;
    }

    private Finding Evaluate(HttpProbeResponse response, SecurityHeader header, Severity missingSeverity)
    {
        bool present = response.GetHeader(header.Name) is not null;
        return present
            ? new Finding($"{Id}.{header.Name}", Category, Severity.Ok, $"Schutz-Kopfzeile gesetzt: {header.Name}")
            : new Finding(
                $"{Id}.missing.{header.Name}",
                Category,
                missingSeverity,
                $"Schutz-Kopfzeile fehlt: {header.Name}",
                header.Purpose,
                new Recommendation(header.Advice, header.Example));
    }

    private Finding DescribeHsts(string value)
    {
        // max-age herauslesen; darunter ein Jahr gilt HSTS als zu kurz, um verlässlich zu wirken.
        long maxAge = 0;
        foreach (string part in value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("max-age=", StringComparison.OrdinalIgnoreCase)
                && long.TryParse(part["max-age=".Length..], out long parsed))
            {
                maxAge = parsed;
            }
        }

        bool includeSubDomains = value.Contains("includeSubDomains", StringComparison.OrdinalIgnoreCase);
        const long OneYearSeconds = 31_536_000;

        if (maxAge < OneYearSeconds)
        {
            return new Finding(
                $"{Id}.hsts.maxage",
                Category,
                Severity.Info,
                $"HSTS ist kurz gesetzt (max-age={maxAge})",
                "Unter einem Jahr wirkt HSTS nur schwach; ein neuer Besucher ist kaum geschützt.",
                new Recommendation("max-age auf mindestens ein Jahr setzen und includeSubDomains ergänzen.",
                    "Strict-Transport-Security: max-age=31536000; includeSubDomains"));
        }

        return new Finding(
            $"{Id}.hsts.detail",
            Category,
            Severity.Ok,
            includeSubDomains
                ? "HSTS ausreichend lang und inklusive Subdomains"
                : "HSTS ausreichend lang (ohne includeSubDomains)");
    }

    private sealed record SecurityHeader(string Name, string Purpose, string Advice, string Example);
}
