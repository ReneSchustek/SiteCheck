using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Probes;

namespace SiteCheck.Core.Checks;

/// <summary>
/// Welche Verschlüsselung wird ausgehandelt, und wie lange gilt das Zertifikat? Alte
/// TLS-Fassungen und ein bald ablaufendes oder abgelaufenes Zertifikat sind Befunde.
/// </summary>
public sealed class TlsCheck(ITlsProbe tls, TimeProvider clock) : ICheck
{
    // Ab 14 Tagen Restlaufzeit wird es knapp: Let's-Encrypt erneuert planmäßig 30 Tage vorher,
    // zwei Wochen stilles Nichterneuern ist das Zeichen, dass der automatische Weg hakt.
    private const int RenewalWarningDays = 14;

    private readonly ITlsProbe _tls = tls;
    private readonly TimeProvider _clock = clock;

    /// <inheritdoc/>
    public string Id => "web.tls";

    /// <inheritdoc/>
    public CheckCategory Category => CheckCategory.Web;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Finding>> RunAsync(CheckTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        int port = target.Address.IsDefaultPort ? 443 : target.Address.Port;
        TlsHandshakeResult? result = await _tls.HandshakeAsync(target.Host, port, cancellationToken).ConfigureAwait(false);

        if (result is null)
        {
            return
            [
                new Finding(
                    Id + ".unreachable",
                    Category,
                    Severity.Critical,
                    "Keine verschlüsselte Verbindung möglich",
                    $"Der Rechner {target.Host} nimmt auf Port {port} keine TLS-Verbindung an. Lädt die Seite im "
                    + "Browser trotzdem, liegt es oft nicht am Server, sondern am eigenen Netz: ein Virenschutz, der "
                    + "HTTPS aufbricht, oder eine zeitweise IP-Sperre des Hosters. Dann von einem anderen Anschluss prüfen.",
                    new Recommendation("Prüfen, ob der Webserver auf Port 443 lauscht und ein Zertifikat eingerichtet ist — und ob der eigene Anschluss die Verbindung zulässt."))
                {
                    Confidence = Confidence.Inferred,
                },
            ];
        }

        List<Finding> findings = [CheckVersion(result)];
        findings.AddRange(CheckCertificate(result, target.Host));
        return findings;
    }

    private Finding CheckVersion(TlsHandshakeResult result) => result.Version switch
    {
        TlsVersion.Tls13 => new Finding(Id + ".version", Category, Severity.Ok, "Verschlüsselung: TLS 1.3"),
        TlsVersion.Tls12 => new Finding(Id + ".version", Category, Severity.Ok, "Verschlüsselung: TLS 1.2"),
        TlsVersion.Outdated => new Finding(
            Id + ".version",
            Category,
            Severity.Critical,
            "Veraltete Verschlüsselung ausgehandelt (TLS 1.0/1.1)",
            "Seit RFC 8996 (2021) gelten TLS 1.0 und 1.1 als unsicher.",
            new Recommendation("Im Webserver mindestens TLS 1.2 erzwingen, TLS 1.0/1.1 abschalten.")),
        _ => new Finding(
            Id + ".version",
            Category,
            Severity.Warning,
            "Verschlüsselung ließ sich nicht eindeutig bestimmen"),
    };

    private IEnumerable<Finding> CheckCertificate(TlsHandshakeResult result, string host)
    {
        if (!result.IsNameMatch)
        {
            yield return new Finding(
                Id + ".cert.name",
                Category,
                Severity.Critical,
                "Zertifikat gilt nicht für diese Adresse",
                $"Das vorgelegte Zertifikat ist nicht auf {host} ausgestellt. Browser zeigen hier eine Warnseite.",
                new Recommendation("Ein Zertifikat einrichten, das auf genau diesen Rechnernamen lautet."));
        }
        else if (!result.IsChainTrusted)
        {
            yield return new Finding(
                Id + ".cert.chain",
                Category,
                Severity.Critical,
                "Zertifikat ist nicht vertrauenswürdig",
                "Die Kette führt zu keiner anerkannten Stelle (selbst signiert oder unvollständig).",
                new Recommendation("Ein Zertifikat einer anerkannten Stelle einsetzen, etwa über Let's Encrypt, und die Zwischenzertifikate mit ausliefern."));
        }

        if (result.CertificateNotAfter is not DateTimeOffset notAfter)
        {
            yield break;
        }

        int daysLeft = (int)(notAfter - _clock.GetUtcNow()).TotalDays;
        string issuer = string.IsNullOrWhiteSpace(result.CertificateIssuer) ? "unbekannt" : result.CertificateIssuer;

        if (daysLeft < 0)
        {
            yield return new Finding(
                Id + ".cert.expiry",
                Category,
                Severity.Critical,
                $"Zertifikat ist abgelaufen (seit {notAfter:dd.MM.yyyy})",
                $"Aussteller: {issuer}",
                new Recommendation("Das Zertifikat sofort erneuern; die Seite gilt Browsern derzeit als unsicher."));
        }
        else if (daysLeft < RenewalWarningDays)
        {
            yield return new Finding(
                Id + ".cert.expiry",
                Category,
                Severity.Warning,
                $"Zertifikat läuft bald ab (in {daysLeft} Tagen, am {notAfter:dd.MM.yyyy})",
                $"Aussteller: {issuer}",
                new Recommendation("Prüfen, ob die automatische Erneuerung läuft; 14 Tage vor Ablauf sollte sie längst gegriffen haben."));
        }
        else
        {
            yield return new Finding(
                Id + ".cert.expiry",
                Category,
                Severity.Ok,
                $"Zertifikat gültig bis {notAfter:dd.MM.yyyy} (noch {daysLeft} Tage)",
                $"Aussteller: {issuer}");
        }
    }
}
