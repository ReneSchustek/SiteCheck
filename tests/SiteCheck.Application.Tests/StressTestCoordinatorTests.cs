using SiteCheck.Application.Tests.Fakes;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Stress;

namespace SiteCheck.Application.Tests;

public sealed class StressTestCoordinatorTests
{
    private static CheckTarget Target()
    {
        Assert.True(CheckTarget.TryParse("https://beispiel.de", out CheckTarget? target, out _));
        return target!;
    }

    [Fact]
    public async Task RunAsync_WithoutOwnershipConfirmation_Throws()
    {
        StressTestCoordinator coordinator = new(new FakeLoadProbe());

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
        StressTestCoordinator coordinator = new(probe);
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
        StressTestCoordinator coordinator = new(new FakeLoadProbe());
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
        StressTestCoordinator coordinator = new(probe);
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
        StressTestCoordinator coordinator = new(probe);
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
