namespace SiteCheck.Core.Models.Stress;

/// <summary>Zwischenstand eines laufenden Lasttests, damit die Oberfläche mitlaufen kann.</summary>
/// <param name="CompletedRequests">Bisher abgeschlossene Anfragen.</param>
/// <param name="Elapsed">Bisher vergangene Zeit.</param>
/// <param name="TotalDuration">Geplante Gesamtdauer; daraus ergibt sich der Balken.</param>
public sealed record StressTestProgress(int CompletedRequests, TimeSpan Elapsed, TimeSpan TotalDuration)
{
    /// <summary>Fortschritt von 0 bis 1, auf die Dauer bezogen.</summary>
    public double Fraction => TotalDuration.TotalSeconds > 0
        ? Math.Clamp(Elapsed.TotalSeconds / TotalDuration.TotalSeconds, 0, 1)
        : 0;
}
