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
}
