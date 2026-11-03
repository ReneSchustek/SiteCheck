using Microsoft.Extensions.Time.Testing;
using SiteCheck.Application.Tests.Fakes;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Guard;

namespace SiteCheck.Application.Tests;

public sealed class HostRequestGuardTests
{
    private const string Host = "www.beispiel.de";

    private static readonly Uri Home = new("https://www.beispiel.de/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static HostRequestGuard Guard(FakeTimeProvider clock, InMemoryGuardStore? store = null, bool resolves = true) =>
        new(clock, store ?? new InMemoryGuardStore(), new StubDnsProbe(resolves));

    [Fact]
    public async Task EnterAsync_SecondRequest_WaitsForMinimumSpacing()
    {
        FakeTimeProvider clock = new();
        HostRequestGuard guard = Guard(clock);
        await guard.EnterAsync(Host, Token);

        Task second = guard.EnterAsync(Host, Token);
        Assert.False(second.IsCompleted);

        clock.Advance(HostRequestGuard.MinimumSpacing - TimeSpan.FromMilliseconds(1));
        Assert.False(second.IsCompleted);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        await second.ConfigureAwait(true);
    }

    [Fact]
    public async Task EnterAsync_DifferentHosts_ShareTheSpacing()
    {
        FakeTimeProvider clock = new();
        HostRequestGuard guard = Guard(clock);
        await guard.EnterAsync("erste-seite.de", Token);

        Task second = guard.EnterAsync("zweite-seite.de", Token);

        Assert.False(second.IsCompleted);
        clock.Advance(HostRequestGuard.MinimumSpacing);
        await second.ConfigureAwait(true);
    }

    [Fact]
    public async Task EnterAsync_FullMinute_StaysAtFortyRequests()
    {
        FakeTimeProvider clock = new();
        HostRequestGuard guard = Guard(clock);
        int entered = 0;
        Task pending = guard.EnterAsync(Host, Token);

        // Eine Minute in kleinen Schritten: Jede frei gewordene Anfrage folgt sofort der nächsten.
        for (int tick = 0; tick < 600; tick++)
        {
            while (pending.IsCompleted)
            {
                await pending.ConfigureAwait(true);
                entered++;
                pending = guard.EnterAsync(Host, Token);
            }

            clock.Advance(TimeSpan.FromMilliseconds(100));
        }

        Assert.Equal(40, entered);
    }

    [Fact]
    public async Task EnterAsync_WhenCancelledDuringWait_Throws()
    {
        FakeTimeProvider clock = new();
        HostRequestGuard guard = Guard(clock);
        await guard.EnterAsync(Host, Token);
        using CancellationTokenSource cancellation = new();

        Task second = guard.EnterAsync(Host, cancellation.Token);
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
    }

    [Fact]
    public async Task EnterAsync_WhenBudgetUsed_HaltsAcrossRestart()
    {
        FakeTimeProvider clock = new() { AutoAdvanceAmount = TimeSpan.FromSeconds(2) };
        InMemoryGuardStore store = new();
        HostRequestGuard guard = Guard(clock, store);
        for (int request = 0; request < HostRequestGuard.HourlyRequestBudget; request++)
        {
            await guard.EnterAsync(Host, Token);
        }

        RequestsHaltedException halt = await Assert.ThrowsAsync<RequestsHaltedException>(
            () => Guard(clock, store).EnterAsync(Host, Token));

        Assert.Equal(RequestHaltReason.BudgetUsed, halt.Reason);
        Assert.Equal("beispiel.de", halt.Host);
    }

    [Fact]
    public async Task EnterAsync_AfterBudgetWindow_IsAllowedAgain()
    {
        FakeTimeProvider clock = new() { AutoAdvanceAmount = TimeSpan.FromSeconds(2) };
        HostRequestGuard guard = Guard(clock);
        for (int request = 0; request < HostRequestGuard.HourlyRequestBudget; request++)
        {
            await guard.EnterAsync(Host, Token);
        }

        clock.Advance(TimeSpan.FromHours(1));

        await guard.EnterAsync(Host, Token);
    }

    [Theory]
    [InlineData("https://www.beispiel.de/", 429)]
    [InlineData("https://www.beispiel.de/", 503)]
    [InlineData("https://www.beispiel.de/", 403)]
    [InlineData("https://www.beispiel.de/.well-known/security.txt", 403)]
    [InlineData("https://www.beispiel.de/.env", 429)]
    public async Task ObserveResponseAsync_WhenRejected_HaltsAndNothingEnters(string address, int status)
    {
        FakeTimeProvider clock = new();
        HostRequestGuard guard = Guard(clock);

        RequestsHaltedException halt = await Assert.ThrowsAsync<RequestsHaltedException>(
            () => guard.ObserveResponseAsync(new Uri(address), status, Token));

        Assert.Equal(RequestHaltReason.HostRejected, halt.Reason);
        _ = await Assert.ThrowsAsync<RequestsHaltedException>(() => guard.EnterAsync(Host, Token));
    }

    [Theory]
    [InlineData("https://www.beispiel.de/.env", 403)]
    [InlineData("https://www.beispiel.de/.git/config", 404)]
    [InlineData("https://www.beispiel.de/uploads/", 403)]
    [InlineData("https://www.beispiel.de/", 200)]
    [InlineData("https://www.beispiel.de/", 500)]
    public async Task ObserveResponseAsync_WhenExpected_DoesNotHalt(string address, int status)
    {
        HostRequestGuard guard = Guard(new FakeTimeProvider());

        await guard.ObserveResponseAsync(new Uri(address), status, Token);

        await guard.EnterAsync(Host, Token);
    }

    [Fact]
    public async Task ObserveNoAnswerAsync_Once_DoesNotHalt()
    {
        HostRequestGuard guard = Guard(new FakeTimeProvider());

        await guard.ObserveNoAnswerAsync(Host, Token);

        Assert.False((await guard.GetStatusAsync(Host, Token)).IsBlocked);
    }

    [Fact]
    public async Task ObserveNoAnswerAsync_Twice_Halts()
    {
        HostRequestGuard guard = Guard(new FakeTimeProvider());
        await guard.ObserveNoAnswerAsync(Host, Token);

        _ = await Assert.ThrowsAsync<RequestsHaltedException>(() => guard.ObserveNoAnswerAsync(Host, Token));

        Assert.True((await guard.GetStatusAsync(Host, Token)).IsBlocked);
    }

    [Fact]
    public async Task ObserveNoAnswerAsync_WithAnswerInBetween_StartsCountingAgain()
    {
        HostRequestGuard guard = Guard(new FakeTimeProvider());
        await guard.ObserveNoAnswerAsync(Host, Token);
        await guard.ObserveResponseAsync(Home, 200, Token);

        await guard.ObserveNoAnswerAsync(Host, Token);

        Assert.False((await guard.GetStatusAsync(Host, Token)).IsBlocked);
    }

    [Fact]
    public async Task ObserveNoAnswerAsync_WhenNameDoesNotResolve_NeverHalts()
    {
        HostRequestGuard guard = Guard(new FakeTimeProvider(), resolves: false);

        await guard.ObserveNoAnswerAsync("vertippt.beispiel.de", Token);
        await guard.ObserveNoAnswerAsync("vertippt.beispiel.de", Token);
        await guard.ObserveNoAnswerAsync("vertippt.beispiel.de", Token);

        Assert.False((await guard.GetStatusAsync("vertippt.beispiel.de", Token)).IsBlocked);
    }

    [Fact]
    public async Task Halt_ReachesSubdomainsOfTheSameDomain()
    {
        HostRequestGuard guard = Guard(new FakeTimeProvider());
        _ = await Assert.ThrowsAsync<RequestsHaltedException>(() => guard.ObserveResponseAsync(Home, 429, Token));

        _ = await Assert.ThrowsAsync<RequestsHaltedException>(() => guard.EnterAsync("mta-sts.beispiel.de", Token));
    }

    [Fact]
    public async Task Halt_OnIpAddress_AffectsOnlyThatAddress()
    {
        HostRequestGuard guard = Guard(new FakeTimeProvider());
        _ = await Assert.ThrowsAsync<RequestsHaltedException>(() => guard.ObserveResponseAsync(new Uri("https://192.0.2.1/"), 429, Token));

        Assert.True((await guard.GetStatusAsync("192.0.2.1", Token)).IsBlocked);
        Assert.False((await guard.GetStatusAsync("192.0.3.1", Token)).IsBlocked);
    }

    [Fact]
    public async Task Halt_SurvivesRestart_AndEndsAfterCooldown()
    {
        FakeTimeProvider clock = new();
        InMemoryGuardStore store = new();
        _ = await Assert.ThrowsAsync<RequestsHaltedException>(() => Guard(clock, store).ObserveResponseAsync(Home, 429, Token));

        HostRequestGuard afterRestart = Guard(clock, store);
        HostGuardStatus status = await afterRestart.GetStatusAsync(Host, Token);
        Assert.True(status.IsBlocked);
        Assert.Equal(RequestHaltReason.HostRejected, status.BlockReason);

        clock.Advance(HostRequestGuard.HaltCooldown);
        await afterRestart.EnterAsync(Host, Token);
    }

    [Fact]
    public async Task ReleaseAsync_LiftsHaltAfterRestart()
    {
        FakeTimeProvider clock = new();
        InMemoryGuardStore store = new();
        _ = await Assert.ThrowsAsync<RequestsHaltedException>(() => Guard(clock, store).ObserveResponseAsync(Home, 429, Token));

        await Guard(clock, store).ReleaseAsync(Host, Token);

        await Guard(clock, store).EnterAsync(Host, Token);
    }

    [Fact]
    public async Task StartCooldownAsync_DoesNotShortenLongerHalt()
    {
        FakeTimeProvider clock = new();
        HostRequestGuard guard = Guard(clock);
        _ = await Assert.ThrowsAsync<RequestsHaltedException>(() => guard.ObserveResponseAsync(Home, 429, Token));

        await guard.StartCooldownAsync(Host, Token);

        HostGuardStatus status = await guard.GetStatusAsync(Host, Token);
        Assert.Equal(RequestHaltReason.HostRejected, status.BlockReason);
        Assert.Equal(clock.GetUtcNow() + HostRequestGuard.HaltCooldown, status.BlockedUntil);
    }

    [Fact]
    public async Task AcknowledgeNoticeAsync_SurvivesRestart()
    {
        FakeTimeProvider clock = new();
        InMemoryGuardStore store = new();
        Assert.False((await Guard(clock, store).GetStatusAsync(Host, Token)).NoticeAcknowledged);

        await Guard(clock, store).AcknowledgeNoticeAsync(Host, Token);

        Assert.True((await Guard(clock, store).GetStatusAsync("beispiel.de", Token)).NoticeAcknowledged);
    }

    [Fact]
    public async Task Guard_WhenStoreFails_KeepsBrakingInMemory()
    {
        FakeTimeProvider clock = new();
        InMemoryGuardStore store = new() { Fails = true };
        HostRequestGuard guard = Guard(clock, store);

        await guard.EnterAsync(Host, Token);
        Task second = guard.EnterAsync(Host, Token);
        Assert.False(second.IsCompleted);
        clock.Advance(HostRequestGuard.MinimumSpacing);
        await second.ConfigureAwait(true);

        _ = await Assert.ThrowsAsync<RequestsHaltedException>(() => guard.ObserveResponseAsync(Home, 429, Token));
        _ = await Assert.ThrowsAsync<RequestsHaltedException>(() => guard.EnterAsync(Host, Token));
    }

    [Fact]
    public void HaltedFindings_Describe_NamesHostForEveryReason()
    {
        foreach (RequestHaltReason reason in Enum.GetValues<RequestHaltReason>())
        {
            RequestsHaltedException halt = new(reason, "beispiel.de", DateTimeOffset.UnixEpoch);

            Assert.Contains("beispiel.de", HaltedFindings.Describe(halt), StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(HaltedFindings.ForRun(halt).Title));
        }
    }

    [Fact]
    public void RequestsHaltedException_StandardConstructors_KeepMessage()
    {
        Assert.Equal("Text", new RequestsHaltedException("Text").Message);
        _ = Assert.IsType<IOException>(new RequestsHaltedException("Text", new IOException()).InnerException);
        Assert.Equal(string.Empty, new RequestsHaltedException().Host);
    }
}

public sealed class GuardedRunTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static CheckTarget Target()
    {
        Assert.True(CheckTarget.TryParse("https://beispiel.de", out CheckTarget? target, out _));
        return target!;
    }

