namespace SiteCheck.Core.Models;

/// <summary>
/// Was sich das Programm zwischen zwei Starts merkt. Bewusst klein: Zugangsdaten oder
/// sonstige Geheimnisse gibt es in SiteCheck nicht und darf es hier nie geben.
/// </summary>
/// <param name="WindowWidth">Zuletzt verwendete Fensterbreite in Pixeln, leer beim ersten Start.</param>
/// <param name="WindowHeight">Zuletzt verwendete Fensterhöhe in Pixeln, leer beim ersten Start.</param>
/// <param name="ScopeNoticeAcknowledged">Ob der Hinweis „nur eigene Seiten, nur passiv“ bestätigt wurde.</param>
public sealed record AppSettings(int? WindowWidth, int? WindowHeight, bool ScopeNoticeAcknowledged)
{
    /// <summary>Stand beim allerersten Start.</summary>
    public static AppSettings Default { get; } = new(null, null, false);
}
