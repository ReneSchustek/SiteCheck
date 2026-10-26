namespace SiteCheck.Core.Abstractions;

/// <summary>Meldet den Fortschritt eines Laufs, damit die Oberfläche einen Balken bewegen kann.</summary>
public interface ICheckProgress
{
    /// <summary>Meldet, dass eine Prüfung beginnt.</summary>
    /// <param name="checkId">Kennung der Prüfung.</param>
    /// <param name="completed">Wie viele Prüfungen bereits fertig sind.</param>
    /// <param name="total">Wie viele Prüfungen der Lauf insgesamt umfasst.</param>
    void Report(string checkId, int completed, int total);
}
