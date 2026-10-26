using SiteCheck.Core.Models;

namespace SiteCheck.Core.Abstractions;

/// <summary>Lädt und speichert die wenigen Einstellungen, die das Programm sich merkt.</summary>
public interface ISettingsStore
{
    /// <summary>Lädt die Einstellungen; bei fehlender oder beschädigter Datei die Vorgaben.</summary>
    /// <param name="cancellationToken">Bricht den Zugriff ab.</param>
    /// <returns>Die Einstellungen.</returns>
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Speichert die Einstellungen.</summary>
    /// <param name="settings">Die zu speichernden Einstellungen.</param>
    /// <param name="cancellationToken">Bricht den Zugriff ab.</param>
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}
