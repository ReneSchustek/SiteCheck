namespace SiteCheck.Core.Models;

/// <summary>Ein abgelegter Prüflauf im Verlauf einer Seite.</summary>
/// <param name="RanAt">Zeitpunkt des Laufs.</param>
/// <param name="Summary">Die Zusammenzählung der Befunde.</param>
/// <param name="Findings">Die Befunde des Laufs, für den Vergleich mit einem späteren Lauf.</param>
public sealed record HistoryEntry(DateTimeOffset RanAt, FindingSummary Summary, IReadOnlyList<Finding> Findings);
