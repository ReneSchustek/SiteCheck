namespace SiteCheck.Core.Abstractions;

/// <summary>
/// Fragt DNS-Einträge ab, die für die Mail-Einrichtung zählen: MX, TXT (für SPF, DKIM, DMARC,
/// MTA-STS, TLS-RPT, BIMI), die Auflösung eines Hosts zu IP-Adressen und die Rückwärtsauflösung
/// (PTR) einer IP. Nur Abfragen, kein Versand.
/// </summary>
public interface IDnsProbe
{
    /// <summary>Liefert die Austauscher aus den MX-Einträgen der Domain, nach Priorität sortiert.</summary>
    /// <param name="domain">Die abzufragende Domain.</param>
    /// <param name="cancellationToken">Bricht die Abfrage ab.</param>
    /// <returns>Die Mailserver-Namen; leer, wenn kein MX-Eintrag besteht.</returns>
    Task<IReadOnlyList<string>> QueryMailExchangersAsync(string domain, CancellationToken cancellationToken);

    /// <summary>Liefert die TXT-Einträge eines Namens, jede Zeichenkette für sich.</summary>
    /// <param name="name">Der abzufragende Name, etwa die Domain selbst oder <c>_dmarc.&lt;domain&gt;</c>.</param>
    /// <param name="cancellationToken">Bricht die Abfrage ab.</param>
    /// <returns>Die TXT-Werte; leer, wenn keine bestehen.</returns>
    Task<IReadOnlyList<string>> QueryTextRecordsAsync(string name, CancellationToken cancellationToken);

    /// <summary>Löst einen Host zu seinen IPv4-Adressen auf.</summary>
    /// <param name="host">Der aufzulösende Rechnername oder ein IPv4-Literal.</param>
    /// <param name="cancellationToken">Bricht die Abfrage ab.</param>
    /// <returns>Die IPv4-Adressen; leer, wenn keine vorhanden oder nicht auflösbar.</returns>
    Task<IReadOnlyList<string>> ResolveIpv4Async(string host, CancellationToken cancellationToken);

    /// <summary>Liest den PTR-Namen (Rückwärtsauflösung) einer IP-Adresse.</summary>
    /// <param name="ipAddress">Die IP-Adresse.</param>
    /// <param name="cancellationToken">Bricht die Abfrage ab.</param>
    /// <returns>Der PTR-Name ohne Schlusspunkt, oder <see langword="null"/>, wenn keiner hinterlegt ist.</returns>
    Task<string?> QueryPointerAsync(string ipAddress, CancellationToken cancellationToken);

    /// <summary>Prüft, ob die Domain mit DNSSEC signiert ist (DNSKEY vorhanden).</summary>
    /// <param name="domain">Die Domain.</param>
    /// <param name="cancellationToken">Bricht die Abfrage ab.</param>
    /// <returns><see langword="true"/>, wenn die Zone signiert ist.</returns>
    Task<bool> IsDnssecSignedAsync(string domain, CancellationToken cancellationToken);

    /// <summary>Liest die CAA-Einträge der Domain (erlaubte Zertifizierungsstellen).</summary>
    /// <param name="domain">Die Domain.</param>
    /// <param name="cancellationToken">Bricht die Abfrage ab.</param>
    /// <returns>Die CAA-Werte in Textform; leer, wenn keine bestehen.</returns>
    Task<IReadOnlyList<string>> QueryCaaAsync(string domain, CancellationToken cancellationToken);

    /// <summary>Prüft, ob für einen Namen TLSA-Einträge (DANE) vorhanden sind.</summary>
    /// <param name="name">Der TLSA-Name, etwa <c>_25._tcp.&lt;mx&gt;</c>.</param>
    /// <param name="cancellationToken">Bricht die Abfrage ab.</param>
    /// <returns><see langword="true"/>, wenn TLSA-Einträge bestehen.</returns>
    Task<bool> HasTlsaAsync(string name, CancellationToken cancellationToken);
}
