using SiteCheck.Core.Models;

namespace SiteCheck.Core.Abstractions;

/// <summary>Speichert die Liste der eigenen Seiten, die man regelmäßig prüft. Keine Zugangsdaten, nur Name und Adresse.</summary>
public interface ISiteStore
{
    /// <summary>Lädt die gespeicherten Seiten; bei fehlender oder beschädigter Datei eine leere Liste.</summary>
    /// <param name="cancellationToken">Bricht den Zugriff ab.</param>
    /// <returns>Die Seiten in gespeicherter Reihenfolge.</returns>
    Task<IReadOnlyList<Site>> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Speichert die gesamte Seitenliste.</summary>
    /// <param name="sites">Die zu speichernden Seiten.</param>
    /// <param name="cancellationToken">Bricht den Zugriff ab.</param>
    Task SaveAsync(IReadOnlyList<Site> sites, CancellationToken cancellationToken);
}
