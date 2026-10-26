using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using SiteCheck.Core.Models;

namespace SiteCheck.App.ViewModels;

/// <summary>
/// Eine Zeile in „Meine Seiten“: Name, Adresse und eine Ampel, die den schlimmsten Befund des
/// letzten Laufs zeigt. Die Ampel steht nie allein über die Farbe — ein Wort trägt die Bedeutung mit.
/// </summary>
public sealed partial class SiteRowViewModel(Site site) : ObservableObject
{
    /// <summary>Die zugrunde liegende Seite (Name, Adresse).</summary>
    public Site Site { get; } = site;

    /// <summary>Anzeigename.</summary>
    public string Name => Site.Name;

    /// <summary>Adresse der Seite.</summary>
    public string Address => Site.Address;

    /// <summary>Ob die Seite gerade im Sammellauf geprüft wird.</summary>
    [ObservableProperty]
    public partial bool IsChecking { get; private set; }

    /// <summary>Schlimmste Stufe des letzten Laufs; <see langword="null"/>, solange nie geprüft.</summary>
    [ObservableProperty]
    public partial Severity? LastWorst { get; private set; }

    /// <summary>Zeitpunkt des letzten Laufs; leer, solange nie geprüft.</summary>
    [ObservableProperty]
    public partial string LastRunText { get; private set; } = string.Empty;

    /// <summary>Das Wort zur Ampel.</summary>
    public string AmpelWord => LastWorst switch
    {
        null => "Noch nicht geprüft",
        Severity.Ok => "In Ordnung",
        Severity.Info => "Hinweis",
        Severity.Warning => "Zu verbessern",
        Severity.Critical => "Kritisch",
        _ => "Unbekannt",
    };

    /// <summary>Die Ampelfarbe; Grau, solange nie geprüft.</summary>
    public Brush AmpelBrush => LastWorst switch
    {
        Severity.Ok => Resolve("StatusOkBrush"),
        Severity.Info => Resolve("StatusInfoBrush"),
        Severity.Warning => Resolve("StatusWarningBrush"),
        Severity.Critical => Resolve("StatusCriticalBrush"),
        _ => new SolidColorBrush(Colors.Gray),
    };

    partial void OnLastWorstChanged(Severity? value)
    {
        OnPropertyChanged(nameof(AmpelWord));
        OnPropertyChanged(nameof(AmpelBrush));
    }

    /// <summary>Setzt die Ampel aus dem Ergebnis eines Laufs.</summary>
    /// <param name="summary">Die Zusammenzählung des Laufs.</param>
    /// <param name="ranAt">Zeitpunkt des Laufs.</param>
    public void ApplyResult(FindingSummary summary, DateTimeOffset ranAt)
    {
        ArgumentNullException.ThrowIfNull(summary);
        LastWorst = summary.WorstSeverity;
        LastRunText = "Geprüft am " + ranAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("de-DE"));
    }

    /// <summary>Markiert den Beginn bzw. das Ende einer Prüfung dieser Zeile.</summary>
    /// <param name="checking">Ob gerade geprüft wird.</param>
    public void SetChecking(bool checking) => IsChecking = checking;

    private static Brush Resolve(string key)
    {
        Microsoft.UI.Xaml.Application? app = Microsoft.UI.Xaml.Application.Current;
        if (app is not null && app.Resources.TryGetValue(key, out object? value) && value is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Colors.Gray);
    }
}
