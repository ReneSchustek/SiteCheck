using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models.Probes;
using SiteCheck.Core.Models.Stress;
using SiteCheck.Core.Models.Update;

namespace SiteCheck.Data.Demo;

/// <summary>TLS-Handschlag der Vorführwelt: gültige Zertifikate, der Shop noch mit TLS 1.2.</summary>
/// <param name="clock">Zeitquelle; das Zertifikat läuft immer gut zwei Monate nach „jetzt“ ab.</param>
public sealed class DemoTlsProbe(TimeProvider clock) : ITlsProbe
{
    private static readonly TimeSpan CertificateRemaining = TimeSpan.FromDays(64);

    private readonly TimeProvider _clock = clock;

    /// <inheritdoc/>
    public Task<TlsHandshakeResult?> HandshakeAsync(string host, int port, CancellationToken cancellationToken)
    {
        string? domain = DemoWorld.DomainOf(host);
        TlsHandshakeResult? result = domain is null
            ? null
            : new TlsHandshakeResult(
                domain == DemoWorld.ShopHost ? TlsVersion.Tls12 : TlsVersion.Tls13,
                _clock.GetUtcNow() + CertificateRemaining,
                "Beispiel-CA R1",
                IsChainTrusted: true,
                IsNameMatch: true);
        return Task.FromResult(result);
    }
}

/// <summary>
/// Lastsonde der Vorführwelt: Antwortzeiten zwischen 90 und 250 ms in festem Muster, ohne Netz.
/// Der Shop weist ab der 40. Anfrage mit 429 ab, damit der Abbruch bei Abweisung zu sehen ist.
/// </summary>
public sealed class DemoLoadProbe : ILoadProbe
{
    private const int ShopRejectsAfter = 40;

    private int _sent;

    /// <inheritdoc/>
    public async Task<RequestOutcome> SendAsync(Uri address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        int number = Interlocked.Increment(ref _sent);
        string? domain = DemoWorld.DomainOf(address.Host);
        if (domain is null)
        {
            return new RequestOutcome(0, null);
        }

        // 37 und 160 sind teilerfremd: Das Muster wiederholt sich erst nach 160 Anfragen und sieht
        // im Verlauf unregelmäßig aus.
        double elapsed = 90 + (number * 37 % 160);
        await Task.Delay(TimeSpan.FromMilliseconds(elapsed), cancellationToken).ConfigureAwait(false);
        int status = domain == DemoWorld.ShopHost && number > ShopRejectsAfter ? 429 : 200;
        return new RequestOutcome(elapsed, status);
    }
}

/// <summary>Update-Prüfung der Vorführung: meldet stets „aktuell“ und fragt nirgends nach.</summary>
/// <param name="current">Die laufende Version.</param>
public sealed class DemoUpdateChecker(Version current) : IUpdateChecker
{
    private readonly Version _current = current;

    /// <inheritdoc/>
    public Task<UpdateInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(UpdateInfo.NoUpdate(_current));
}

/// <summary>
/// Download-Zugang der Vorführung. Erreicht ihn trotzdem ein Aufruf, ist das ein Fehler in der
/// Verdrahtung, und er soll laut scheitern statt still ins Netz zu gehen.
/// </summary>
public sealed class OfflineHttpGetClient : IHttpGetClient
{
    /// <inheritdoc/>
    public Task<string?> GetStringAsync(Uri requestUri, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException($"Im Vorführmodus wird nichts geladen: {requestUri}");

    /// <inheritdoc/>
    public Task<bool> DownloadFileAsync(Uri requestUri, string destinationPath, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException($"Im Vorführmodus wird nichts geladen: {requestUri}");
}
