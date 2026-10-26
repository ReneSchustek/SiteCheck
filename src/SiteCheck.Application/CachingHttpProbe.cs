using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models.Probes;

namespace SiteCheck.Application;

/// <summary>
/// Umhüllt eine <see cref="IHttpProbe"/> und merkt sich jede GET-Antwort je Adresse für die
/// Dauer <b>eines</b> Laufs. So holen mehrere Prüfungen, die dieselbe Seite ansehen (Kopfzeilen,
/// Cookies, Versionsverrat und die „/“-Verzeichnisprüfung), diese Seite nur einmal.
/// <para>
/// Bewusst nur pro Lauf: Eine neue Instanz je Lauf stellt sicher, dass ein erneuter Lauf echte
/// Änderungen an der Seite sieht und nicht einen alten Stand meldet.
/// </para>
/// </summary>
public sealed class CachingHttpProbe(IHttpProbe inner) : IHttpProbe
{
    private readonly IHttpProbe _inner = inner;

    // Null ist eine gültige, zu merkende Antwort („nicht erreichbar“): So laufen Folgeprüfungen
    // nicht erneut in dasselbe Timeout. Deshalb ein Dictionary auf nullable, kein GetOrAdd mit Wert.
    private readonly Dictionary<string, HttpProbeResponse?> _following = [];
    private readonly Dictionary<string, HttpProbeResponse?> _nonFollowing = [];

    /// <inheritdoc/>
    public async Task<HttpProbeResponse?> FetchAsync(Uri address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (_following.TryGetValue(address.AbsoluteUri, out HttpProbeResponse? cached))
        {
            return cached;
        }

        HttpProbeResponse? response = await _inner.FetchAsync(address, cancellationToken).ConfigureAwait(false);
        _following[address.AbsoluteUri] = response;
        return response;
    }

    /// <inheritdoc/>
    public async Task<HttpProbeResponse?> FetchWithoutRedirectAsync(Uri address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (_nonFollowing.TryGetValue(address.AbsoluteUri, out HttpProbeResponse? cached))
        {
            return cached;
        }

        HttpProbeResponse? response = await _inner.FetchWithoutRedirectAsync(address, cancellationToken).ConfigureAwait(false);
        _nonFollowing[address.AbsoluteUri] = response;
        return response;
    }
}
