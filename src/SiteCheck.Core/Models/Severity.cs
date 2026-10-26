namespace SiteCheck.Core.Models;

/// <summary>
/// Wie schwer ein Befund wiegt. Die Reihenfolge ist aufsteigend und trägt: Der schlimmste
/// Befund einer Seite ist der mit dem höchsten Wert, und genau danach richtet sich die Ampel.
/// </summary>
public enum Severity
{
    /// <summary>Gut eingestellt, nichts zu tun.</summary>
    Ok = 0,

    /// <summary>Zur Kenntnis; kein Fehler, aber ein Umstand, den man kennen sollte.</summary>
    Info = 1,

    /// <summary>Sollte verbessert werden.</summary>
    Warning = 2,

    /// <summary>Muss behoben werden; ein offenes Risiko oder ein Grund für ausbleibende Mails.</summary>
    Critical = 3,
}
