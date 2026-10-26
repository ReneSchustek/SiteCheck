namespace SiteCheck.Core.Models.Stress;

/// <summary>
/// Einstellungen eines Lasttests. Die Obergrenzen sind <b>fest verdrahtet</b> und lassen sich
/// zur Laufzeit nicht anheben: SiteCheck ist ein Prüfwerkzeug für eigene Seiten, kein Mittel,
/// um fremde Server in die Knie zu zwingen. <see cref="Clamp"/> schneidet jede Eingabe auf
/// den erlaubten Bereich zurück, statt sie abzulehnen.
/// </summary>
/// <param name="Concurrency">Anzahl gleichzeitig offener Anfragen.</param>
/// <param name="Duration">Gesamtdauer des Laufs.</param>
public sealed record StressTestOptions(int Concurrency, TimeSpan Duration)
{
    /// <summary>Höchste erlaubte Zahl gleichzeitiger Anfragen. Darüber wäre es ein Lastangriff, kein Test.</summary>
    public const int MaxConcurrency = 20;

    /// <summary>Höchste erlaubte Laufdauer in Sekunden.</summary>
    public const int MaxDurationSeconds = 60;

    /// <summary>Mindestzahl gleichzeitiger Anfragen.</summary>
    public const int MinConcurrency = 1;

    /// <summary>Kürzeste erlaubte Laufdauer in Sekunden.</summary>
    public const int MinDurationSeconds = 5;

    /// <summary>Ein maßvoller Standard, mit dem der erste Lauf beginnt.</summary>
    public static StressTestOptions Default { get; } = new(5, TimeSpan.FromSeconds(20));

    /// <summary>Schneidet Parallelität und Dauer auf den fest verdrahteten erlaubten Bereich zurück.</summary>
    /// <returns>Eine Fassung, deren Werte garantiert innerhalb der Grenzen liegen.</returns>
    public StressTestOptions Clamp()
    {
        int concurrency = Math.Clamp(Concurrency, MinConcurrency, MaxConcurrency);
        int seconds = Math.Clamp((int)Math.Round(Duration.TotalSeconds), MinDurationSeconds, MaxDurationSeconds);
        return new StressTestOptions(concurrency, TimeSpan.FromSeconds(seconds));
    }
}
