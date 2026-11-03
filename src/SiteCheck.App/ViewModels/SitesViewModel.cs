using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SiteCheck.App.Services;
using SiteCheck.Application;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;

namespace SiteCheck.App.ViewModels;

/// <summary>
/// Verwaltet die Liste der eigenen Seiten, die man regelmäßig prüft. Rein optional: Geprüft
/// werden kann jede Adresse direkt in der Prüf-Ansicht; diese Liste ist eine Bequemlichkeit für
/// wiederkehrende Seiten. „Alle prüfen“ läuft sie der Reihe nach durch und setzt je Seite eine
/// Ampel aus dem schlimmsten Befund. Gespeichert werden nur Name und Adresse.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "Die CancellationTokenSource gehört nur dem laufenden Sammellauf: in CheckAllAsync erzeugt, im finally freigegeben und auf null gesetzt. Als Singleton mit App-Lebensdauer wird das ViewModel nie disposed.")]
public sealed partial class SitesViewModel(
    ISiteStore store,
    IHistoryStore history,
    ICheckRunnerFactory runnerFactory,
    TimeProvider clock,
    HostRequestGuard guard,
    IConfirmationService confirmation)
    : ObservableObject
{
    private readonly ISiteStore _store = store;
    private readonly IHistoryStore _history = history;
    private readonly ICheckRunnerFactory _runnerFactory = runnerFactory;
    private readonly TimeProvider _clock = clock;
    private readonly HostRequestGuard _guard = guard;
    private readonly IConfirmationService _confirmation = confirmation;

    private CancellationTokenSource? _cancellation;

    /// <summary>Die gespeicherten Seiten mit ihrer Ampel.</summary>
    public ObservableCollection<SiteRowViewModel> Sites { get; } = [];

    /// <summary>Wird ausgelöst, wenn der Nutzer eine einzelne Seite in der Prüf-Ansicht prüfen möchte.</summary>
    public event EventHandler<CheckRequestedEventArgs>? CheckRequested;

    /// <summary>Name für eine neu hinzuzufügende Seite.</summary>
    [ObservableProperty]
    public partial string NewName { get; set; } = string.Empty;

    /// <summary>Adresse für eine neu hinzuzufügende Seite.</summary>
    [ObservableProperty]
    public partial string NewAddress { get; set; } = string.Empty;

    /// <summary>Freundliche Fehlermeldung beim Hinzufügen; leer, wenn alles passt.</summary>
    [ObservableProperty]
    public partial string ValidationMessage { get; private set; } = string.Empty;

    /// <summary>Ob gerade ein Sammellauf läuft.</summary>
    [ObservableProperty]
    public partial bool IsCheckingAll { get; private set; }

    /// <summary>Begleittext während des Sammellaufs.</summary>
    [ObservableProperty]
    public partial string CheckAllStatus { get; private set; } = string.Empty;

    /// <summary>Ob die Liste leer ist (für den Leerzustand in der Ansicht).</summary>
    public bool IsEmpty => Sites.Count == 0;

    /// <summary>Ob eine Fehlermeldung sichtbar ist.</summary>
    public bool HasValidationMessage => !string.IsNullOrEmpty(ValidationMessage);

    partial void OnValidationMessageChanged(string value) => OnPropertyChanged(nameof(HasValidationMessage));

    partial void OnIsCheckingAllChanged(bool value)
    {
        CheckAllCommand.NotifyCanExecuteChanged();
        CancelAllCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Lädt die gespeicherten Seiten und ihre zuletzt bekannte Ampel aus dem Verlauf.</summary>
    /// <param name="cancellationToken">Bricht das Laden ab.</param>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Site> loaded = await _store.LoadAsync(cancellationToken).ConfigureAwait(true);
        Sites.Clear();
        foreach (Site site in loaded)
        {
            SiteRowViewModel row = new(site);
            await ApplyLastRunAsync(row, cancellationToken).ConfigureAwait(true);
            Sites.Add(row);
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>Fügt die eingegebene Seite hinzu und speichert die Liste.</summary>
    [RelayCommand]
    private async Task AddAsync()
    {
        ValidationMessage = string.Empty;
        if (!CheckTarget.TryParse(NewAddress, out CheckTarget? target, out _))
        {
            ValidationMessage = "Bitte eine gültige Adresse eingeben, z. B. meine-seite.de.";
            return;
        }

        string address = target.ToString();
        if (Sites.Any(row => string.Equals(row.Address, address, StringComparison.OrdinalIgnoreCase)))
        {
            ValidationMessage = "Diese Adresse ist bereits in der Liste.";
            return;
        }

        string name = string.IsNullOrWhiteSpace(NewName) ? target.Host : NewName.Trim();
        Sites.Add(new SiteRowViewModel(new Site(name, address)));
        NewName = string.Empty;
        NewAddress = string.Empty;
        OnPropertyChanged(nameof(IsEmpty));
        await SaveAsync().ConfigureAwait(true);
    }

    /// <summary>Entfernt eine Seite aus der Liste.</summary>
    /// <param name="row">Die zu entfernende Zeile.</param>
    [RelayCommand]
    private async Task RemoveAsync(SiteRowViewModel? row)
    {
        if (row is null || !Sites.Remove(row))
        {
            return;
        }

        OnPropertyChanged(nameof(IsEmpty));
        await SaveAsync().ConfigureAwait(true);
    }

    /// <summary>Bittet darum, die gewählte Seite in der Prüf-Ansicht zu prüfen.</summary>
    /// <param name="row">Die zu prüfende Zeile.</param>
    [RelayCommand]
    private void Check(SiteRowViewModel? row)
    {
        if (row is not null)
        {
            CheckRequested?.Invoke(this, new CheckRequestedEventArgs(row.Address));
        }
    }

    /// <summary>Prüft alle gespeicherten Seiten der Reihe nach und setzt je Seite die Ampel.</summary>
    [RelayCommand(CanExecute = nameof(CanCheckAll))]
    private async Task CheckAllAsync()
    {
        IsCheckingAll = true;
        _cancellation = new CancellationTokenSource();
        CancellationToken token = _cancellation.Token;
        try
        {
            SiteRowViewModel[] rows = [.. Sites];
            if (!await ConfirmNewHostsAsync(rows, token).ConfigureAwait(true))
            {
                CheckAllStatus = "Abgebrochen — es wurde nichts gesendet.";
                return;
            }

            for (int index = 0; index < rows.Length; index++)
            {
                token.ThrowIfCancellationRequested();
                SiteRowViewModel row = rows[index];
                CheckAllStatus = string.Create(CultureInfo.CurrentCulture, $"Prüfe {index + 1} von {rows.Length}: {row.Name}");

                // Die Sperrwache hält den Abstand über alle Seiten. Weist eine Seite ab, endet der ganze
                // Lauf: Die übrigen liegen oft auf demselben Server, und gesperrt wird der Anschluss.
                if (!await CheckOneAsync(row, token).ConfigureAwait(true))
                {
                    CheckAllStatus = $"Sammellauf angehalten bei {row.Name}, um deinen Anschluss zu schützen. Mehr dazu in der Prüf-Ansicht.";
                    return;
                }
            }

            CheckAllStatus = "Alle Seiten geprüft.";
        }
        catch (OperationCanceledException)
        {
            CheckAllStatus = "Sammellauf abgebrochen.";
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            IsCheckingAll = false;
        }
    }

    private bool CanCheckAll() => !IsCheckingAll && Sites.Count > 0;

    /// <summary>Bricht den laufenden Sammellauf ab.</summary>
    [RelayCommand(CanExecute = nameof(IsCheckingAll))]
    private void CancelAll() => _cancellation?.Cancel();

    private async Task<bool> ConfirmNewHostsAsync(SiteRowViewModel[] rows, CancellationToken token)
    {
        List<string> unacknowledged = [];
        foreach (SiteRowViewModel row in rows)
        {
            if (CheckTarget.TryParse(row.Address, out CheckTarget? target, out _)
                && !(await _guard.GetStatusAsync(target.Host, token).ConfigureAwait(true)).NoticeAcknowledged)
            {
                unacknowledged.Add(row.Address);
            }
        }

        if (unacknowledged.Count == 0)
        {
            return true;
        }

        if (!await _confirmation.ConfirmHostNoticeAsync(unacknowledged, token).ConfigureAwait(true))
        {
            return false;
        }

        foreach (string address in unacknowledged)
        {
            if (CheckTarget.TryParse(address, out CheckTarget? target, out _))
            {
                await _guard.AcknowledgeNoticeAsync(target.Host, token).ConfigureAwait(true);
            }
        }

        return true;
    }

    // Liefert false, wenn der Lauf angehalten wurde; die Ampel behält dann ihren letzten Stand.
    private async Task<bool> CheckOneAsync(SiteRowViewModel row, CancellationToken token)
    {
        if (!CheckTarget.TryParse(row.Address, out CheckTarget? target, out _))
        {
            return true;
        }

        row.SetChecking(true);
        try
        {
            CheckReport report = await _runnerFactory.Create(CheckRunOptions.Standard).RunAsync(target, _clock, progress: null, token).ConfigureAwait(true);
            if (report.WasHalted)
            {
                return false;
            }

            row.ApplyResult(report.Summary, report.StartedAt);
            await AppendHistoryAsync(report).ConfigureAwait(true);
            return true;
        }
        finally
        {
            row.SetChecking(false);
        }
    }

    private async Task ApplyLastRunAsync(SiteRowViewModel row, CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<HistoryEntry> entries = await _history.LoadAsync(row.Address, cancellationToken).ConfigureAwait(true);
            if (entries.Count > 0)
            {
                row.ApplyResult(entries[0].Summary, entries[0].RanAt);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ein unlesbarer Verlauf lässt die Zeile einfach „noch nicht geprüft“ bleiben.
        }
    }

    private async Task AppendHistoryAsync(CheckReport report)
    {
        try
        {
            HistoryEntry entry = new(report.StartedAt, report.Summary, report.Findings);
            await _history.AppendAsync(report.Address, entry, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Der Verlauf ist eine Beigabe; sein Scheitern darf den Sammellauf nicht stören.
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            await _store.SaveAsync([.. Sites.Select(row => row.Site)], CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ValidationMessage = "Die Liste konnte nicht gespeichert werden: " + ex.Message;
        }
    }
}
