using SiteCheck.Core.Models.Probes;

namespace SiteCheck.Core.Abstractions;

/// <summary>Führt einen TLS-Handschlag mit der Seite und liest Fassung und Zertifikatsdaten aus.</summary>
public interface ITlsProbe
{
    /// <summary>
    /// Verbindet sich mit Rechner und Port und handelt die Verschlüsselung aus.
    /// </summary>
    /// <param name="host">Rechnername (ASCII, wie <see cref="Models.CheckTarget.Host"/>).</param>
    /// <param name="port">Port, üblicherweise 443.</param>
    /// <param name="cancellationToken">Bricht den Versuch ab.</param>
    /// <returns>Das Ergebnis, oder <see langword="null"/>, wenn gar keine Verbindung zustande kam.</returns>
    Task<TlsHandshakeResult?> HandshakeAsync(string host, int port, CancellationToken cancellationToken);
}
