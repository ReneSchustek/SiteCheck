namespace SiteCheck.Core.Models;

/// <summary>Wie ein Befund im Vergleich zum vorigen Lauf dasteht.</summary>
public enum FindingChange
{
    /// <summary>War schon im vorigen Lauf da und ist es noch.</summary>
    Unchanged = 0,

    /// <summary>Ist neu hinzugekommen.</summary>
    New = 1,

    /// <summary>War im vorigen Lauf ein Problem und ist jetzt weg (oder in Ordnung).</summary>
    Resolved = 2,
}

/// <summary>Ein Befund zusammen mit seiner Veränderung gegenüber dem vorigen Lauf.</summary>
/// <param name="Finding">Der Befund des aktuellen Laufs, oder bei <see cref="FindingChange.Resolved"/> der des vorigen.</param>
/// <param name="Change">Wie er sich verändert hat.</param>
public sealed record ComparedFinding(Finding Finding, FindingChange Change);

/// <summary>
/// Vergleich zweier Läufe über die stabile <see cref="Finding.Key"/>-Kennung: was neu ist,
/// was behoben wurde und was unverändert blieb.
/// </summary>
public static class ReportComparison
{
    /// <summary>Vergleicht den aktuellen Lauf mit dem vorigen.</summary>
    /// <param name="current">Befunde des aktuellen Laufs.</param>
    /// <param name="previous">Befunde des vorigen Laufs; leer, wenn es keinen gab.</param>
    /// <returns>
    /// Die aktuellen Befunde mit Veränderung, gefolgt von den behobenen Problemen des vorigen
    /// Laufs. Nur Probleme gelten als „behoben“ — ein weggefallener Ok-Befund ist keine Meldung wert.
    /// </returns>
    public static IReadOnlyList<ComparedFinding> Compare(IReadOnlyList<Finding> current, IReadOnlyList<Finding> previous)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(previous);

        HashSet<string> previousKeys = previous.Select(finding => finding.Key).ToHashSet(StringComparer.Ordinal);
        HashSet<string> currentKeys = current.Select(finding => finding.Key).ToHashSet(StringComparer.Ordinal);

        List<ComparedFinding> result = current
            .Select(finding => new ComparedFinding(
                finding,
                previousKeys.Contains(finding.Key) ? FindingChange.Unchanged : FindingChange.New))
            .ToList();

        result.AddRange(previous
            .Where(finding => finding.IsProblem && !currentKeys.Contains(finding.Key))
            .Select(finding => new ComparedFinding(finding, FindingChange.Resolved)));

        return result;
    }
}
