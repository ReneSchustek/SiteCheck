using Microsoft.Extensions.DependencyInjection;
using SiteCheck.Application.Demo;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;
using SiteCheck.Data.Demo;
using SiteCheck.Data.Logging;
using SiteCheck.Data.Storage;

namespace SiteCheck.App.Composition;

/// <summary>Wie das Programm läuft: echt oder als Vorführung mit Beispieldaten.</summary>
/// <param name="IsDemo">Vorführung: feste Antworten statt Netz, eigener Datenordner.</param>
public sealed record AppRunMode(bool IsDemo)
{
    /// <summary>Der gewöhnliche Betrieb.</summary>
    public static AppRunMode Normal { get; } = new(IsDemo: false);
}

/// <summary>
/// Der Vorführmodus für Bildschirmfotos und Vorstellungen. Er startet mit <c>--demo</c>, stellt
/// keine einzige Netzanfrage und arbeitet in einem eigenen Ordner, der bei jedem Start neu
/// angelegt wird. Seitenliste, Verlauf und Sperrwache des echten Betriebs bleiben unberührt.
/// </summary>
internal static class DemoMode
{
    /// <summary>Der Startschalter.</summary>
    internal const string Switch = "--demo";

    private static readonly TimeSpan[] MainSiteRunsAgo = [TimeSpan.FromDays(13), TimeSpan.FromDays(6)];
    private static readonly TimeSpan ShopRunAgo = TimeSpan.FromDays(3);
    private static readonly TimeSpan BlogRunAgo = TimeSpan.FromDays(2);

    /// <summary>Ob der Startschalter gesetzt ist. Das erste Argument ist der Programmpfad.</summary>
    /// <param name="arguments">Die Befehlszeile, wie <see cref="Environment.GetCommandLineArgs"/> sie liefert.</param>
    /// <returns><see langword="true"/> bei <c>--demo</c>.</returns>
    internal static bool IsRequested(IReadOnlyList<string> arguments) =>
        arguments.Skip(1).Any(argument => string.Equals(argument, Switch, StringComparison.OrdinalIgnoreCase));

    /// <summary>Der Datenordner der Vorführung, getrennt vom echten.</summary>
    /// <param name="normal">Die Pfade des echten Betriebs.</param>
    /// <returns>Pfade unter <c>…\SiteCheck\demo</c>.</returns>
    internal static AppPaths PathsBeside(AppPaths normal) => new(Path.Combine(normal.RootDirectory, "demo"));

    /// <summary>
    /// Leert den Vorführordner. Jede Vorführung beginnt gleich: frische Sperrwache, Hinweis vor der
    /// ersten Prüfung, derselbe Verlauf.
    /// </summary>
    /// <param name="paths">Die Pfade der Vorführung.</param>
    internal static void ResetDirectory(AppPaths paths)
    {
        if (Directory.Exists(paths.RootDirectory))
        {
            Directory.Delete(paths.RootDirectory, recursive: true);
        }

        _ = Directory.CreateDirectory(paths.RootDirectory);
    }

    /// <summary>Legt Seitenliste, Verlauf und Protokoll der Vorführung an.</summary>
    /// <param name="services">Der Dienst-Container der Vorführung.</param>
    /// <param name="cancellationToken">Bricht das Anlegen ab.</param>
    /// <returns>Ein Task, der nach dem Anlegen endet.</returns>
    internal static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        DateTimeOffset now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        DemoClock clock = new(now);
        using FileLoggerProvider loggers = new(services.GetRequiredService<AppPaths>().LogDirectory, clock);
        DemoSeeder seeder = new(
            services.GetRequiredService<ISiteStore>(),
            services.GetRequiredService<IHistoryStore>(),
            services.GetRequiredService<ISettingsStore>(),
            clock,
            loggers);
        await seeder.SeedAsync(CreatePlan(clock), now, cancellationToken).ConfigureAwait(true);
    }

    // Die Hauptseite zweimal im früheren Stand, damit eine heutige Prüfung Behobenes zeigt; der
    // Shop früher sauber, heute weist er ab; der Blog durchgehend gut.
    private static DemoSeedPlan CreatePlan(DemoClock clock)
    {
        Site main = DemoWorld.Sites[0];
        Site shop = DemoWorld.Sites[1];
        Site blog = DemoWorld.Sites[2];
        List<DemoSeedRun> runs = [.. MainSiteRunsAgo.Select(ago => Run(main, ago, DemoEra.Earlier, clock))];
        runs.Add(Run(shop, ShopRunAgo, DemoEra.Earlier, clock));
        runs.Add(Run(blog, BlogRunAgo, DemoEra.Current, clock));
        return new DemoSeedPlan(DemoWorld.Sites, runs, main.Address, DemoWorld.ShopHost);
    }

    private static DemoSeedRun Run(Site site, TimeSpan ago, DemoEra era, DemoClock clock) =>
        new(site, ago, new DemoHttpProbe(era), new DemoTlsProbe(clock), new DemoDnsProbe(era));
}
