namespace SiteCheck.App.Services;

/// <summary>Zeigt Hinweise und holt Bestätigungen über einen Dialog ein.</summary>
public interface IConfirmationService
{
    /// <summary>Zeigt den Erststart-Hinweis zur Nutzungsgrenze und wartet, bis er bestätigt ist.</summary>
    /// <param name="cancellationToken">Bricht die Anzeige ab.</param>
    Task ShowScopeNoticeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Holt vor einem Lasttest die Bestätigung ein, dass die Seite dem Nutzer gehört und selbst
    /// gehostet ist. Ohne dieses „Ja“ darf kein Lasttest laufen.
    /// </summary>
    /// <param name="address">Die betroffene Adresse, die im Dialog genannt wird.</param>
    /// <param name="cancellationToken">Bricht die Anzeige ab.</param>
    /// <returns><see langword="true"/>, wenn der Nutzer das Eigentum bestätigt hat.</returns>
    Task<bool> ConfirmStressTestOwnershipAsync(string address, CancellationToken cancellationToken);

    /// <summary>Weist vor dem ersten Lauf gegen eine Seite auf die Aussperr-Gefahr hin.</summary>
    /// <param name="addresses">Die betroffenen Adressen, die im Dialog genannt werden.</param>
    /// <param name="cancellationToken">Bricht die Anzeige ab.</param>
    /// <returns><see langword="true"/>, wenn der Nutzer fortfahren will.</returns>
    Task<bool> ConfirmHostNoticeAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken);

    /// <summary>Holt die Bestätigung für die Abfrage heikler Pfade ein; gilt nur für diesen einen Lauf.</summary>
    /// <param name="address">Die betroffene Adresse.</param>
    /// <param name="cancellationToken">Bricht die Anzeige ab.</param>
    /// <returns><see langword="true"/>, wenn der Nutzer die Abfrage will.</returns>
    Task<bool> ConfirmSensitivePathsAsync(string address, CancellationToken cancellationToken);

    /// <summary>Fragt, ob trotz laufender Sperrzeit erneut Anfragen an die Seite gehen sollen.</summary>
    /// <param name="address">Die betroffene Adresse.</param>
    /// <param name="reason">Warum die Seite gerade ruht.</param>
    /// <param name="cancellationToken">Bricht die Anzeige ab.</param>
    /// <returns><see langword="true"/>, wenn der Nutzer die Sperrzeit aufheben will.</returns>
    Task<bool> ConfirmRetryDuringHaltAsync(string address, string reason, CancellationToken cancellationToken);
}
