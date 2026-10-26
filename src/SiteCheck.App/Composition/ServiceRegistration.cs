using Microsoft.Extensions.DependencyInjection;
using SiteCheck.App.Services;
using SiteCheck.App.ViewModels;
using SiteCheck.Application;
using SiteCheck.Core.Abstractions;
using SiteCheck.Data.Probes;
using SiteCheck.Data.Storage;

namespace SiteCheck.App.Composition;

/// <summary>
/// Die Composition Root: hier und nur hier werden die Dienste verdrahtet. Alles andere bekommt
/// seine Abhängigkeiten über den Konstruktor.
/// </summary>
public static class ServiceRegistration
{
    /// <summary>Baut den Dienst-Container der Anwendung.</summary>
    /// <returns>Der fertige <see cref="IServiceProvider"/>.</returns>
    public static IServiceProvider Build()
    {
        ServiceCollection services = new();

        _ = services.AddSingleton(TimeProvider.System);

        // Sonden: die HTTP-Sonde hält zwei Clients (mit und ohne Folgen der Umleitung),
        // die Lastsonde einen eigenen mit knapperem Timeout.
        _ = services.AddSingleton<IHttpProbe>(_ => new HttpProbe(
            ProbeHttpClients.CreateFollowing(),
            ProbeHttpClients.CreateNonFollowing()));
        _ = services.AddSingleton<ITlsProbe, TlsProbe>();
        _ = services.AddSingleton<IDnsProbe>(_ => new DnsProbe());
        _ = services.AddSingleton<ILoadProbe>(_ => new LoadProbe(ProbeHttpClients.CreateForLoad()));

        // Prüflauf: eine Factory baut je Lauf einen Runner über einen frischen HTTP-Cache, damit
        // die Prüfungen eines Laufs Seiten-Anfragen teilen, ein neuer Lauf aber frisch abfragt.
        _ = services.AddSingleton<ICheckRunnerFactory>(provider => new CheckRunnerFactory(
            provider.GetRequiredService<IHttpProbe>(),
            provider.GetRequiredService<ITlsProbe>(),
            provider.GetRequiredService<IDnsProbe>(),
            provider.GetRequiredService<TimeProvider>()));
        _ = services.AddSingleton(provider => new StressTestCoordinator(provider.GetRequiredService<ILoadProbe>()));

        // Speicher im Anwendungsdatenordner.
        _ = services.AddSingleton<AppPaths>();
        _ = services.AddSingleton<ISettingsStore, SettingsStore>();
        _ = services.AddSingleton<ISiteStore, SiteStore>();
        _ = services.AddSingleton<IHistoryStore, HistoryStore>();

        // App-Dienste für die Oberfläche.
        _ = services.AddSingleton<IConfirmationService, ConfirmationService>();
        _ = services.AddSingleton<IReportExportService, ReportExportService>();

        // ViewModels als Singletons: In diesem Einzelfenster soll der Zustand (Eingabe,
        // Ergebnis, Verlauf) den Seitenwechsel überstehen, und die Seitenliste muss der
        // Prüf-Ansicht eine Adresse übergeben können.
        _ = services.AddSingleton<CheckViewModel>();
        _ = services.AddSingleton<SitesViewModel>();
        _ = services.AddSingleton<StressTestViewModel>();

        return services.BuildServiceProvider();
    }
}
