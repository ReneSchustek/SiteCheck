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
    /// <summary>Kennung des Befunds, der erklärt, warum ein Lauf angehalten wurde.</summary>
    public const string HaltedFindingKey = "run.halted";

    /// <summary>Endung der Kennung eines Befunds, dessen Prüfung nicht lief.</summary>
    public const string NotCheckedKeySuffix = ".notchecked";

    /// <summary>Ob der Lauf angehalten wurde und der Bericht deshalb unvollständig ist.</summary>
    public bool WasHalted => Findings.Any(finding => finding.Key == HaltedFindingKey);

    /// <summary>Zusammenzählung der Befunde je Stufe.</summary>
    public FindingSummary Summary => FindingSummary.From(Findings);
}
