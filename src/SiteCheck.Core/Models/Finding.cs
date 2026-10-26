namespace SiteCheck.Core.Models;

/// <summary>
/// Ein einzelnes Prüfergebnis.
/// <para>
/// <see cref="Key"/> ist eine stabile Kennung je Sachverhalt, nicht je Lauf, etwa
/// <c>header.missing.Content-Security-Policy</c>. Über sie erkennt der Vergleich zweier Läufe,
/// was neu, behoben oder unverändert ist; ein Titel dagegen enthält Zahlen wie die Resttage
/// des Zertifikats und ändert sich von Lauf zu Lauf.
/// </para>
/// </summary>
/// <param name="Key">Stabile Kennung des Sachverhalts.</param>
/// <param name="Category">Bereich, zu dem der Befund gehört.</param>
/// <param name="Severity">Gewicht des Befunds.</param>
/// <param name="Title">Ein klarer Satz, was festgestellt wurde.</param>
/// <param name="Detail">Optionale Begründung oder gefundene Werte.</param>
/// <param name="Recommendation">Was zu tun ist; bei Befunden in Ordnung leer.</param>
public sealed record Finding(
    string Key,
    CheckCategory Category,
    Severity Severity,
    string Title,
    string? Detail = null,
    Recommendation? Recommendation = null)
{
    /// <summary>
    /// Wie sicher der Befund ist. Als Zusatzeigenschaft (nicht im Konstruktor), damit die vielen
    /// gesicherten Befunde knapp bleiben und nur Vermutungen ausdrücklich
    /// <c>{ Confidence = Confidence.Inferred }</c> setzen.
    /// </summary>
    public Confidence Confidence { get; init; } = Confidence.Measured;

    /// <summary>Ob der Befund etwas zu tun verlangt. Hinweise zählen nicht dazu.</summary>
    public bool IsProblem => Severity >= Severity.Warning;
}
