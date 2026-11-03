using SiteCheck.Core.Models;

namespace SiteCheck.Core.Abstractions;

/// <summary>Liest das Protokoll für die Ansicht in der App.</summary>
public interface ILogReader
{
    /// <summary>Liest die Einträge aller aufbewahrten Tage.</summary>
    /// <param name="cancellationToken">Bricht das Lesen ab.</param>
    /// <returns>Die Einträge, neueste zuerst; leer, wenn es noch keine gibt.</returns>
    Task<IReadOnlyList<LogEntry>> ReadAsync(CancellationToken cancellationToken);
}
