using Microsoft.UI.Xaml;
using SiteCheck.Application;
using SiteCheck.Core.Models;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace SiteCheck.App.Services;

/// <summary>
/// Kopiert den Textbericht in die Zwischenablage und speichert den HTML-Bericht über die
/// Dateiauswahl. Im unpackaged-Betrieb muss die Dateiauswahl mit dem Fensterhandle verbunden
/// werden (<c>InitializeWithWindow</c>), sonst öffnet sie sich nicht.
/// </summary>
public sealed class ReportExportService : IReportExportService
{
    /// <inheritdoc/>
    public void CopyTextToClipboard(CheckReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        DataPackage package = new();
        package.SetText(ReportBuilder.BuildText(report));
        Clipboard.SetContent(package);
    }

    /// <inheritdoc/>
    public async Task<string?> SaveHtmlAsync(CheckReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);

        Window? window = App.MainWindow;
        if (window is null)
        {
            return null;
        }

        FileSavePicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = SuggestFileName(report),
        };
        picker.FileTypeChoices.Add("HTML-Bericht", [".html"]);

        nint handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, handle);

        StorageFile? file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return null;
        }

        await FileIO.WriteTextAsync(file, ReportBuilder.BuildHtml(report));
        return file.Path;
    }

    private static string SuggestFileName(CheckReport report)
    {
        // Aus der Adresse einen brauchbaren Dateinamen bauen: Rechnername plus Datum.
        string host = Uri.TryCreate(report.Address, UriKind.Absolute, out Uri? uri) ? uri.Host : "bericht";
        string safeHost = string.Concat(host.Split(Path.GetInvalidFileNameChars()));
        return $"sitecheck-{safeHost}-{report.StartedAt.ToLocalTime():yyyy-MM-dd}";
    }
}
