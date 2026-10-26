using SiteCheck.Core.Models;

namespace SiteCheck.Core.Abstractions;

/// <summary>
/// Eine einzelne Prüfung. Jede liefert null bis mehrere Befunde für ein Ziel und ist
/// unabhängig von Konsole und Oberfläche.
/// </summary>
public interface ICheck
{
    /// <summary>Stabile Kennung der Prüfung, etwa <c>web.redirect</c>. Ordnet Befunde ihrer Quelle zu.</summary>
    string Id { get; }

    /// <summary>Bereich, zu dem die Prüfung gehört; steuert die Gruppierung in Ausgabe und Bericht.</summary>
    CheckCategory Category { get; }

    /// <summary>Führt die Prüfung aus.</summary>
    /// <param name="target">Das Prüfziel.</param>
    /// <param name="cancellationToken">Bricht die Prüfung ab.</param>
    /// <returns>Die Befunde dieser Prüfung in fester Reihenfolge.</returns>
    Task<IReadOnlyList<Finding>> RunAsync(CheckTarget target, CancellationToken cancellationToken);
}
