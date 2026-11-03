namespace SiteCheck.Core.Models;

/// <summary>
/// Was sich das Programm zwischen zwei Starts merkt. Bewusst klein: Zugangsdaten oder
/// sonstige Geheimnisse gibt es in SiteCheck nicht und darf es hier nie geben.
/// </summary>
/// <param name="WindowWidth">Zuletzt verwendete Fensterbreite in Pixeln, leer beim ersten Start.</param>
/// <param name="WindowHeight">Zuletzt verwendete Fensterhöhe in Pixeln, leer beim ersten Start.</param>
/// <param name="ScopeNoticeAcknowledged">Ob der Hinweis „nur eigene Seiten, nur von außen“ bestätigt wurde.</param>
/// <param name="AutoUpdateEnabled">Ob das Programm einmal am Tag nach Updates sucht und sie selbst einspielt.</param>
/// <param name="LastUpdateCheck">Wann zuletzt nach Updates gesucht wurde.</param>
/// <param name="PendingUpdateVersion">Die heruntergeladene und signaturgeprüfte Version, die beim nächsten Start eingespielt wird.</param>
/// <param name="PendingUpdateSha256">Der vermerkte Hash ihrer Programmdatei; nur genau dieses Paket wird eingespielt.</param>
public sealed record AppSettings(
    int? WindowWidth,
    int? WindowHeight,
    bool ScopeNoticeAcknowledged,
    bool AutoUpdateEnabled = true,
    DateTimeOffset? LastUpdateCheck = null,
    string? PendingUpdateVersion = null,
    string? PendingUpdateSha256 = null)
{
    /// <summary>Stand beim allerersten Start.</summary>
    public static AppSettings Default { get; } = new(null, null, false);
}
