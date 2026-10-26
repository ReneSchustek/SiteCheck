namespace SiteCheck.App.ViewModels;

/// <summary>
/// Der Zustand der Prüf-Ansicht. Als ausdrücklicher Zustand statt mehrerer Wahrheitswerte,
/// weil der Prüflauf abgebrochen werden kann und damit mehr als zwei Ausgänge hat (siehe
/// ViewModel-State-Regel).
/// </summary>
public enum CheckRunState
{
    /// <summary>Noch kein Lauf; das Adressfeld wartet auf eine Eingabe.</summary>
    Idle,

    /// <summary>Ein Lauf läuft gerade.</summary>
    Running,

    /// <summary>Ein Lauf ist vollständig durchgelaufen; Ergebnisse stehen.</summary>
    Completed,

    /// <summary>Der Nutzer hat den Lauf abgebrochen.</summary>
    Cancelled,

    /// <summary>Der Lauf ist an einem unerwarteten Fehler gescheitert.</summary>
    Error,
}
