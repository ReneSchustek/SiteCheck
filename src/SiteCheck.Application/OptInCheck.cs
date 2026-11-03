using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Localization;
using SiteCheck.Core.Models;

namespace SiteCheck.Application;

/// <summary>
/// Steht für eine Prüfung, die im Standardlauf nicht mitläuft. Sie sendet nichts und sagt im
/// Bericht, dass nichts geprüft wurde, damit das Fehlen nicht wie „in Ordnung“ aussieht.
/// </summary>
internal sealed class OptInCheck(ICheck check, string title) : ICheck
{
    public string Id => check.Id;

    public CheckCategory Category => check.Category;

    public Task<IReadOnlyList<Finding>> RunAsync(CheckTarget target, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Finding>>(
        [
            new Finding(
                check.Id + CheckReport.NotCheckedKeySuffix,
                check.Category,
                Severity.Info,
                title,
                DomainText.Get("Diese Pfade sehen für die Abwehr eines Hosters wie ein Scanner aus und können den eigenen Anschluss sperren. Deshalb laufen sie nur nach ausdrücklicher Bestätigung.")),
        ]);
}
