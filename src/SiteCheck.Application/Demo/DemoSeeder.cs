using Microsoft.Extensions.Logging;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Guard;

namespace SiteCheck.Application.Demo;

/// <summary>Eine Uhr, die steht, wo man sie hinstellt. Der Vorführmodus legt damit frühere Läufe an.</summary>
/// <param name="now">Startzeitpunkt.</param>
public sealed class DemoClock(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    /// <summary>Stellt die Uhr.</summary>
    /// <param name="now">Der neue Zeitpunkt.</param>
    public void Set(DateTimeOffset now) => _now = now;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => _now;
}

/// <summary>Ein früherer Prüflauf, den die Vorführung in den Verlauf legt.</summary>
/// <param name="Site">Die geprüfte Seite.</param>
/// <param name="Ago">Wie lange der Lauf zurückliegt.</param>
/// <param name="Http">HTTP-Sonde mit dem damaligen Stand.</param>
/// <param name="Tls">TLS-Sonde mit dem damaligen Stand.</param>
/// <param name="Dns">DNS-Sonde mit dem damaligen Stand.</param>
public sealed record DemoSeedRun(Site Site, TimeSpan Ago, IHttpProbe Http, ITlsProbe Tls, IDnsProbe Dns);

/// <summary>Alles, was die Vorführung vor dem ersten Fenster anlegt.</summary>
/// <param name="Sites">Die Einträge unter „Meine Seiten“.</param>
/// <param name="Runs">Frühere Prüfläufe, in beliebiger Reihenfolge.</param>
/// <param name="StressTarget">Adresse, für die ein früherer Lasttest im Protokoll steht.</param>
/// <param name="HaltedHost">Host, für den ein früherer Halt samt Aufhebung im Protokoll steht.</param>
public sealed record DemoSeedPlan(IReadOnlyList<Site> Sites, IReadOnlyList<DemoSeedRun> Runs, string StressTarget, string HaltedHost);

/// <summary>
/// Legt die Beispieldaten der Vorführung an: Seitenliste, Verlauf und Protokoll. Der Verlauf
/// entsteht aus echten Prüfläufen gegen die übergebenen Sonden, das Protokoll aus den echten
/// Meldungen; erfunden ist nur die Welt dahinter, nicht die Darstellung.
/// </summary>
/// <param name="sites">Speicher der Seitenliste.</param>
/// <param name="history">Speicher des Verlaufs.</param>
/// <param name="settings">Speicher der Einstellungen.</param>
/// <param name="clock">Die Uhr, die Protokoll und Läufe stempelt; sie wird während des Anlegens verstellt.</param>
/// <param name="loggers">Liefert die Protokollschreiber, die mit <paramref name="clock"/> stempeln.</param>
public sealed class DemoSeeder(ISiteStore sites, IHistoryStore history, ISettingsStore settings, DemoClock clock, ILoggerProvider loggers)
{
    private static readonly TimeSpan StressAgo = TimeSpan.FromHours(20);
    private static readonly TimeSpan HaltAgo = TimeSpan.FromDays(4);
    private static readonly TimeSpan HaltReleasedAfter = TimeSpan.FromHours(3);

    private readonly ISiteStore _sites = sites;
    private readonly IHistoryStore _history = history;
    private readonly ISettingsStore _settings = settings;
    private readonly DemoClock _clock = clock;
    private readonly ILoggerProvider _loggers = loggers;

    /// <summary>Legt alle Beispieldaten an. Erwartet einen leeren Datenordner.</summary>
    /// <param name="plan">Was angelegt wird.</param>
    /// <param name="now">Der tatsächliche Zeitpunkt; alles liegt davor.</param>
    /// <param name="cancellationToken">Bricht das Anlegen ab.</param>
    /// <returns>Ein Task, der nach dem Anlegen endet.</returns>
    public async Task SeedAsync(DemoSeedPlan plan, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);

        // Der Erststart-Hinweis gehört zum echten ersten Start, nicht in jede Vorführung.
        await _settings.SaveAsync(AppSettings.Default with { ScopeNoticeAcknowledged = true }, cancellationToken).ConfigureAwait(false);
        await _sites.SaveAsync(plan.Sites, cancellationToken).ConfigureAwait(false);

        // Das Protokoll hängt Zeile an Zeile; was früher war, muss deshalb zuerst geschrieben werden.
        foreach ((DateTimeOffset at, Func<Task> write) in Timeline(plan, now).OrderBy(step => step.At))
        {
            _clock.Set(at);
            await write().ConfigureAwait(false);
        }

        _clock.Set(now);
    }

    private IEnumerable<(DateTimeOffset At, Func<Task> Write)> Timeline(DemoSeedPlan plan, DateTimeOffset now)
    {
        foreach (DemoSeedRun run in plan.Runs)
        {
            yield return (now - run.Ago, () => RecordRunAsync(run));
        }

        ILogger guard = _loggers.CreateLogger(CategoryOf<HostRequestGuard>());
        ILogger stress = _loggers.CreateLogger(CategoryOf<StressTestCoordinator>());
        DateTimeOffset haltAt = now - HaltAgo;
        yield return (haltAt, () => Log(() => guard.RequestsHalted(plan.HaltedHost, haltAt + HostRequestGuard.HaltCooldown, RequestHaltReason.HostRejected, _clock)));
        yield return (haltAt + HaltReleasedAfter, () => Log(() => guard.HaltReleased(plan.HaltedHost)));
        yield return (now - StressAgo, () => Log(() => stress.StressStarted(plan.StressTarget, 5, 20)));
        yield return (now - StressAgo + TimeSpan.FromSeconds(21), () => Log(() => stress.StressFinished(plan.StressTarget, 512, false)));
    }

    // Der volle Katalog mit heiklen Pfaden: Der Verlauf soll zeigen, was eine gründliche Prüfung
    // damals fand. Keine Sperrwache, denn die Sonden der Vorführung erreichen kein Netz.
    private async Task RecordRunAsync(DemoSeedRun run)
    {
        if (!CheckTarget.TryParse(run.Site.Address, out CheckTarget? target, out TargetParseError error))
        {
            throw new InvalidOperationException($"Vorführadresse {run.Site.Address} ist ungültig: {error}");
        }

        IReadOnlyList<ICheck> checks = CheckCatalog.CreateDefault(run.Http, run.Tls, run.Dns, _clock, CheckRunOptions.WithSensitivePaths);
        CheckRunner runner = new(checks, _loggers.CreateLogger(CategoryOf<CheckRunner>()));
        CheckReport report = await runner.RunAsync(target, _clock, null, CancellationToken.None).ConfigureAwait(false);
        await _history.AppendAsync(report.Address, new HistoryEntry(report.StartedAt, report.Summary, report.Findings), CancellationToken.None).ConfigureAwait(false);
    }

    // Dieselbe Kategorie wie ILogger<T>, damit die Einträge im Protokoll wie echte aussehen.
    private static string CategoryOf<T>() => typeof(T).FullName ?? typeof(T).Name;

    private static Task Log(Action write)
    {
        write();
        return Task.CompletedTask;
    }
}
