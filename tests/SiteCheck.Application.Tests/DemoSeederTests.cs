using Microsoft.Extensions.Logging;
using SiteCheck.Application.Demo;
using SiteCheck.Application.Tests.Fakes;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;

namespace SiteCheck.Application.Tests;

/// <summary>
/// Die Vorführung legt ihre Beispieldaten aus echten Läufen und echten Meldungen an. Diese Tests
/// halten fest, dass Verlauf und Protokoll zeitlich stimmen und nichts davon den echten Betrieb
/// voraussetzt.
/// </summary>
public sealed class DemoSeederTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Seed_StoresSitesAndAcknowledgesTheScopeNotice()
    {
        Fixture fixture = new();

        await fixture.Seeder.SeedAsync(fixture.Plan(), Now, Token);

        Assert.Equal(fixture.Sites, fixture.SiteStore.Saved);
        Assert.True(fixture.Settings.Saved?.ScopeNoticeAcknowledged);
    }

    [Fact]
    public async Task Seed_RecordsEachRunAtItsOwnTime()
    {
        Fixture fixture = new();

        await fixture.Seeder.SeedAsync(fixture.Plan(), Now, Token);

        Assert.Equal(
            [Now - TimeSpan.FromDays(6), Now - TimeSpan.FromDays(2)],
            fixture.History.Appended.Select(entry => entry.Entry.RanAt));
        Assert.All(fixture.History.Appended, entry => Assert.NotEmpty(entry.Entry.Findings));
    }

    [Fact]
    public async Task Seed_WritesTheLogInOrderWithRealMessages()
    {
        Fixture fixture = new();

        await fixture.Seeder.SeedAsync(fixture.Plan(), Now, Token);

        IReadOnlyList<(DateTimeOffset At, string Text)> lines = fixture.Log.Lines;
        Assert.Equal(lines.OrderBy(line => line.At), lines);
        Assert.Contains(lines, line => line.Text.StartsWith("Prüflauf gestartet: https://seite.example/", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Text.StartsWith("Anfragen an shop.example angehalten bis", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Text == "Sperrzeit für shop.example von Hand aufgehoben");
        Assert.Contains(lines, line => line.Text.StartsWith("Lasttest beendet: https://seite.example/", StringComparison.Ordinal));
        Assert.All(lines, line => Assert.True(line.At < Now));
    }

    [Fact]
    public async Task Seed_LeavesTheClockAtNow()
    {
        Fixture fixture = new();

        await fixture.Seeder.SeedAsync(fixture.Plan(), Now, Token);

        Assert.Equal(Now, fixture.Clock.GetUtcNow());
    }

    [Fact]
    public async Task Seed_InvalidAddress_FailsWithTheAddress()
    {
        Fixture fixture = new();
        DemoSeedPlan plan = fixture.Plan() with
        {
            Runs = [new DemoSeedRun(new Site("Kaputt", "ftp://seite.example/"), TimeSpan.FromDays(1), fixture.Http, new CountingTlsProbe(), new StubDnsProbe())],
        };

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Seeder.SeedAsync(plan, Now, Token));

        Assert.Contains("ftp://seite.example/", error.Message, StringComparison.Ordinal);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Clock = new DemoClock(Now);
            Log = new ClockedLoggerProvider(Clock);
            Seeder = new DemoSeeder(SiteStore, History, Settings, Clock, Log);
        }

        public IReadOnlyList<Site> Sites { get; } = [new Site("Seite", "https://seite.example/")];

        public RecordingHttpProbe Http { get; } = new(_ => 200);

        public DemoClock Clock { get; }

        public ClockedLoggerProvider Log { get; }

        public MemorySiteStore SiteStore { get; } = new();

        public MemoryHistoryStore History { get; } = new();

        public MemorySettingsStore Settings { get; } = new();

        public DemoSeeder Seeder { get; }

        // Bewusst in falscher Reihenfolge: Der Seeder muss selbst nach der Zeit ordnen.
        public DemoSeedPlan Plan() => new(
            Sites,
            [
                new DemoSeedRun(Sites[0], TimeSpan.FromDays(2), Http, new CountingTlsProbe(), new StubDnsProbe()),
                new DemoSeedRun(Sites[0], TimeSpan.FromDays(6), Http, new CountingTlsProbe(), new StubDnsProbe()),
            ],
            Sites[0].Address,
            "shop.example");
    }

    private sealed class MemorySiteStore : ISiteStore
    {
        public IReadOnlyList<Site>? Saved { get; private set; }

        public Task<IReadOnlyList<Site>> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(Saved ?? []);

        public Task SaveAsync(IReadOnlyList<Site> sites, CancellationToken cancellationToken)
        {
            Saved = sites;
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryHistoryStore : IHistoryStore
    {
        public List<(string Address, HistoryEntry Entry)> Appended { get; } = [];

        public Task<IReadOnlyList<HistoryEntry>> LoadAsync(string address, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<HistoryEntry>>([.. Appended.Where(item => item.Address == address).Select(item => item.Entry)]);

        public Task AppendAsync(string address, HistoryEntry entry, CancellationToken cancellationToken)
        {
            Appended.Add((address, entry));
            return Task.CompletedTask;
        }

        public Task ClearAsync(string address, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class MemorySettingsStore : ISettingsStore
    {
        public AppSettings? Saved { get; private set; }

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(Saved ?? AppSettings.Default);

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
        {
            Saved = settings;
            return Task.CompletedTask;
        }
    }

    /// <summary>Schreibt jede Meldung mit der Zeit der Uhr mit, wie es der Dateischreiber tut.</summary>
    private sealed class ClockedLoggerProvider(TimeProvider clock) : ILoggerProvider
    {
        private readonly List<(DateTimeOffset At, string Text)> _lines = [];
        private readonly TimeProvider _clock = clock;

        public IReadOnlyList<(DateTimeOffset At, string Text)> Lines => _lines;

        public ILogger CreateLogger(string categoryName) => new Logger(this);

        public void Dispose()
        {
            // Hält nichts offen.
        }

        private sealed class Logger(ClockedLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                owner._lines.Add((owner._clock.GetUtcNow(), formatter(state, exception)));
        }
    }
}
