using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;

namespace SiteCheck.Core.Checks;

/// <summary>
/// Härtung auf DNS-Ebene: Ist die Domain mit DNSSEC signiert, und gibt es einen CAA-Eintrag,
/// der festlegt, welche Zertifizierungsstellen für die Domain ausstellen dürfen? Beides ist
/// direkt im DNS messbar.
/// </summary>
public sealed class DnsHardeningCheck(IDnsProbe dns) : ICheck
{
    private readonly IDnsProbe _dns = dns;

    /// <inheritdoc/>
    public string Id => "web.dns";

    /// <inheritdoc/>
    public CheckCategory Category => CheckCategory.Web;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Finding>> RunAsync(CheckTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (target.IsIpAddress)
        {
            return [];
        }

        List<Finding> findings =
        [
            await CheckDnssecAsync(target.MailDomain, cancellationToken).ConfigureAwait(false),
            await CheckCaaAsync(target, cancellationToken).ConfigureAwait(false),
        ];
        return findings;
    }

    private async Task<Finding> CheckDnssecAsync(string domain, CancellationToken cancellationToken)
    {
        bool signed = await _dns.IsDnssecSignedAsync(domain, cancellationToken).ConfigureAwait(false);
        return signed
            ? new Finding($"{Id}.dnssec", Category, Severity.Ok, $"DNSSEC ist aktiv ({domain})")
            : new Finding(
                $"{Id}.dnssec.absent",
                Category,
                Severity.Info,
                "DNSSEC ist nicht aktiv",
                "DNSSEC signiert die DNS-Antworten und schützt so vor untergeschobenen Adressen. Kein Muss, "
                + "aber ein Sicherheitsgewinn.",
                new Recommendation("DNSSEC beim DNS-Anbieter der Domain einschalten."));
    }

    private async Task<Finding> CheckCaaAsync(CheckTarget target, CancellationToken cancellationToken)
    {
        // CAA wird oft am registrierbaren Namen gesetzt; dort nachsehen, nicht an der Subdomain.
        IReadOnlyList<string> caa = await _dns.QueryCaaAsync(target.MailDomain, cancellationToken).ConfigureAwait(false);
        return caa.Count > 0
            ? new Finding($"{Id}.caa", Category, Severity.Ok, $"CAA-Eintrag vorhanden: {string.Join(", ", caa)}")
            : new Finding(
                $"{Id}.caa.absent",
                Category,
                Severity.Info,
                "Kein CAA-Eintrag",
                "Ein CAA-Eintrag legt fest, welche Zertifizierungsstellen für die Domain ausstellen dürfen, und "
                + "verhindert so Zertifikate von fremder Stelle. Kein Muss.",
                new Recommendation("Einen CAA-Eintrag für die genutzte Zertifizierungsstelle setzen.", "beispiel.de. IN CAA 0 issue \"letsencrypt.org\""));
    }
}
