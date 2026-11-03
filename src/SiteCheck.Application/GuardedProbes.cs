using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models.Probes;

namespace SiteCheck.Application;

/// <summary>
/// Lässt jede HTTP-Anfrage über die Sperrwache laufen. Sitzt innen am <see cref="CachingHttpProbe"/>,
/// damit Antworten aus dem Zwischenspeicher nichts kosten. Wer eine Abweisung bekommt, erhält
/// statt der Antwort die Ausnahme: Eine 429 ist keine Aussage über die Kopfzeilen der Seite.
/// </summary>
public sealed class GuardedHttpProbe(IHttpProbe inner, HostRequestGuard guard) : IHttpProbe
{
    private readonly IHttpProbe _inner = inner;
    private readonly HostRequestGuard _guard = guard;

    /// <inheritdoc/>
    public Task<HttpProbeResponse?> FetchAsync(Uri address, CancellationToken cancellationToken) =>
        SendAsync(address, _inner.FetchAsync, cancellationToken);

    /// <inheritdoc/>
    public Task<HttpProbeResponse?> FetchWithoutRedirectAsync(Uri address, CancellationToken cancellationToken) =>
        SendAsync(address, _inner.FetchWithoutRedirectAsync, cancellationToken);

    private async Task<HttpProbeResponse?> SendAsync(
        Uri address,
        Func<Uri, CancellationToken, Task<HttpProbeResponse?>> send,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);

        await _guard.EnterAsync(address.Host, cancellationToken).ConfigureAwait(false);
        HttpProbeResponse? response = await send(address, cancellationToken).ConfigureAwait(false);

        if (response is null)
        {
            await _guard.ObserveNoAnswerAsync(address.Host, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _guard.ObserveResponseAsync(address, response.StatusCode, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }
}

/// <summary>Lässt jeden TLS-Handschlag über die Sperrwache laufen; er ist auch eine Verbindung zum Server.</summary>
public sealed class GuardedTlsProbe(ITlsProbe inner, HostRequestGuard guard) : ITlsProbe
{
    private readonly ITlsProbe _inner = inner;
    private readonly HostRequestGuard _guard = guard;

    /// <inheritdoc/>
    public async Task<TlsHandshakeResult?> HandshakeAsync(string host, int port, CancellationToken cancellationToken)
    {
        await _guard.EnterAsync(host, cancellationToken).ConfigureAwait(false);
        TlsHandshakeResult? result = await _inner.HandshakeAsync(host, port, cancellationToken).ConfigureAwait(false);

        if (result is null)
        {
            await _guard.ObserveNoAnswerAsync(host, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Der Handschlag zählt als Antwort: Der Server hat die Verbindung angenommen.
            await _guard.ObserveResponseAsync(new UriBuilder(Uri.UriSchemeHttps, host, port).Uri, 200, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}