    private static string[] SensitiveMarkers => [".git", ".env", "composer", ".htaccess", "config.php", "backup", ".DS_Store", "/assets/", "/uploads/", "/images/", "/files/"];

    private static async Task<(CheckReport Report, RecordingHttpProbe Http, CountingTlsProbe Tls)> RunAsync(
        Func<Uri, int?> statusFor,
        CheckRunOptions options,
        bool tlsAnswers = true,
        HostRequestGuard? guard = null)
    {
        FakeTimeProvider clock = new() { AutoAdvanceAmount = TimeSpan.FromSeconds(2) };
        StubDnsProbe dns = new();
        RecordingHttpProbe http = new(statusFor);
        CountingTlsProbe tls = new(tlsAnswers);
        CheckRunnerFactory factory = new(http, tls, dns, clock, guard ?? new HostRequestGuard(clock, new InMemoryGuardStore(), dns));
        CheckReport report = await factory.Create(options).RunAsync(Target(), clock, null, Token).ConfigureAwait(true);
        return (report, http, tls);
    }

    [Fact]
    public async Task StandardRun_SendsNoSensitivePaths_AndSaysSo()
    {
        (CheckReport report, RecordingHttpProbe http, _) = await RunAsync(_ => 200, CheckRunOptions.Standard);

        Assert.DoesNotContain(http.Requests, request => SensitiveMarkers.Any(marker => request.AbsoluteUri.Contains(marker, StringComparison.Ordinal)));
        Assert.Contains(report.Findings, finding => finding.Key == "web.files" + CheckReport.NotCheckedKeySuffix);
        Assert.Contains(report.Findings, finding => finding.Key == "web.listing" + CheckReport.NotCheckedKeySuffix);
        Assert.False(report.WasHalted);
    }

