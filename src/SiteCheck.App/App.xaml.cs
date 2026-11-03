using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using SiteCheck.App.Composition;
using SiteCheck.App.Services;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models.Update;

namespace SiteCheck.App;

/// <summary>
/// Einstiegspunkt der Anwendung. Baut den Dienst-Container, zeigt den Startbildschirm, öffnet
/// das Hauptfenster und zeigt beim ersten Start den Hinweis zur Nutzungsgrenze.
/// </summary>
[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "WinUI-3 Application-Klasse ist der Einstiegspunkt und muss public sein.")]
[SuppressMessage("Naming", "CA1724:Type names should not match namespaces",
    Justification = "Der WinUI-3-Einstiegspunkt heißt per Konvention 'App' im 'SiteCheck.App'-Namespace.")]
public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? _splash;

    /// <summary>Erstellt die Anwendung und den Dienst-Container.</summary>
    public App()
    {
        InitializeComponent();
        Services = ServiceRegistration.Build();
    }

    /// <summary>Der Dienst-Container der Anwendung.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>Das Hauptfenster, sobald es offen ist. Wird für die Dateiauswahl (Fensterhandle) gebraucht.</summary>
    public static Window? MainWindow { get; private set; }

    /// <summary>Die Zeichenwurzel des Hauptfensters, für Dialoge.</summary>
    public static Microsoft.UI.Xaml.XamlRoot? MainWindowRoot => (MainWindow?.Content as FrameworkElement)?.XamlRoot;

    /// <summary>Wird beim Start aufgerufen.</summary>
    /// <param name="args">Startparameter.</param>
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            await StartAsync();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // Eine Ausnahme aus einem async-void-Handler bliebe sonst unbeobachtet; das Protokoll hält sie fest.
            Services.GetRequiredService<ILogger<App>>().StartFailed(ex);
            throw;
        }
    }

    private async Task StartAsync()
    {
        UpdateOrchestrator updates = Services.GetRequiredService<UpdateOrchestrator>();

        // Aus dem Staging-Ordner gestartet: nur die Dateien tauschen, kein Fenster öffnen.
        if (UpdateApplyArgs.TryParse(Environment.GetCommandLineArgs(), out UpdateApplyArgs helper))
        {
            await updates.RunHelperApplyAsync(helper);
            return;
        }

        // Ein vorbereitetes Update kommt vor dem ersten Fenster, sonst sperrt dieser Prozess die Dateien.
        if (await updates.TryApplyPendingUpdateOnStartupAsync())
        {
            Exit();
            return;
        }

        _splash = new SplashWindow();
        _splash.Activate();

        MainWindow = new MainWindow();
        MainWindow.Activate();

        _splash.Close();
        _splash = null;

        // Der Blick auf neue Versionen darf das Öffnen des Fensters nicht aufhalten.
        _ = CheckForUpdatesAsync(updates);
    }

    private static async Task CheckForUpdatesAsync(UpdateOrchestrator updates)
    {
        try
        {
            await updates.CheckAtStartupAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // Ein Fehler beim Nachsehen ist kein Grund, das laufende Programm zu stören.
            Services.GetRequiredService<ILogger<App>>().UpdateCheckFailed(ex);
        }
    }

    /// <summary>Löst den Erststart-Hinweis aus, sobald das Fenster bereit ist.</summary>
    internal static async Task ShowFirstRunNoticeIfNeededAsync()
    {
        ISettingsStore settings = Services.GetRequiredService<ISettingsStore>();
        Core.Models.AppSettings current = await settings.LoadAsync(CancellationToken.None).ConfigureAwait(true);
        if (current.ScopeNoticeAcknowledged)
        {
            return;
        }

        await Services.GetRequiredService<IConfirmationService>()
            .ShowScopeNoticeAsync(CancellationToken.None).ConfigureAwait(true);
        await settings.SaveAsync(current with { ScopeNoticeAcknowledged = true }, CancellationToken.None).ConfigureAwait(true);
    }
}
