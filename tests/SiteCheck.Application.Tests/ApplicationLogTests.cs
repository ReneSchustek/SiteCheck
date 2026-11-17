using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using SiteCheck.Application.Tests.Fakes;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Stress;

namespace SiteCheck.Application.Tests;

/// <summary>Ein Halt muss sich später nachvollziehen lassen; darum prüfen diese Tests, dass er im Protokoll steht.</summary>
public sealed class ApplicationLogTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static CheckTarget Target()
    {
        Assert.True(CheckTarget.TryParse("https://beispiel.de", out CheckTarget? target, out _));
        return target!;
    }

    [Fact]
    public async Task Run_LogsStartEndAndHalt()
    {
        ListLogger logger = new();
        FakeTimeProvider clock = new() { AutoAdvanceAmount = TimeSpan.FromSeconds(2) };
        StubDnsProbe dns = new();
        HostRequestGuard guard = new(clock, new InMemoryGuardStore(), dns, logger);
        CheckRunnerFactory factory = new(new RecordingHttpProbe(_ => 429), new CountingTlsProbe(), dns, clock, guard, logger);

        CheckReport report = await factory.Create(CheckRunOptions.Standard).RunAsync(Target(), clock, null, Token);

        Assert.True(report.WasHalted);
        Assert.Contains(logger.Messages, message => message.StartsWith("Prüflauf gestartet", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.Contains("angehalten: True", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.Contains("Anfragen an beispiel.de angehalten", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Halt_NamesItsEndInTheLocalTimeOfTheClock()
    {
        ListLogger logger = new();
        FakeTimeProvider clock = new(new DateTimeOffset(2026, 11, 17, 11, 8, 20, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("Test+1", TimeSpan.FromHours(1), "Test+1", "Test+1"));
        HostRequestGuard guard = new(clock, new InMemoryGuardStore(), new StubDnsProbe(), logger);

        _ = await Assert.ThrowsAsync<RequestsHaltedException>(() => guard.ObserveResponseAsync(new Uri("https://beispiel.de/"), 429, Token));

        DateTimeOffset until = clock.GetUtcNow() + HostRequestGuard.HaltCooldown;
        string expected = until.ToOffset(TimeSpan.FromHours(1)).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        string message = Assert.Single(logger.Messages, message => message.StartsWith("Anfragen an beispiel.de angehalten", StringComparison.Ordinal));
        Assert.Equal($"Anfragen an beispiel.de angehalten bis {expected}: HostRejected", message);
        Assert.DoesNotContain("Z:", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BudgetAndRelease_AreLogged()
    {
        ListLogger logger = new();
        FakeTimeProvider clock = new() { AutoAdvanceAmount = TimeSpan.FromSeconds(2) };
        HostRequestGuard guard = new(clock, new InMemoryGuardStore(), new StubDnsProbe(), logger);
        for (int request = 0; request < HostRequestGuard.HourlyRequestBudget; request++)
        {
            await guard.EnterAsync("beispiel.de", Token);
        }

        _ = await Assert.ThrowsAsync<RequestsHaltedException>(() => guard.EnterAsync("beispiel.de", Token));
        await guard.ReleaseAsync("beispiel.de", Token);

        Assert.Contains(logger.Messages, message => message.Contains("BudgetUsed", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.Contains("von Hand aufgehoben", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StressTest_LogsStartAndEnd()
    {
        ListLogger logger = new();
        HostRequestGuard guard = new(TimeProvider.System, new InMemoryGuardStore(), new StubDnsProbe());
        StressTestCoordinator coordinator = new(new FakeLoadProbe(statusCode: 429), guard, logger);

        _ = await coordinator.RunAsync(Target(), new StressTestOptions(1, TimeSpan.FromSeconds(5)), ownershipConfirmed: true, progress: null, Token);

        Assert.Contains(logger.Messages, message => message.StartsWith("Lasttest gestartet", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.Contains("wegen Abweisung beendet: True", StringComparison.Ordinal));
    }

    private sealed class ListLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