    [Fact]
    public async Task StandardRun_StaysSmall()
    {
        (_, RecordingHttpProbe http, CountingTlsProbe tls) = await RunAsync(_ => 200, CheckRunOptions.Standard);

        // Wächter: http-Umleitung, Startseite, security.txt und MTA-STS.
        Assert.True(http.Requests.Count + tls.Calls <= 5, "Der Standardlauf ist auf wenige Anfragen ausgelegt.");
    }

    [Fact]
    public async Task ConfirmedRun_SendsSensitivePaths()
    {
        (CheckReport report, RecordingHttpProbe http, _) = await RunAsync(_ => 404, CheckRunOptions.WithSensitivePaths);

        Assert.Contains(http.Requests, request => request.AbsolutePath == "/.env");
        Assert.DoesNotContain(report.Findings, finding => finding.Key.StartsWith("web.files.notchecked", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Rejection_StopsAllFurtherRequests_AndLeavesRestNotChecked()
    {
        (CheckReport report, RecordingHttpProbe http, _) = await RunAsync(
            address => address.AbsolutePath == "/.env" ? 429 : 200,
            CheckRunOptions.WithSensitivePaths);

        Uri last = http.Requests[^1];
        Assert.Equal("/.env", last.AbsolutePath);
        Assert.True(report.WasHalted);
        Assert.Equal(CheckReport.HaltedFindingKey, report.Findings[0].Key);
        Assert.Contains(report.Findings, finding => finding.Key == "web.files" + CheckReport.NotCheckedKeySuffix);
        Assert.Contains(report.Findings, finding => finding.Key == "web.listing" + CheckReport.NotCheckedKeySuffix);
        Assert.DoesNotContain(report.Findings, finding => finding.Key.StartsWith("web.files", StringComparison.Ordinal) && finding.Severity == Severity.Ok);
    }

    [Fact]
    public async Task SilentServer_HaltsAfterSecondAttempt_WithoutClaimingCritical()
    {
        (CheckReport report, RecordingHttpProbe http, CountingTlsProbe tls) = await RunAsync(
            _ => null,
            CheckRunOptions.Standard,
            tlsAnswers: false);

        Assert.True(report.WasHalted);
        _ = Assert.Single(http.Requests);
        Assert.Equal(1, tls.Calls);
        Assert.Equal(0, report.Summary.CriticalCount);
    }

    [Fact]
    public async Task SecondRun_DuringHalt_SendsNothing()
    {
        FakeTimeProvider clock = new() { AutoAdvanceAmount = TimeSpan.FromSeconds(2) };
        HostRequestGuard guard = new(clock, new InMemoryGuardStore(), new StubDnsProbe());
        _ = await RunAsync(_ => 429, CheckRunOptions.Standard, guard: guard);

        (CheckReport second, RecordingHttpProbe http, CountingTlsProbe tls) = await RunAsync(_ => 200, CheckRunOptions.Standard, guard: guard).ConfigureAwait(true);

        Assert.True(second.WasHalted);
        Assert.Empty(http.Requests);
        Assert.Equal(0, tls.Calls);
    }

    [Fact]
    public void ReportComparison_WhatWasNotChecked_IsNeverResolved()
    {
        Finding exposed = new("web.files.exposed", CheckCategory.Web, Severity.Critical, "offen");
        Finding other = new("web.cookie.flags", CheckCategory.Web, Severity.Warning, "Cookie");
        Finding notChecked = new("web.files" + CheckReport.NotCheckedKeySuffix, CheckCategory.Web, Severity.Info, "Nicht geprüft");

        IReadOnlyList<ComparedFinding> compared = ReportComparison.Compare([notChecked], [exposed, other]);

        ComparedFinding resolved = Assert.Single(compared, entry => entry.Change == FindingChange.Resolved);
        Assert.Equal("web.cookie.flags", resolved.Finding.Key);
    }
}
