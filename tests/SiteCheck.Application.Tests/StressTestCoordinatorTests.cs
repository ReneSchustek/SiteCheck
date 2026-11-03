using SiteCheck.Application.Tests.Fakes;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Guard;
using SiteCheck.Core.Models.Stress;

namespace SiteCheck.Application.Tests;

public sealed class StressTestCoordinatorTests
{
    private static HostRequestGuard NewGuard() => new(TimeProvider.System, new InMemoryGuardStore(), new StubDnsProbe());

    private static CheckTarget Target()
    {
        Assert.True(CheckTarget.TryParse("https://beispiel.de", out CheckTarget? target, out _));
        return target!;
    }

    [Fact]
    public async Task RunAsync_WithoutOwnershipConfirmation_Throws()
    {
        StressTestCoordinator coordinator = new(new FakeLoadProbe(), NewGuard());

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.RunAsync(
            Target(),
            StressTestOptions.Default,
            ownershipConfirmed: false,
            progress: null,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RunAsync_WithConfirmation_SendsRequestsAndSummarizes()
    {
        FakeLoadProbe probe = new(statusCode: 200, elapsedMs: 2);
        StressTestCoordinator coordinator = new(probe, NewGuard());
        StressTestOptions options = new(2, TimeSpan.FromSeconds(5));

        StressTestResult result = await coordinator.RunAsync(
            Target(),
            options,
            ownershipConfirmed: true,
            progress: null,
            TestContext.Current.CancellationToken);

        Assert.True(result.TotalRequests > 0);
        Assert.Equal(result.TotalRequests, probe.Calls);
        Assert.Equal(result.TotalRequests, result.SuccessCount);
    }

    [Fact]
    public async Task RunAsync_WhenCancelled_Throws()
    {
        StressTestCoordinator coordinator = new(new FakeLoadProbe(), NewGuard());
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.RunAsync(
            Target(),
            new StressTestOptions(2, TimeSpan.FromSeconds(5)),
            ownershipConfirmed: true,
            progress: null,
            cts.Token));
    }

    [Fact]
    public async Task RunAsync_WithProgress_ReportsAtLeastOnce()
    {
        FakeLoadProbe probe = new(statusCode: 200, elapsedMs: 1);
        StressTestCoordinator coordinator = new(probe, NewGuard());
        CountingProgress progress = new();

        StressTestResult result = await coordinator.RunAsync(
            Target(),
            new StressTestOptions(2, TimeSpan.FromSeconds(1)),
            ownershipConfirmed: true,
            progress: progress,
            TestContext.Current.CancellationToken);

        // Mindestens die Abschlussmeldung muss ankommen; der Zwischen-Timer feuert zusätzlich.
        Assert.True(progress.Count >= 1);
        Assert.Equal(result.TotalRequests, progress.Last!.CompletedRequests);
    }

    // Synchroner Fortschritts-Empfänger: Progress<T> würde die Rückrufe asynchron zustellen und
    // den Test flackern lassen; dieser zählt direkt und threadsicher.
    [Fact]
    public async Task RunAsync_OnRejection_StopsAtOnceAndRests()
    {
        FakeLoadProbe probe = new(statusCode: 429);
        HostRequestGuard guard = NewGuard();
        StressTestCoordinator coordinator = new(probe, guard);

        StressTestResult result = await coordinator.RunAsync(
            Target(), new StressTestOptions(1, TimeSpan.FromSeconds(30)), ownershipConfirmed: true, progress: null, TestContext.Current.CancellationToken);

        Assert.True(result.StoppedByRejection);
        Assert.Equal(1, probe.Calls);
        Assert.Equal(RequestHaltReason.HostRejected, (await guard.GetStatusAsync("beispiel.de", TestContext.Current.CancellationToken)).BlockReason);
    }

    [Fact]
    public async Task RunAsync_WhenHostIsBlocked_SendsNothing()
    {
        FakeLoadProbe probe = new();
        HostRequestGuard guard = NewGuard();
        await guard.StartCooldownAsync("beispiel.de", TestContext.Current.CancellationToken);
        StressTestCoordinator coordinator = new(probe, guard);

        _ = await Assert.ThrowsAsync<RequestsHaltedException>(() => coordinator.RunAsync(
            Target(), StressTestOptions.Default, ownershipConfirmed: true, progress: null, TestContext.Current.CancellationToken));

        Assert.Equal(0, probe.Calls);
    }

    [Fact]
    public async Task RunAsync_AfterCompletion_BlocksHostForCooldown()
    {
        HostRequestGuard guard = NewGuard();
        StressTestCoordinator coordinator = new(new FakeLoadProbe(), guard);

        StressTestResult result = await coordinator.RunAsync(
            Target(), new StressTestOptions(1, TimeSpan.FromSeconds(5)), ownershipConfirmed: true, progress: null, TestContext.Current.CancellationToken);

        Assert.False(result.StoppedByRejection);
        Assert.Equal(RequestHaltReason.CoolingDown, (await guard.GetStatusAsync("beispiel.de", TestContext.Current.CancellationToken)).BlockReason);
    }

    private sealed class CountingProgress : IProgress<StressTestProgress>
    {
        private int _count;

        public int Count => _count;

        public StressTestProgress? Last { get; private set; }

        public void Report(StressTestProgress value)
        {
            _ = Interlocked.Increment(ref _count);
            Last = value;
        }
    }

    [Fact]
    public async Task RunAsync_ClampsExcessiveOptions()
    {
        // Eine absurd hohe Anforderung läuft, aber nur innerhalb der festen Dauergrenze.
        FakeLoadProbe probe = new(elapsedMs: 1);
        StressTestCoordinator coordinator = new(probe, NewGuard());
        StressTestOptions tooMuch = new(10_000, TimeSpan.FromSeconds(5));

        StressTestResult result = await coordinator.RunAsync(
            Target(),
            tooMuch,
            ownershipConfirmed: true,
            progress: null,
            TestContext.Current.CancellationToken);

        // Dauer bleibt im erlaubten Rahmen; der Lauf endet also zügig und zählt endlich viele Anfragen.
        Assert.True(result.Elapsed <= TimeSpan.FromSeconds(StressTestOptions.MaxDurationSeconds + 2));
        Assert.True(result.TotalRequests > 0);
    }
}
