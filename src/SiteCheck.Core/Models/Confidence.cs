namespace SiteCheck.Core.Models;

/// <summary>
/// Wie sicher ein Befund ist. Ein Außen-Check sieht manches direkt und schließt anderes nur aus
/// Indizien. Diese Unterscheidung steht sichtbar am Befund, damit keine Vermutung als Tatsache
/// durchgeht (siehe die Lehre aus dem ersten Bericht zur Musik-Seite).
/// </summary>
public enum Confidence
{
    /// <summary>Direkt gemessen: Die Anfrage oder DNS-Abfrage hat es so ergeben.</summary>
    Measured = 0,

    /// <summary>Aus Indizien geschlossen; von außen nicht sicher feststellbar.</summary>
    Inferred = 1,
}
