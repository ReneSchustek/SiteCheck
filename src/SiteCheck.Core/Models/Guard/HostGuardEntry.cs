namespace SiteCheck.Core.Models.Guard;

/// <summary>Was sich die Sperrwache über einen Host merkt, auch über einen Neustart hinweg.</summary>
/// <param name="RecentRequests">Zeitpunkte der Anfragen innerhalb der letzten Stunde, älteste zuerst.</param>
/// <param name="BlockedUntil">Bis wann nichts an diesen Host gesendet wird; leer, wenn er frei ist.</param>
/// <param name="BlockReason">Warum der Host gerade gesperrt ist.</param>
/// <param name="NoticeAcknowledged">Ob der Hinweis zur Aussperr-Gefahr für diesen Host bestätigt wurde.</param>
public sealed record HostGuardEntry(
    IReadOnlyList<DateTimeOffset> RecentRequests,
    DateTimeOffset? BlockedUntil,
    RequestHaltReason? BlockReason,
    bool NoticeAcknowledged)
{
    /// <summary>Stand eines Hosts, an den noch nichts gesendet wurde.</summary>
    public static HostGuardEntry Empty { get; } = new([], null, null, false);
}
