using System.Security.Cryptography;
using SiteCheck.Core.Abstractions;

namespace SiteCheck.Data.Demo;

/// <summary>
/// DNS-Antworten der Vorführwelt aus einer festen Tabelle, ohne Resolver. Die Hauptseite hat
/// früher kein DMARC und heute nur <c>p=none</c>; der Shop ist mittel, der Blog vollständig.
/// </summary>
/// <param name="era">Welcher Stand der Vorführseiten gilt.</param>
public sealed class DemoDnsProbe(DemoEra era) : IDnsProbe
{
    // Ein echter, lesbarer Schlüssel, damit die Längenprüfung von DKIM greift. Er wird einmal je
    // Prozess erzeugt und nirgends abgelegt; zu ihm gibt es keinen genutzten privaten Teil.
    private static readonly Lazy<string> DkimPublicKey = new(CreateDkimPublicKey, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly DemoEra _era = era;

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> QueryMailExchangersAsync(string domain, CancellationToken cancellationToken) =>
        Answer(DemoWorld.DomainOf(domain) is { } known && Same(domain, known) ? ["mail." + known] : []);

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> QueryTextRecordsAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        string? domain = DemoWorld.DomainOf(name);
        if (domain is null)
        {
            return Answer([]);
        }

        string label = name.Length > domain.Length ? name[..(name.Length - domain.Length - 1)] : string.Empty;
        return Answer(label switch
        {
            "" => [domain == DemoWorld.MainHost && _era == DemoEra.Earlier ? "v=spf1 a mx ~all" : "v=spf1 mx -all"],
            "_dmarc" => DmarcFor(domain),
            "default._domainkey" => ["v=DKIM1; k=rsa; p=" + DkimPublicKey.Value],
            "_mta-sts" when domain == DemoWorld.BlogHost => ["v=STSv1; id=20261101T000000"],
            "_smtp._tls" when domain == DemoWorld.BlogHost => ["v=TLSRPTv1; rua=mailto:tls-berichte@blog.example"],
            _ => [],
        });
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> ResolveIpv4Async(string host, CancellationToken cancellationToken) =>
        Answer(DemoWorld.DomainOf(host) is { } domain ? [DemoWorld.AddressOf(domain)] : []);

    /// <inheritdoc/>
    public Task<string?> QueryPointerAsync(string ipAddress, CancellationToken cancellationToken)
    {
        string? name = ipAddress switch
        {
            "192.0.2.10" => "mail." + DemoWorld.MainHost,
            "192.0.2.20" => "mail." + DemoWorld.ShopHost,
            "192.0.2.30" => "mail." + DemoWorld.BlogHost,
            _ => null,
        };
        return Task.FromResult(name);
    }

    /// <inheritdoc/>
    public Task<bool> IsDnssecSignedAsync(string domain, CancellationToken cancellationToken) =>
        Task.FromResult(DemoWorld.DomainOf(domain) == DemoWorld.BlogHost);

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> QueryCaaAsync(string domain, CancellationToken cancellationToken) =>
        Answer(DemoWorld.DomainOf(domain) is DemoWorld.MainHost or DemoWorld.BlogHost ? ["0 issue \"ca.example\""] : []);

    /// <inheritdoc/>
    public Task<bool> HasTlsaAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(DemoWorld.DomainOf(name) == DemoWorld.BlogHost);

    private IReadOnlyList<string> DmarcFor(string domain) => (domain, _era) switch
    {
        (DemoWorld.MainHost, DemoEra.Earlier) => [],
        (DemoWorld.MainHost, DemoEra.Current) => ["v=DMARC1; p=none; rua=mailto:dmarc@meine-seite.example"],
        (DemoWorld.ShopHost, _) => ["v=DMARC1; p=quarantine; rua=mailto:dmarc@shop.example"],
        _ => ["v=DMARC1; p=reject; rua=mailto:dmarc@blog.example"],
    };

    private static bool Same(string name, string domain) => name.TrimEnd('.').Equals(domain, StringComparison.OrdinalIgnoreCase);

    private static Task<IReadOnlyList<string>> Answer(IReadOnlyList<string> records) => Task.FromResult(records);

    private static string CreateDkimPublicKey()
    {
        using RSA rsa = RSA.Create(2048);
        return Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
    }
}
