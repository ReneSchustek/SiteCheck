using SiteCheck.Core.Models;

namespace SiteCheck.Core.Abstractions;

/// <summary>Legt je Seite die vergangenen Prüfläufe ab, damit sich zeigen lässt, was sich gebessert oder verschlechtert hat.</summary>
public interface IHistoryStore
{
    /// <summary>Lädt den Verlauf einer Seite, neuester Lauf zuerst.</summary>
    /// <param name="address">Adresse der Seite, wie <see cref="CheckTarget.ToString"/> sie liefert.</param>
    /// <param name="cancellationToken">Bricht den Zugriff ab.</param>
    /// <returns>Die Läufe, neuester zuerst; leer, wenn es keinen gibt.</returns>
    Task<IReadOnlyList<HistoryEntry>> LoadAsync(string address, CancellationToken cancellationToken);

    /// <summary>Hängt einen Lauf an den Verlauf einer Seite an.</summary>
    /// <param name="address">Adresse der Seite.</param>
    /// <param name="entry">Der abzulegende Lauf.</param>
    /// <param name="cancellationToken">Bricht den Zugriff ab.</param>
    Task AppendAsync(string address, HistoryEntry entry, CancellationToken cancellationToken);

    /// <summary>Löscht den gesamten Verlauf einer Seite.</summary>
    /// <param name="address">Adresse der Seite.</param>
    /// <param name="cancellationToken">Bricht den Zugriff ab.</param>
    Task ClearAsync(string address, CancellationToken cancellationToken);
}
