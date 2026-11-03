using SiteCheck.Core.Models.Guard;

namespace SiteCheck.Core.Abstractions;

/// <summary>
/// Hält den Stand der Sperrwache über einen Neustart hinweg fest. Der Server zählt die Summe
/// aller Läufe, also auch die der Konsole und die vor dem letzten Programmstart.
/// </summary>
public interface IRequestGuardStore
{
    /// <summary>Lädt den Stand je Host; bei fehlender oder beschädigter Datei leer.</summary>
    /// <param name="cancellationToken">Bricht den Zugriff ab.</param>
    /// <returns>Der Stand je Host.</returns>
    Task<IReadOnlyDictionary<string, HostGuardEntry>> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Speichert den Stand aller Hosts.</summary>
    /// <param name="hosts">Der Stand je Host.</param>
    /// <param name="cancellationToken">Bricht den Zugriff ab.</param>
    Task SaveAsync(IReadOnlyDictionary<string, HostGuardEntry> hosts, CancellationToken cancellationToken);
}
