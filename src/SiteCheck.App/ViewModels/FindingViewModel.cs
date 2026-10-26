using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using SiteCheck.Core.Models;

namespace SiteCheck.App.ViewModels;

/// <summary>
/// Stellt einen Befund für die Anzeige dar: Titel, Detail und Empfehlung, dazu ein Wort und
/// eine Farbe je Stufe. Die Farbe steht nie allein — das Wort trägt die Bedeutung auch ohne sie.
/// </summary>
public sealed class FindingViewModel(Finding finding, FindingChange change = FindingChange.Unchanged)
{
    private readonly Finding _finding = finding;

    /// <summary>Wie der Befund gegenüber dem letzten Lauf dasteht (neu, behoben, unverändert).</summary>
    public FindingChange Change { get; } = change;

    /// <summary>Kurzes Wort zur Veränderung; leer, wenn unverändert.</summary>
    public string ChangeLabel => Change switch
    {
        FindingChange.New => "neu",
        FindingChange.Resolved => "behoben",
        _ => string.Empty,
    };

    /// <summary>Ob eine Veränderung angezeigt werden soll.</summary>
    public bool HasChangeLabel => Change != FindingChange.Unchanged;

    /// <summary>Ein klarer Satz, was festgestellt wurde.</summary>
    public string Title => _finding.Title;

    /// <summary>Ob der Befund nur aus Indizien geschlossen ist (von außen nicht sicher feststellbar).</summary>
    public bool IsInferred => _finding.Confidence == Confidence.Inferred;

    /// <summary>Das Wort zur Stufe, z. B. „Kritisch“.</summary>
    public string SeverityWord => _finding.Severity switch
    {
        Severity.Ok => "In Ordnung",
        Severity.Info => "Hinweis",
        Severity.Warning => "Zu verbessern",
        Severity.Critical => "Kritisch",
        _ => "Unbekannt",
    };

    /// <summary>Begründung oder gefundene Werte; leer, wenn es nichts zu ergänzen gibt.</summary>
    public string Detail => _finding.Detail ?? string.Empty;

    /// <summary>Ob eine Begründung vorliegt.</summary>
    public bool HasDetail => !string.IsNullOrWhiteSpace(_finding.Detail);

    /// <summary>Der Rat, was zu tun ist; leer, wenn der Befund in Ordnung ist.</summary>
    public string Advice => _finding.Recommendation?.Advice ?? string.Empty;

    /// <summary>Ob ein Rat vorliegt.</summary>
    public bool HasAdvice => _finding.Recommendation is not null;

    /// <summary>Ein Beispiel zum Übernehmen (Konfigurationszeile, DNS-Eintrag); leer, wenn keins da ist.</summary>
    public string Example => _finding.Recommendation?.Example ?? string.Empty;

    /// <summary>Ob ein Beispiel vorliegt.</summary>
    public bool HasExample => !string.IsNullOrWhiteSpace(_finding.Recommendation?.Example);

    /// <summary>Die Farbe des linken Streifens je Stufe, aus der Palette.</summary>
    public Brush AccentBrush => ResolveBrush(_finding.Severity switch
    {
        Severity.Ok => "StatusOkBrush",
        Severity.Info => "StatusInfoBrush",
        Severity.Warning => "StatusWarningBrush",
        Severity.Critical => "StatusCriticalBrush",
        _ => "StatusInfoBrush",
    });

    private static Brush ResolveBrush(string key)
    {
        // Außerhalb einer laufenden App (z. B. im Test) gibt es keine Application-Ressourcen.
        Microsoft.UI.Xaml.Application? app = Microsoft.UI.Xaml.Application.Current;
        if (app is not null && app.Resources.TryGetValue(key, out object? value) && value is Brush brush)
        {
            return brush;
        }

        // Fallback, falls das Wörterbuch den Schlüssel nicht führt.
        return new SolidColorBrush(Colors.Gray);
    }
}
