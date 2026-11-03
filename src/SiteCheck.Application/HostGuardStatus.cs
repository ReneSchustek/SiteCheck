using SiteCheck.Core.Models.Guard;

namespace SiteCheck.Application;

/// <summary>Was die Oberfläche vor einem Lauf über einen Host wissen muss.</summary>
/// <param name="BlockedUntil">Bis wann nichts gesendet wird; leer, wenn der Host frei ist.</param>
/// <param name="BlockReason">Warum der Host gesperrt ist.</param>
/// <param name="NoticeAcknowledged">Ob der Hinweis zur Aussperr-Gefahr bestätigt wurde.</param>
public sealed record HostGuardStatus(DateTimeOffset? BlockedUntil, RequestHaltReason? BlockReason, bool NoticeAcknowledged)
{
    /// <summary>Ob gerade nichts an den Host gesendet wird.</summary>
    public bool IsBlocked => BlockedUntil is not null;
}
