namespace SiteCheck.Core.Models;

/// <summary>Zusammenzählung eines Laufs: wie viele Befunde je Stufe, und welche Stufe die Ampel bestimmt.</summary>
/// <param name="OkCount">Befunde in Ordnung.</param>
/// <param name="InfoCount">Hinweise.</param>
/// <param name="WarningCount">Befunde, die verbessert werden sollten.</param>
/// <param name="CriticalCount">Kritische Befunde.</param>
public sealed record FindingSummary(int OkCount, int InfoCount, int WarningCount, int CriticalCount)
{
    /// <summary>Die schlimmste vorkommende Stufe. Ohne jeden Befund gilt die Seite als in Ordnung.</summary>
    public Severity WorstSeverity =>
        CriticalCount > 0 ? Severity.Critical
        : WarningCount > 0 ? Severity.Warning
        : InfoCount > 0 ? Severity.Info
        : Severity.Ok;

    /// <summary>Zählt die Befunde je Stufe aus.</summary>
    /// <param name="findings">Die Befunde eines Laufs.</param>
    /// <returns>Die Zusammenzählung.</returns>
    public static FindingSummary From(IEnumerable<Finding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);

        int ok = 0;
        int info = 0;
        int warning = 0;
        int critical = 0;
        foreach (Finding finding in findings)
        {
            switch (finding.Severity)
            {
                case Severity.Ok:
                    ok++;
                    break;
                case Severity.Info:
                    info++;
                    break;
                case Severity.Warning:
                    warning++;
                    break;
                case Severity.Critical:
                    critical++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(findings), finding.Severity, "Unbekannte Stufe.");
            }
        }

        return new FindingSummary(ok, info, warning, critical);
    }
}
