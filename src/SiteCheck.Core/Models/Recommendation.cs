namespace SiteCheck.Core.Models;

/// <summary>
/// Der konkrete Rat zu einem Befund: was zu tun ist, und wo es sich lohnt, ein Beispiel
/// zum Abschreiben. Das Beispiel ist Konfiguration oder ein DNS-Eintrag, keine Prosa, und
/// wird deshalb in Festbreitenschrift gezeigt.
/// </summary>
/// <param name="Advice">Was zu tun ist, in ganzen Sätzen.</param>
/// <param name="Example">Optionales Beispiel zum Übernehmen, etwa eine Zeile für die .htaccess.</param>
public sealed record Recommendation(string Advice, string? Example = null);
