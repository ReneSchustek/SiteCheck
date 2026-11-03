namespace SiteCheck.Core.Models;

/// <summary>Stufe eines Protokolleintrags, soweit die Ansicht sie unterscheidet.</summary>
public enum LogEntryLevel
{
    /// <summary>Normaler Ablauf.</summary>
    Information = 0,

    /// <summary>Etwas Auffälliges, etwa ein Halt.</summary>
    Warning = 1,

    /// <summary>Ein Fehler.</summary>
    Error = 2,
}

/// <summary>Ein Eintrag des Protokolls.</summary>
/// <param name="Timestamp">Lokale Zeit als Text, wie sie in der Datei steht.</param>
/// <param name="Level">Die Stufe.</param>
/// <param name="Text">Bereich und Meldung, bei Fehlern samt Ausnahme.</param>
public sealed record LogEntry(string Timestamp, LogEntryLevel Level, string Text);
