using System.Globalization;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Guard;

namespace SiteCheck.Application;

/// <summary>Die Befunde und Texte, die entstehen, wenn die Sperrwache einen Lauf anhält.</summary>
public static class HaltedFindings
{
    /// <summary>Der Befund, der erklärt, warum der Lauf angehalten wurde.</summary>
    /// <param name="halt">Der Grund.</param>
    /// <returns>Der Befund für den Anfang des Berichts.</returns>
    public static Finding ForRun(RequestsHaltedException halt)
    {
        ArgumentNullException.ThrowIfNull(halt);

        return new Finding(
            CheckReport.HaltedFindingKey,
            CheckCategory.Web,
            Severity.Warning,
            Title(halt.Reason),
            Describe(halt),
            new Recommendation(
                "Die Seite von einem anderen Anschluss öffnen, etwa über Mobilfunk. Läuft sie dort, ist wahrscheinlich "
                + "dieser Anschluss gesperrt: dann warten und von hier nichts weiter abrufen."))
        {
            Confidence = Confidence.Inferred,
        };
    }

    /// <summary>Der Befund für eine Prüfung, die wegen des Halts nicht lief.</summary>
    /// <param name="check">Die Prüfung.</param>
    /// <returns>Der Befund; nie „in Ordnung“.</returns>
    public static Finding ForCheck(ICheck check)
    {
        ArgumentNullException.ThrowIfNull(check);
        return new Finding(
            check.Id + CheckReport.NotCheckedKeySuffix,
            check.Category,
            Severity.Info,
            "Nicht geprüft — der Lauf wurde angehalten",
            "Kennung der Prüfung: " + check.Id);
    }

    /// <summary>Beschreibt einen Halt in wenigen Sätzen, für Statuszeilen und die Konsole.</summary>
    /// <param name="halt">Der Grund.</param>
    /// <returns>Der Text samt Host und Uhrzeit.</returns>
    public static string Describe(RequestsHaltedException halt)
    {
        ArgumentNullException.ThrowIfNull(halt);

        string until = halt.Until.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture);
        return halt.Reason switch
        {
            RequestHaltReason.BudgetUsed =>
                $"Für {halt.Host} ist das Budget dieser Stunde aufgebraucht. Bis {until} Uhr geht nichts mehr hinaus, sonst sieht der Hoster einen Angriff.",
            RequestHaltReason.CoolingDown =>
                $"Nach dem Lasttest ruht {halt.Host} bis {until} Uhr, damit die Abwehr des Hosters sich beruhigt.",
            _ =>
                $"{halt.Host} hat abgewiesen oder nicht mehr geantwortet. Entweder ist die Seite gerade weg, oder der Hoster sperrt diesen Anschluss. "
                + $"Weitere Anfragen würden eine Sperre verlängern, deshalb geht bis {until} Uhr nichts mehr hinaus.",
        };
    }

    private static string Title(RequestHaltReason reason) => reason switch
    {
        RequestHaltReason.BudgetUsed => "Prüfung angehalten: Anfragebudget dieser Stunde aufgebraucht",
        RequestHaltReason.CoolingDown => "Prüfung angehalten: Abkühlzeit nach dem Lasttest",
        _ => "Prüfung angehalten: Der Server weist ab oder antwortet nicht mehr",
    };
}
