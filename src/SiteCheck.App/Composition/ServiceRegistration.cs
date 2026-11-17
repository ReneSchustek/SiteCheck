using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SiteCheck.App.Services;
using SiteCheck.App.ViewModels;
using SiteCheck.Application;
using SiteCheck.Core.Abstractions;
using SiteCheck.Data.Demo;
using SiteCheck.Data.Logging;
using SiteCheck.Data.Probes;
using SiteCheck.Data.Storage;
using SiteCheck.Data.Update;

namespace SiteCheck.App.Composition;

/// <summary>
/// Die Composition Root: hier und nur hier werden die Dienste verdrahtet. Alles andere bekommt
/// seine Abhängigkeiten über den Konstruktor.
/// </summary>
public static class ServiceRegistration
{
    /// <summary>Baut den Dienst-Container der Anwendung.</summary>
    /// <param name="mode">Echter Betrieb oder Vorführung.</param>
    /// <param name="paths">Wo die Daten liegen; in der Vorführung ein eigener Ordner.</param>
    /// <returns>Der fertige <see cref="IServiceProvider"/>.</returns>
    public static IServiceProvider Build(AppRunMode mode, AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(mode);
        ArgumentNullException.ThrowIfNull(paths);
        ServiceCollection services = new();

        _ = services.AddSingleton(TimeProvider.System);
        _ = services.AddSingleton(mode);
        _ = services.AddSingleton(paths);
        AddProbes(services, mode);

        // Prüflauf: eine Factory baut je Lauf einen Runner über einen frischen HTTP-Cache, damit
        // die Prüfungen eines Laufs Seiten-Anfragen teilen, ein neuer Lauf aber frisch abfragt.
        _ = services.AddSingleton<ICheckRunnerFactory>(provider => new CheckRunnerFactory(
            provider.GetRequiredService<IHttpProbe>(),
            provider.GetRequiredService<ITlsProbe>(),
            provider.GetRequiredService<IDnsProbe>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<HostRequestGuard>(),
            provider.GetRequiredService<ILogger<CheckRunner>>()));
        _ = services.AddSingleton(provider => new StressTestCoordinator(
            provider.GetRequiredService<ILoadProbe>(),
            provider.GetRequiredService<HostRequestGuard>(),
            provider.GetRequiredService<ILogger<StressTestCoordinator>>()));

        // Genau eine Sperrwache im Prozess: Prüflauf, Sammellauf und Lasttest teilen sich ihre Grenzen.
        _ = services.AddSingleton<IRequestGuardStore, RequestGuardStore>();
        _ = services.AddSingleton(provider => new HostRequestGuard(
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<IRequestGuardStore>(),
            provider.GetRequiredService<IDnsProbe>(),
            provider.GetRequiredService<ILogger<HostRequestGuard>>()));

        // Speicher im Anwendungsdatenordner.
        _ = services.AddSingleton<ISettingsStore, SettingsStore>();
        _ = services.AddSingleton<ISiteStore, SiteStore>();
        _ = services.AddSingleton<IHistoryStore, HistoryStore>();

        // Das Protokoll liegt neben den übrigen Daten und bleibt lesbar, wenn das Programm nicht startet.
        _ = services.AddLogging(logging => logging.AddProvider(new FileLoggerProvider(paths.LogDirectory, TimeProvider.System)));

        AddUpdateSources(services, mode);
        _ = services.AddSingleton(provider => new UpdateInstallerService(
            provider.GetRequiredService<IHttpGetClient>(),
            provider.GetRequiredService<ILogger<UpdateInstallerService>>()));
        _ = services.AddSingleton(provider => new UpdateOrchestrator(
            provider.GetRequiredService<UpdateInstallerService>(),
            provider.GetRequiredService<IUpdateChecker>(),
            provider.GetRequiredService<ISettingsStore>(),
            provider.GetRequiredService<TimeProvider>(),
            CurrentVersion(),
            provider.GetRequiredService<ILogger<UpdateOrchestrator>>()));

        // App-Dienste für die Oberfläche.
        _ = services.AddSingleton<IStringResources, ReswStringResources>();
        _ = services.AddSingleton<IConfirmationService, ConfirmationService>();
        _ = services.AddSingleton<IReportExportService, ReportExportService>();

        // ViewModels als Singletons: In diesem Einzelfenster soll der Zustand (Eingabe,
        // Ergebnis, Verlauf) den Seitenwechsel überstehen, und die Seitenliste muss der
        // Prüf-Ansicht eine Adresse übergeben können.
        _ = services.AddSingleton<ILogReader>(provider => new LogFileReader(provider.GetRequiredService<AppPaths>().LogDirectory));
        _ = services.AddSingleton<CheckViewModel>();
        _ = services.AddSingleton<LogViewModel>();
        _ = services.AddSingleton<UpdateViewModel>();
        _ = services.AddSingleton<SitesViewModel>();
        _ = services.AddSingleton<StressTestViewModel>();

        return services.BuildServiceProvider();
    }

    // Sonden: die HTTP-Sonde hält zwei Clients (mit und ohne Folgen der Umleitung), die Lastsonde
    // einen eigenen mit knapperem Timeout. Die Vorführung tauscht alle vier gegen feste Antworten;
    // die Prüfungen darüber bleiben dieselben.
    private static void AddProbes(ServiceCollection services, AppRunMode mode)
    {
        if (mode.IsDemo)
        {
            _ = services.AddSingleton<IHttpProbe>(_ => new DemoHttpProbe(DemoEra.Current));
            _ = services.AddSingleton<ITlsProbe>(provider => new DemoTlsProbe(provider.GetRequiredService<TimeProvider>()));
            _ = services.AddSingleton<IDnsProbe>(_ => new DemoDnsProbe(DemoEra.Current));
            _ = services.AddSingleton<ILoadProbe>(_ => new DemoLoadProbe());
            return;
        }

        _ = services.AddSingleton<IHttpProbe>(_ => new HttpProbe(
            ProbeHttpClients.CreateFollowing(),
            ProbeHttpClients.CreateNonFollowing()));
        _ = services.AddSingleton<ITlsProbe, TlsProbe>();
        _ = services.AddSingleton<IDnsProbe>(_ => new DnsProbe());
        _ = services.AddSingleton<ILoadProbe>(_ => new LoadProbe(ProbeHttpClients.CreateForLoad()));
    }

    // Selbst-Update. Die Version kommt aus der Assembly, damit Anzeige und Vergleich nie
    // auseinanderlaufen. In der Vorführung fragt niemand bei GitHub nach.
    private static void AddUpdateSources(ServiceCollection services, AppRunMode mode)
    {
        if (mode.IsDemo)
        {
            _ = services.AddSingleton<IHttpGetClient, OfflineHttpGetClient>();
            _ = services.AddSingleton<IUpdateChecker>(_ => new DemoUpdateChecker(CurrentVersion()));
            return;
        }

        _ = services.AddSingleton<IHttpGetClient, HttpGetClient>();
        _ = services.AddSingleton<IUpdateChecker>(provider => new GitHubUpdateChecker(
            provider.GetRequiredService<IHttpGetClient>(),
            CurrentVersion(),
            provider.GetRequiredService<ILogger<GitHubUpdateChecker>>()));
    }

    private static Version CurrentVersion() => typeof(ServiceRegistration).Assembly.GetName().Version ?? new Version(0, 0, 0);
}
