namespace SiteCheck.Core.Models.Stress;

/// <summary>Das Ergebnis einer einzelnen Anfrage im Lasttest.</summary>
/// <param name="ElapsedMilliseconds">Dauer bis zur Antwort (oder bis zum Fehler) in Millisekunden.</param>
/// <param name="StatusCode">HTTP-Statuscode; <see langword="null"/>, wenn gar keine Antwort kam (Zeitüberschreitung, Verbindungsfehler).</param>
public sealed record RequestOutcome(double ElapsedMilliseconds, int? StatusCode)
{
    /// <summary>Ob die Anfrage erfolgreich war: eine Antwort kam und ihr Status war kein Server- oder Verbindungsfehler.</summary>
    public bool IsSuccess => StatusCode is >= 100 and < 500;
}
