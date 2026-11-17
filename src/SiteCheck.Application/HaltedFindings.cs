using System.Globalization;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Localization;
using SiteCheck.Core.Models.Guard;
using SiteCheck.Core.Models;

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
                DomainText.Get("Die Seite von einem anderen Anschluss öffnen, etwa über Mobilfunk. Läuft sie dort, ist wahrscheinlich dieser Anschluss gesperrt: dann warten und von hier nichts weiter abrufen.")))
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
            DomainText.Format($"Nicht geprüft: {check.Name}"),
            DomainText.Get("Der Lauf wurde angehalten, bevor diese Prüfung an der Reihe war."));
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
                DomainText.Format($"Für {halt.Host} ist das Budget dieser Stunde aufgebraucht. Bis {until} Uhr geht nichts mehr hinaus, sonst sieht der Hoster einen Angriff."),
            RequestHaltReason.CoolingDown =>
                DomainText.Format($"Nach dem Lasttest ruht {halt.Host} bis {until} Uhr, damit die Abwehr des Hosters sich beruhigt."),
            _ =>
                DomainText.Format($"{halt.Host} hat abgewiesen oder nicht mehr geantwortet. Entweder ist die Seite gerade weg, oder der Hoster sperrt diesen Anschluss. Weitere Anfragen würden eine Sperre verlängern, deshalb geht bis {until} Uhr nichts mehr hinaus."),
        };
    }

    private static string Title(RequestHaltReason reason) => reason switch
    {
        RequestHaltReason.BudgetUsed => DomainText.Get("Prüfung angehalten: Anfragebudget dieser Stunde aufgebraucht"),
        RequestHaltReason.CoolingDown => DomainText.Get("Prüfung angehalten: Abkühlzeit nach dem Lasttest"),
        _ => DomainText.Get("Prüfung angehalten: Der Server weist ab oder antwortet nicht mehr"),
    };
}
