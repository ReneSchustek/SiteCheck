using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Probes;

namespace SiteCheck.Core.Abstractions;

/// <summary>
/// Holt Kopfzeilen, Status und Cookies einer Adresse. Die Umsetzung liegt in SiteCheck.Data;
/// im Test tritt eine Attrappe mit festen Antworten an ihre Stelle, damit die Prüflogik ohne
/// echtes Internet geprüft werden kann.
/// </summary>
public interface IHttpProbe
{
    /// <summary>
    /// Fragt eine Adresse ab und folgt dabei Umleitungen bis zur endgültigen Antwort.
    /// </summary>
    /// <param name="address">Die abzufragende Adresse.</param>
    /// <param name="cancellationToken">Bricht die Abfrage ab.</param>
    /// <returns>Die Antwort, oder <see langword="null"/>, wenn die Adresse nicht erreichbar war.</returns>
    Task<HttpProbeResponse?> FetchAsync(Uri address, CancellationToken cancellationToken);

    /// <summary>
    /// Fragt eine Adresse ab, <b>ohne</b> Umleitungen zu folgen. So wird die erste Antwort
    /// auf die http-Adresse sichtbar, die eine Umleitungsprüfung gerade braucht.
    /// </summary>
    /// <param name="address">Die abzufragende Adresse.</param>
    /// <param name="cancellationToken">Bricht die Abfrage ab.</param>
    /// <returns>Die Antwort, oder <see langword="null"/>, wenn die Adresse nicht erreichbar war.</returns>
    Task<HttpProbeResponse?> FetchWithoutRedirectAsync(Uri address, CancellationToken cancellationToken);
}
