using SiteCheck.Core.Models.Stress;

namespace SiteCheck.Core.Abstractions;

/// <summary>
/// Schickt eine einzelne Anfrage an die Adresse und misst, wie lange sie dauert. Die
/// Umsetzung liegt in SiteCheck.Data; im Test tritt eine Attrappe an ihre Stelle, damit der
/// Ablauf des Lasttests ohne echten Server geprüft werden kann.
/// </summary>
public interface ILoadProbe
{
    /// <summary>
    /// Schickt eine Anfrage und gibt deren Ergebnis zurück. Wirft nicht bei einem
    /// HTTP-Fehler oder einer Zeitüberschreitung, sondern bildet beides im Ergebnis ab.
    /// </summary>
    /// <param name="address">Die anzufragende Adresse.</param>
    /// <param name="cancellationToken">Bricht die einzelne Anfrage ab.</param>
    /// <returns>Das gemessene Ergebnis der Anfrage.</returns>
    Task<RequestOutcome> SendAsync(Uri address, CancellationToken cancellationToken);
}
