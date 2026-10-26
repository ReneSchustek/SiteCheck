using System.Net;
using System.Net.Sockets;
using DnsClient;
using DnsClient.Protocol;
using SiteCheck.Core.Abstractions;

namespace SiteCheck.Data.Probes;

/// <summary>
/// Fragt MX-, TXT-, A- und PTR-Einträge über einen DNS-Resolver ab. Nur Abfragen; es wird
/// nichts im DNS verändert und keine Mail verschickt.
/// </summary>
public sealed class DnsProbe : IDnsProbe
{
    private readonly ILookupClient _dns;

    /// <summary>Erstellt die Sonde mit dem Standard-Resolver des Systems.</summary>
    public DnsProbe()
        : this(new LookupClient())
    {
    }

    /// <summary>Erstellt die Sonde mit einem vorgegebenen Resolver (für Tests).</summary>
    /// <param name="dns">Der zu verwendende Resolver.</param>
    public DnsProbe(ILookupClient dns)
    {
        _dns = dns;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> QueryMailExchangersAsync(string domain, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        IDnsQueryResponse response = await _dns.QueryAsync(domain, QueryType.MX, cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.Answers
            .MxRecords()
            .OrderBy(record => record.Preference)
            .Select(record => record.Exchange.Value.TrimEnd('.'))
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> QueryTextRecordsAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        IDnsQueryResponse response = await _dns.QueryAsync(name, QueryType.TXT, cancellationToken: cancellationToken).ConfigureAwait(false);

        // Ein TXT-Eintrag kann in mehrere Zeichenketten zerfallen (255-Byte-Grenze); ein langer
        // SPF- oder DKIM-Wert gehört zusammengesetzt, sonst findet die Prüfung "v=spf1" nicht wieder.
        return response.Answers
            .TxtRecords()
            .Select(record => string.Concat(record.Text))
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ResolveIpv4Async(string host, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        if (IPAddress.TryParse(host, out IPAddress? literal))
        {
            return literal.AddressFamily == AddressFamily.InterNetwork ? [literal.ToString()] : [];
        }

        try
        {
            IPAddress[] addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
            return addresses
                .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
                .Select(address => address.ToString())
                .ToArray();
        }
        catch (SocketException)
        {
            // Name nicht auflösbar: keine Adressen.
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<string?> QueryPointerAsync(string ipAddress, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ipAddress);

        if (!IPAddress.TryParse(ipAddress, out IPAddress? ip))
        {
            return null;
        }

        IDnsQueryResponse response = await _dns.QueryReverseAsync(ip, cancellationToken).ConfigureAwait(false);
        return response.Answers.PtrRecords().FirstOrDefault()?.PtrDomainName.Value.TrimEnd('.');
    }

    /// <inheritdoc/>
    public async Task<bool> IsDnssecSignedAsync(string domain, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        // Eine signierte Zone veröffentlicht ihre öffentlichen Schlüssel als DNSKEY. Deren
        // Vorhandensein ist das verlässlichste Zeichen für DNSSEC, das sich mit einer einzelnen
        // Abfrage feststellen lässt.
        IDnsQueryResponse response = await _dns.QueryAsync(domain, QueryType.DNSKEY, cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.Answers.Any(record => record.RecordType == ResourceRecordType.DNSKEY);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> QueryCaaAsync(string domain, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        IDnsQueryResponse response = await _dns.QueryAsync(domain, QueryType.CAA, cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.Answers
            .CaaRecords()
            .Select(record => $"{record.Tag} {record.Value}")
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<bool> HasTlsaAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        IDnsQueryResponse response = await _dns.QueryAsync(name, QueryType.TLSA, cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.Answers.Any(record => record.RecordType == ResourceRecordType.TLSA);
    }
}
