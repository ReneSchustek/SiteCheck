namespace SiteCheck.Core.Models;

/// <summary>Das Ergebnis eines vollständigen Prüflaufs für eine Adresse.</summary>
/// <param name="Address">Die geprüfte Adresse, wie <see cref="CheckTarget.ToString"/> sie liefert.</param>
/// <param name="StartedAt">Beginn des Laufs.</param>
/// <param name="FinishedAt">Ende des Laufs.</param>
/// <param name="Findings">Alle Befunde in der Reihenfolge der Prüfungen.</param>
public sealed record CheckReport(
    string Address,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    IReadOnlyList<Finding> Findings)
{
    /// <summary>Zusammenzählung der Befunde je Stufe.</summary>
    public FindingSummary Summary => FindingSummary.From(Findings);
}
