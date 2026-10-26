using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using SiteCheck.App.Composition;
using SiteCheck.App.Services;
using SiteCheck.Core.Abstractions;

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
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _splash = new SplashWindow();
        _splash.Activate();

        MainWindow = new MainWindow();
        MainWindow.Activate();

        _splash.Close();
        _splash = null;
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
