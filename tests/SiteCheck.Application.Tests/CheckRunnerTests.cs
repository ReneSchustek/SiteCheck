using Microsoft.Extensions.Time.Testing;
using SiteCheck.Application.Tests.Fakes;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;

namespace SiteCheck.Application.Tests;

public sealed class CheckRunnerTests
{
    private static CheckTarget Target()
    {
        Assert.True(CheckTarget.TryParse("https://beispiel.de", out CheckTarget? target, out _));
        return target!;
    }

    [Fact]
    public async Task RunAsync_AggregatesFindingsInOrder()
    {
        StubCheck web = new("web", CheckCategory.Web, [new Finding("w", CheckCategory.Web, Severity.Ok, "Web")]);
        StubCheck mail = new("mail", CheckCategory.Mail, [new Finding("m", CheckCategory.Mail, Severity.Warning, "Mail")]);
        CheckRunner runner = new([web, mail]);

        CheckReport report = await runner.RunAsync(Target(), new FakeTimeProvider(), progress: null, TestContext.Current.CancellationToken);

        Assert.Equal(["w", "m"], report.Findings.Select(finding => finding.Key));
        Assert.Equal(Severity.Warning, report.Summary.WorstSeverity);
    }

    [Fact]
    public async Task RunAsync_WhenCheckThrows_ReportsErrorFindingAndContinues()
    {
        StubCheck failing = new("boom", CheckCategory.Web, [], throws: true);
        StubCheck next = new("next", CheckCategory.Web, [new Finding("ok", CheckCategory.Web, Severity.Ok, "weiter")]);
        CheckRunner runner = new([failing, next]);

        CheckReport report = await runner.RunAsync(Target(), new FakeTimeProvider(), progress: null, TestContext.Current.CancellationToken);

        Assert.Contains(report.Findings, finding => finding.Key == "boom.error" && finding.Severity == Severity.Warning);
        Assert.Equal(1, next.RunCount);
    }

    [Fact]
    public async Task RunAsync_WhenCheckCancels_PropagatesCancellation()
    {
        // Wirft die Prüfung selbst einen Abbruch, reicht der Runner ihn durch (kein Fehlerbefund).
        StubCheck cancelling = new("c", CheckCategory.Web, [], cancels: true);
        CheckRunner runner = new([cancelling]);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runner.RunAsync(Target(), new FakeTimeProvider(), progress: null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RunAsync_ReportsProgressForEachCheck()
    {
        RecordingProgress progress = new();
        StubCheck a = new("a", CheckCategory.Web, []);
        StubCheck b = new("b", CheckCategory.Web, []);
        CheckRunner runner = new([a, b]);

        _ = await runner.RunAsync(Target(), new FakeTimeProvider(), progress, TestContext.Current.CancellationToken);

        // Je Prüfung ein Start plus die Abschlussmeldung am Ende.
        Assert.Equal(3, progress.Totals.Count);
        Assert.All(progress.Totals, total => Assert.Equal(2, total));
    }

    [Fact]
    public async Task RunAsync_WhenCancelled_Throws()
    {
        StubCheck a = new("a", CheckCategory.Web, []);
        CheckRunner runner = new([a]);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runner.RunAsync(Target(), new FakeTimeProvider(), progress: null, cts.Token));
    }

    private sealed class RecordingProgress : ICheckProgress
    {
        public List<int> Totals { get; } = [];

        public void Report(string checkId, int completed, int total) => Totals.Add(total);
    }
}
