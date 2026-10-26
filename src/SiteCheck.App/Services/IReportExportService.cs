using SiteCheck.Core.Models;

namespace SiteCheck.App.Services;

/// <summary>
/// Gibt einen Bericht weiter: in die Zwischenablage kopieren oder als Datei speichern. Beides
/// braucht die Oberfläche (Zwischenablage, Dateiauswahl), deshalb hier in der App-Schicht.
/// </summary>
public interface IReportExportService
{
    /// <summary>Legt den Textbericht in die Zwischenablage.</summary>
    /// <param name="report">Der zu kopierende Bericht.</param>
    void CopyTextToClipboard(CheckReport report);

    /// <summary>
    /// Fragt einen Speicherort ab und schreibt den HTML-Bericht dorthin.
    /// </summary>
    /// <param name="report">Der zu speichernde Bericht.</param>
    /// <param name="cancellationToken">Bricht das Speichern ab.</param>
    /// <returns>Der Pfad der geschriebenen Datei, oder <see langword="null"/>, wenn der Nutzer abbrach.</returns>
    Task<string?> SaveHtmlAsync(CheckReport report, CancellationToken cancellationToken);
}
