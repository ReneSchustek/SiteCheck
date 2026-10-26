using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using SiteCheck.App.Services;
using SiteCheck.Application;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;

namespace SiteCheck.App.ViewModels;

/// <summary>
/// Die Haupt-Ansicht: eine Adresse eingeben, prüfen, das Ergebnis sehen. Der Lauf lässt sich
/// abbrechen, das Ergebnis kopieren und als Datei speichern.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "Die CancellationTokenSource gehört nur einem einzelnen Lauf: Sie wird in RunAsync erzeugt und im finally wieder freigegeben und auf null gesetzt. Im Ruhezustand hält das ViewModel nichts Verwerfbares, und als Singleton mit App-Lebensdauer wird es nie disposed.")]
public sealed partial class CheckViewModel(
    ICheckRunnerFactory runnerFactory,
    TimeProvider clock,
    IHistoryStore history,
    IReportExportService export)
    : ObservableObject, ICheckProgress
{
    private readonly ICheckRunnerFactory _runnerFactory = runnerFactory;
    private readonly TimeProvider _clock = clock;
    private readonly IHistoryStore _history = history;
    private readonly IReportExportService _export = export;

    private DispatcherQueue? _dispatcher;
    private CancellationTokenSource? _cancellation;
    private CheckReport? _lastReport;

    /// <summary>Die vom Nutzer eingegebene Adresse.</summary>
    [ObservableProperty]
    public partial string AddressInput { get; set; } = string.Empty;

    /// <summary>Der aktuelle Zustand des Laufs.</summary>
    [ObservableProperty]
    public partial CheckRunState State { get; private set; } = CheckRunState.Idle;

    /// <summary>Freundliche Fehlermeldung bei ungültiger Eingabe; leer, wenn alles passt.</summary>
    [ObservableProperty]
    public partial string ValidationMessage { get; private set; } = string.Empty;

    /// <summary>Fortschritt von 0 bis 1 während des Laufs.</summary>
    [ObservableProperty]
    public partial double Progress { get; private set; }

    /// <summary>Begleittext zum Fortschritt bzw. die Zusammenzählung am Ende.</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = string.Empty;

    /// <summary>Die Web-Befunde des letzten Laufs.</summary>
    public ObservableCollection<FindingViewModel> WebFindings { get; } = [];

    /// <summary>Die Mail-Befunde des letzten Laufs.</summary>
    public ObservableCollection<FindingViewModel> MailFindings { get; } = [];

    /// <summary>Ob gerade ein Lauf läuft (für die Sichtbarkeit von Fortschritt und Abbrechen).</summary>
    public bool IsRunning => State == CheckRunState.Running;

    /// <summary>Ob ein abgeschlossenes Ergebnis vorliegt (für Kopieren/Speichern und die Ergebnisliste).</summary>
    public bool HasResult => State is CheckRunState.Completed && _lastReport is not null;

    /// <summary>Ob die Fehlermeldung zur Eingabe sichtbar ist.</summary>
    public bool HasValidationMessage => !string.IsNullOrEmpty(ValidationMessage);

    partial void OnStateChanged(CheckRunState value)
    {
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(HasResult));
        RunCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        ClearHistoryCommand.NotifyCanExecuteChanged();
    }

    partial void OnValidationMessageChanged(string value) => OnPropertyChanged(nameof(HasValidationMessage));

    /// <summary>Startet einen Prüflauf für die eingegebene Adresse.</summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        ValidationMessage = string.Empty;
        if (!CheckTarget.TryParse(AddressInput, out CheckTarget? target, out TargetParseError error))
        {
            ValidationMessage = DescribeError(error);
            return;
        }

        _dispatcher ??= DispatcherQueue.GetForCurrentThread();
        AddressInput = target.ToString();
        WebFindings.Clear();
        MailFindings.Clear();
        Progress = 0;
        StatusText = "Prüfung läuft …";
        State = CheckRunState.Running;

        _cancellation = new CancellationTokenSource();
        try
        {
            CheckReport report = await _runnerFactory.Create().RunAsync(target, _clock, this, _cancellation.Token);
            _lastReport = report;

            // Mit dem vorigen Lauf vergleichen, bevor dieser abgelegt wird: So lässt sich je
            // Befund zeigen, was neu, behoben oder unverändert ist.
            IReadOnlyList<Finding> previous = await LoadPreviousFindingsAsync(report.Address).ConfigureAwait(true);
            // Beim allerersten Lauf gibt es keinen Vergleich — dann steht alles als unverändert da,
            // nicht als „neu“. Erst ein zweiter Lauf zeigt, was sich bewegt hat.
            IReadOnlyList<ComparedFinding> compared = previous.Count == 0
                ? [.. report.Findings.Select(finding => new ComparedFinding(finding, FindingChange.Unchanged))]
                : ReportComparison.Compare(report.Findings, previous);
            FillFindings(compared);
            StatusText = SummaryText(report.Summary);
            State = CheckRunState.Completed;
            await AppendToHistoryAsync(report).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Prüfung abgebrochen.";
            State = CheckRunState.Cancelled;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            StatusText = "Die Prüfung ist unerwartet fehlgeschlagen: " + ex.Message;
            State = CheckRunState.Error;
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            Progress = 0;
        }
    }

    private bool CanRun() => State != CheckRunState.Running;

    /// <summary>Bricht den laufenden Prüflauf ab.</summary>
    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Cancel() => _cancellation?.Cancel();

    /// <summary>Kopiert den Bericht als Text in die Zwischenablage.</summary>
    [RelayCommand(CanExecute = nameof(HasResult))]
    private void Copy()
    {
        if (_lastReport is not null)
        {
            _export.CopyTextToClipboard(_lastReport);
            StatusText = "Bericht in die Zwischenablage kopiert.";
        }
    }

    /// <summary>Speichert den Bericht als HTML-Datei.</summary>
    [RelayCommand(CanExecute = nameof(HasResult))]
    private async Task SaveAsync()
    {
        if (_lastReport is null)
        {
            return;
        }

        string? path = await _export.SaveHtmlAsync(_lastReport, CancellationToken.None);
        if (path is not null)
        {
            StatusText = "Bericht gespeichert: " + path;
        }
    }

    /// <summary>Leert den gespeicherten Verlauf der aktuell geprüften Seite.</summary>
    [RelayCommand(CanExecute = nameof(HasResult))]
    private async Task ClearHistoryAsync()
    {
        if (_lastReport is null)
        {
            return;
        }

        try
        {
            await _history.ClearAsync(_lastReport.Address, CancellationToken.None).ConfigureAwait(true);
            StatusText = "Verlauf dieser Seite geleert.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText = "Der Verlauf konnte nicht geleert werden: " + ex.Message;
        }
    }

    /// <inheritdoc/>
    void ICheckProgress.Report(string checkId, int completed, int total)
    {
        double fraction = total > 0 ? (double)completed / total : 0;
        // Der Rückruf kommt aus einem Hintergrund-Faden (die Sonden lösen den Kontext mit
        // ConfigureAwait(false)); die Anzeige darf nur auf dem UI-Faden angefasst werden.
        if (_dispatcher is { } dispatcher && !dispatcher.HasThreadAccess)
        {
            _ = dispatcher.TryEnqueue(() => Progress = fraction);
        }
        else
        {
            Progress = fraction;
        }
    }

    private void FillFindings(IReadOnlyList<ComparedFinding> compared)
    {
        foreach (ComparedFinding entry in compared)
        {
            FindingViewModel item = new(entry.Finding, entry.Change);
            if (entry.Finding.Category == CheckCategory.Mail)
            {
                MailFindings.Add(item);
            }
            else
            {
                WebFindings.Add(item);
            }
        }
    }

    private async Task<IReadOnlyList<Finding>> LoadPreviousFindingsAsync(string address)
    {
        try
        {
            IReadOnlyList<HistoryEntry> entries = await _history.LoadAsync(address, CancellationToken.None).ConfigureAwait(true);
            return entries.Count > 0 ? entries[0].Findings : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ohne lesbaren Verlauf gibt es keinen Vergleich — dann gelten alle Befunde als unverändert.
            return [];
        }
    }

    private async Task AppendToHistoryAsync(CheckReport report)
    {
        try
        {
            HistoryEntry entry = new(report.StartedAt, report.Summary, report.Findings);
            await _history.AppendAsync(report.Address, entry, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Der Verlauf ist eine Beigabe; scheitert das Schreiben, bleibt das Ergebnis trotzdem stehen.
            StatusText += " (Verlauf konnte nicht gespeichert werden.)";
        }
    }

    private static string SummaryText(FindingSummary summary) => string.Create(
        CultureInfo.CurrentCulture,
        $"Ergebnis: {summary.OkCount} in Ordnung, {summary.WarningCount} zu verbessern, {summary.CriticalCount} kritisch.");

    private static string DescribeError(TargetParseError error) => error switch
    {
        TargetParseError.Empty => "Bitte zuerst eine Adresse eingeben, z. B. meine-seite.de.",
        TargetParseError.UnsupportedScheme => "Es lassen sich nur http- und https-Adressen prüfen.",
        TargetParseError.ContainsCredentials => "Bitte die Adresse ohne Benutzername und Passwort eingeben.",
        _ => "Das sieht nicht nach einer gültigen Web-Adresse aus. Beispiel: meine-seite.de",
    };
}
