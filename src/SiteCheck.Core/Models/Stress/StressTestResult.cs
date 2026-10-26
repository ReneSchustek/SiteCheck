namespace SiteCheck.Core.Models.Stress;

/// <summary>
/// Zusammenfassung eines Lasttests: wie viele Anfragen in welcher Zeit, wie schnell im Mittel,
/// im Median und am langsamen Ende, und wie viele scheiterten.
/// </summary>
/// <param name="TotalRequests">Gesamtzahl abgeschickter Anfragen.</param>
/// <param name="SuccessCount">Davon erfolgreich beantwortet (Status unter 500).</param>
/// <param name="FailureCount">Fehlgeschlagen (Status 500+ oder keine Antwort).</param>
/// <param name="Elapsed">Tatsächliche Gesamtdauer des Laufs.</param>
/// <param name="AverageMilliseconds">Mittlere Antwortzeit.</param>
/// <param name="MedianMilliseconds">Mittlere Antwortzeit nach Rang (Median).</param>
/// <param name="Percentile95Milliseconds">Das langsame Ende: 95 % der Antworten waren schneller.</param>
public sealed record StressTestResult(
    int TotalRequests,
    int SuccessCount,
    int FailureCount,
    TimeSpan Elapsed,
    double AverageMilliseconds,
    double MedianMilliseconds,
    double Percentile95Milliseconds)
{
    /// <summary>Anfragen pro Sekunde über den ganzen Lauf gerechnet.</summary>
    public double RequestsPerSecond => Elapsed.TotalSeconds > 0 ? TotalRequests / Elapsed.TotalSeconds : 0;

    /// <summary>Anteil fehlgeschlagener Anfragen von 0 bis 1.</summary>
    public double FailureRate => TotalRequests > 0 ? (double)FailureCount / TotalRequests : 0;

    /// <summary>Rechnet die Einzelergebnisse eines Laufs zu einer Zusammenfassung zusammen.</summary>
    /// <param name="outcomes">Die Ergebnisse aller Anfragen.</param>
    /// <param name="elapsed">Die gemessene Gesamtdauer des Laufs.</param>
    /// <returns>Die Zusammenfassung; bei leerer Eingabe eine mit lauter Nullwerten.</returns>
    public static StressTestResult From(IReadOnlyCollection<RequestOutcome> outcomes, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(outcomes);

        if (outcomes.Count == 0)
        {
            return new StressTestResult(0, 0, 0, elapsed, 0, 0, 0);
        }

        int success = outcomes.Count(outcome => outcome.IsSuccess);
        double[] sortedTimes = outcomes.Select(outcome => outcome.ElapsedMilliseconds).OrderBy(value => value).ToArray();

        return new StressTestResult(
            outcomes.Count,
            success,
            outcomes.Count - success,
            elapsed,
            sortedTimes.Average(),
            Quantile(sortedTimes, 0.50),
            Quantile(sortedTimes, 0.95));
    }

    // Nächster-Rang-Methode auf aufsteigend sortierten Werten: schlicht, ohne Interpolation,
    // und für eine grobe Lasteinschätzung völlig ausreichend.
    private static double Quantile(double[] sortedAscending, double quantile)
    {
        int rank = (int)Math.Ceiling(quantile * sortedAscending.Length);
        int index = Math.Clamp(rank - 1, 0, sortedAscending.Length - 1);
        return sortedAscending[index];
    }
}
