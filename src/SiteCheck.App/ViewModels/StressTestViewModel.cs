using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using SiteCheck.App.Services;
using SiteCheck.Application;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Stress;

namespace SiteCheck.App.ViewModels;

/// <summary>
/// Die Lasttest-Ansicht. Prüft eine eigene Seite unter Last — aber erst, nachdem der Nutzer
/// ausdrücklich bestätigt hat, dass die Seite ihm gehört und selbst gehostet ist. Die
/// Obergrenzen für Parallelität und Dauer sind im Kern fest verdrahtet; die Regler hier
/// bewegen sich nur innerhalb dieser Grenzen.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "Die CancellationTokenSource gehört nur einem einzelnen Lauf: Sie wird in StartAsync erzeugt und im finally wieder freigegeben und auf null gesetzt. Als Singleton mit App-Lebensdauer wird das ViewModel nie disposed.")]
public sealed partial class StressTestViewModel(StressTestCoordinator coordinator, IConfirmationService confirmation)
    : ObservableObject
{
    private readonly StressTestCoordinator _coordinator = coordinator;
    private readonly IConfirmationService _confirmation = confirmation;

    private DispatcherQueue? _dispatcher;
    private CancellationTokenSource? _cancellation;

    /// <summary>Kleinste einstellbare Parallelität.</summary>
    public int MinConcurrency => StressTestOptions.MinConcurrency;

    /// <summary>Größte einstellbare Parallelität (fest verdrahtet).</summary>
    public int MaxConcurrency => StressTestOptions.MaxConcurrency;

    /// <summary>Kürzeste einstellbare Dauer in Sekunden.</summary>
    public int MinDuration => StressTestOptions.MinDurationSeconds;

    /// <summary>Längste einstellbare Dauer in Sekunden (fest verdrahtet).</summary>
    public int MaxDuration => StressTestOptions.MaxDurationSeconds;

    /// <summary>Die zu testende Adresse.</summary>
    [ObservableProperty]
    public partial string AddressInput { get; set; } = string.Empty;

    /// <summary>Gewählte Zahl gleichzeitiger Anfragen.</summary>
    [ObservableProperty]
    public partial double Concurrency { get; set; } = StressTestOptions.Default.Concurrency;

    /// <summary>Gewählte Dauer in Sekunden.</summary>
    [ObservableProperty]
    public partial double DurationSeconds { get; set; } = StressTestOptions.Default.Duration.TotalSeconds;

    /// <summary>Zustand des Laufs.</summary>
    [ObservableProperty]
    public partial CheckRunState State { get; private set; } = CheckRunState.Idle;

    /// <summary>Freundliche Fehlermeldung bei ungültiger Eingabe.</summary>
    [ObservableProperty]
    public partial string ValidationMessage { get; private set; } = string.Empty;

    /// <summary>Fortschritt von 0 bis 1 während des Laufs.</summary>
    [ObservableProperty]
    public partial double Progress { get; private set; }

    /// <summary>Begleittext: während des Laufs die Anzahl Anfragen, danach das Ergebnis.</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = string.Empty;

    /// <summary>Das Ergebnis des letzten Laufs als mehrzeilige Zusammenfassung; leer, solange keins vorliegt.</summary>
    [ObservableProperty]
    public partial string ResultText { get; private set; } = string.Empty;

    /// <summary>Ob gerade ein Lauf läuft.</summary>
    public bool IsRunning => State == CheckRunState.Running;

    /// <summary>Ob eine Fehlermeldung zur Eingabe sichtbar ist.</summary>
    public bool HasValidationMessage => !string.IsNullOrEmpty(ValidationMessage);

    /// <summary>Ob ein Ergebnis vorliegt.</summary>
    public bool HasResult => !string.IsNullOrEmpty(ResultText);

    partial void OnStateChanged(CheckRunState value)
    {
        OnPropertyChanged(nameof(IsRunning));
        StartCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    partial void OnValidationMessageChanged(string value) => OnPropertyChanged(nameof(HasValidationMessage));

    partial void OnResultTextChanged(string value) => OnPropertyChanged(nameof(HasResult));

    /// <summary>Startet den Lasttest nach der Eigentums-Bestätigung.</summary>
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        ValidationMessage = string.Empty;
        if (!CheckTarget.TryParse(AddressInput, out CheckTarget? target, out TargetParseError error))
        {
            ValidationMessage = DescribeError(error);
            return;
        }

        AddressInput = target.ToString();

        // Vor jedem Lauf frisch bestätigen lassen; ohne „Ja“ passiert nichts.
        bool owns = await _confirmation.ConfirmStressTestOwnershipAsync(target.ToString(), CancellationToken.None);
        if (!owns)
        {
            StatusText = "Abgebrochen — ohne Bestätigung kein Lasttest.";
            return;
        }

        _dispatcher ??= DispatcherQueue.GetForCurrentThread();
        ResultText = string.Empty;
        Progress = 0;
        StatusText = "Lasttest läuft …";
        State = CheckRunState.Running;

        StressTestOptions options = new((int)Concurrency, TimeSpan.FromSeconds(DurationSeconds));
        Progress<StressTestProgress> progress = new(OnProgress);
        _cancellation = new CancellationTokenSource();
        try
        {
            StressTestResult result = await _coordinator.RunAsync(target, options, ownershipConfirmed: true, progress, _cancellation.Token);
            ResultText = DescribeResult(result);
            StatusText = "Lasttest abgeschlossen.";
            State = CheckRunState.Completed;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Lasttest abgebrochen.";
            State = CheckRunState.Cancelled;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            StatusText = "Der Lasttest ist fehlgeschlagen: " + ex.Message;
            State = CheckRunState.Error;
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            Progress = 0;
        }
    }

    private bool CanStart() => State != CheckRunState.Running;

    /// <summary>Bricht den laufenden Lasttest ab.</summary>
    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Cancel() => _cancellation?.Cancel();

    private void OnProgress(StressTestProgress update)
    {
        // Progress<T> ruft auf dem erzeugenden (UI-)Kontext zurück, solange dieser existiert;
        // zur Sicherheit über den Dispatcher, falls er doch einmal auf einem anderen Faden kommt.
        void Apply()
        {
            Progress = update.Fraction;
            StatusText = string.Create(CultureInfo.CurrentCulture, $"Lasttest läuft … {update.CompletedRequests} Anfragen");
        }

        if (_dispatcher is { } dispatcher && !dispatcher.HasThreadAccess)
        {
            _ = dispatcher.TryEnqueue(Apply);
        }
        else
        {
            Apply();
        }
    }

    private static string DescribeResult(StressTestResult result) => string.Create(
        CultureInfo.CurrentCulture,
        $"""
         {result.TotalRequests} Anfragen in {result.Elapsed.TotalSeconds:0.#} Sekunden
         {result.RequestsPerSecond:0.#} Anfragen pro Sekunde
         Erfolgreich: {result.SuccessCount}, fehlgeschlagen: {result.FailureCount} ({result.FailureRate:P0})
         Antwortzeit im Mittel: {result.AverageMilliseconds:0} ms
         Median: {result.MedianMilliseconds:0} ms, langsamstes Fünftel (95 %): {result.Percentile95Milliseconds:0} ms
         """);

    private static string DescribeError(TargetParseError error) => error switch
    {
        TargetParseError.Empty => "Bitte zuerst die Adresse deiner Seite eingeben.",
        TargetParseError.UnsupportedScheme => "Es lassen sich nur http- und https-Adressen testen.",
        TargetParseError.ContainsCredentials => "Bitte die Adresse ohne Benutzername und Passwort eingeben.",
        _ => "Das sieht nicht nach einer gültigen Web-Adresse aus. Beispiel: meine-seite.de",
    };
}
