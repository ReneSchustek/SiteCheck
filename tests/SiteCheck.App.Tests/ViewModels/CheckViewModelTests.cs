using Microsoft.Extensions.Time.Testing;
using SiteCheck.App.Tests.Fakes;
using SiteCheck.App.ViewModels;
using SiteCheck.Application;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;

namespace SiteCheck.App.Tests.ViewModels;

public sealed class CheckViewModelTests
{
    private static CheckViewModel Build(FakeHistoryStore? history = null, params ICheck[] checks)
    {
        ICheck[] effective = checks.Length == 0
            ? [new StubCheck(CheckCategory.Web, [new Finding("web.ok", CheckCategory.Web, Severity.Ok, "Alles gut")])]
            : checks;
        FakeCheckRunnerFactory factory = new(effective);
        return new CheckViewModel(factory, new FakeTimeProvider(), history ?? new FakeHistoryStore(), new FakeReportExportService(), TestGuards.Create(), new FakeConfirmationService(confirms: true), new FakeStringResources());
    }

    [Fact]
    public async Task Run_WithInvalidAddress_ShowsValidationAndDoesNotRun()
    {
        CheckViewModel vm = Build();
        vm.AddressInput = "nicht gültig ohne punkt";

        await vm.RunCommand.ExecuteAsync(null);

        Assert.True(vm.HasValidationMessage);
        Assert.Equal(CheckRunState.Idle, vm.State);
    }

    [Fact]
    public async Task Run_WithValidAddress_FillsFindingsAndNormalizesInput()
    {
        FakeHistoryStore history = new();
        CheckViewModel vm = Build(
            history,
            new StubCheck(CheckCategory.Web, [new Finding("web.ok", CheckCategory.Web, Severity.Ok, "Web ok")]),
            new StubCheck(CheckCategory.Mail, [new Finding("mail.bad", CheckCategory.Mail, Severity.Critical, "Kein MX")]));
        vm.AddressInput = "beispiel.de";

        await vm.RunCommand.ExecuteAsync(null);

        Assert.Equal(CheckRunState.Completed, vm.State);
        Assert.True(vm.HasResult);
        Assert.Equal("https://beispiel.de/", vm.AddressInput);
        _ = Assert.Single(vm.WebFindings);
        _ = Assert.Single(vm.MailFindings);
        Assert.Equal("Status_Check_Summary|1|0|1", vm.StatusText);
        _ = Assert.Single(history.Appended);
    }

    [Fact]
    public async Task Run_MarksNewAndResolvedAgainstPreviousRun()
    {
        FakeHistoryStore history = new();
        // Voriger Lauf: ein Mangel, der diesmal weg ist; dafür kommt ein neuer dazu.
        Finding oldProblem = new("mail.old", CheckCategory.Mail, Severity.Warning, "alt");
        history.Seed(new HistoryEntry(DateTimeOffset.UnixEpoch, FindingSummary.From([oldProblem]), [oldProblem]));

        CheckViewModel vm = Build(
            history,
            new StubCheck(CheckCategory.Web, [new Finding("web.new", CheckCategory.Web, Severity.Warning, "neu")]));
        vm.AddressInput = "beispiel.de";
        await vm.RunCommand.ExecuteAsync(null);

        Assert.Contains(vm.WebFindings, f => f.Title == "neu" && f.Change == FindingChange.New);
        Assert.Contains(vm.MailFindings, f => f.Title == "alt" && f.Change == FindingChange.Resolved);
    }

    [Fact]
    public async Task Run_FirstTime_MarksEverythingUnchanged()
    {
        CheckViewModel vm = Build(
            new FakeHistoryStore(),
            new StubCheck(CheckCategory.Web, [new Finding("web.ok", CheckCategory.Web, Severity.Ok, "ok")]));
        vm.AddressInput = "beispiel.de";
        await vm.RunCommand.ExecuteAsync(null);

        Assert.All(vm.WebFindings, f => Assert.Equal(FindingChange.Unchanged, f.Change));
    }

    [Fact]
    public async Task ClearHistory_InvokesStore()
    {
        FakeHistoryStore history = new();
        CheckViewModel vm = Build(history);
        vm.AddressInput = "beispiel.de";
        await vm.RunCommand.ExecuteAsync(null);

        Assert.True(vm.ClearHistoryCommand.CanExecute(null));
        await vm.ClearHistoryCommand.ExecuteAsync(null);

        Assert.Contains("https://beispiel.de/", history.Cleared);
    }

    [Fact]
    public async Task Copy_AfterRun_InvokesExport()
    {
        FakeReportExportService export = new();
        FakeCheckRunnerFactory factory = new(new StubCheck(CheckCategory.Web, [new Finding("web.ok", CheckCategory.Web, Severity.Ok, "ok")]));
        CheckViewModel vm = new(factory, new FakeTimeProvider(), new FakeHistoryStore(), export, TestGuards.Create(), new FakeConfirmationService(confirms: true), new FakeStringResources());
        vm.AddressInput = "beispiel.de";
        await vm.RunCommand.ExecuteAsync(null);

        Assert.True(vm.CopyCommand.CanExecute(null));
        vm.CopyCommand.Execute(null);

        Assert.Equal(1, export.CopyCount);
    }

    private static CheckViewModel BuildGuarded(FakeCheckRunnerFactory factory, FakeConfirmationService confirmation, HostRequestGuard guard, FakeHistoryStore? history = null) =>
        new(factory, new FakeTimeProvider(), history ?? new FakeHistoryStore(), new FakeReportExportService(), guard, confirmation, new FakeStringResources());

    private static FakeCheckRunnerFactory OkFactory() =>
        new(new StubCheck(CheckCategory.Web, [new Finding("w", CheckCategory.Web, Severity.Ok, "ok")]));

    [Fact]
    public async Task Run_FirstTime_AsksOnceForEachHost()
    {
        FakeConfirmationService confirmation = new(confirms: true);
        CheckViewModel vm = BuildGuarded(OkFactory(), confirmation, TestGuards.Create());
        vm.AddressInput = "beispiel.de";

        await vm.RunCommand.ExecuteAsync(null);
        await vm.RunCommand.ExecuteAsync(null);

        Assert.Equal(1, confirmation.NoticeCount);
        Assert.Equal(CheckRunState.Completed, vm.State);
    }

    [Fact]
    public async Task Run_WithoutNoticeConfirmation_SendsNothing()
    {
        HaltingCheck check = new();
        CheckViewModel vm = BuildGuarded(new FakeCheckRunnerFactory(check), new FakeConfirmationService(confirms: false), TestGuards.Create());
        vm.AddressInput = "beispiel.de";

        await vm.RunCommand.ExecuteAsync(null);

        Assert.Equal(0, check.Calls);
        Assert.Equal(CheckRunState.Idle, vm.State);
    }

    [Fact]
    public async Task Run_ByDefault_UsesStandardRunWithoutAskingForPaths()
    {
        FakeCheckRunnerFactory factory = OkFactory();
        FakeConfirmationService confirmation = new(confirms: true);
        CheckViewModel vm = BuildGuarded(factory, confirmation, TestGuards.Create());
        vm.AddressInput = "beispiel.de";

        await vm.RunCommand.ExecuteAsync(null);

        Assert.Equal(CheckRunOptions.Standard, factory.LastOptions);
        Assert.Equal(0, confirmation.SensitivePathsCount);
    }

    [Fact]
    public async Task Run_WithSensitivePaths_ConfirmsEachRunAndResetsTheChoice()
    {
        FakeCheckRunnerFactory factory = OkFactory();
        FakeConfirmationService confirmation = new(confirms: true);
        CheckViewModel vm = BuildGuarded(factory, confirmation, TestGuards.Create());
        vm.AddressInput = "beispiel.de";
        vm.IncludeSensitivePaths = true;

        await vm.RunCommand.ExecuteAsync(null);

        Assert.Equal(CheckRunOptions.WithSensitivePaths, factory.LastOptions);
        Assert.Equal(1, confirmation.SensitivePathsCount);
        Assert.False(vm.IncludeSensitivePaths);
    }

    [Fact]
    public async Task Run_WhenHalted_ShowsItAndKeepsNoHistory()
    {
        FakeHistoryStore history = new();
        CheckViewModel vm = BuildGuarded(new FakeCheckRunnerFactory(new HaltingCheck()), new FakeConfirmationService(confirms: true), TestGuards.Create(), history);
        vm.AddressInput = "beispiel.de";

        await vm.RunCommand.ExecuteAsync(null);

        Assert.True(vm.WasHalted);
        Assert.Empty(history.Appended);
        Assert.Equal("Status_Check_Halted", vm.HaltMessage);
        Assert.NotEqual(vm.HaltMessage, vm.StatusText);
        Assert.StartsWith("Status_Check_Summary|", vm.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_DuringHalt_WithoutConfirmation_SendsNothingAndKeepsHalt()
    {
        HostRequestGuard guard = TestGuards.Create();
        await guard.StartCooldownAsync("beispiel.de", TestContext.Current.CancellationToken);
        HaltingCheck check = new();
        FakeConfirmationService confirmation = new(confirms: false);
        CheckViewModel vm = BuildGuarded(new FakeCheckRunnerFactory(check), confirmation, guard);
        vm.AddressInput = "beispiel.de";

        await vm.RunCommand.ExecuteAsync(null);

        Assert.Equal(1, confirmation.RetryCount);
        Assert.Equal(0, check.Calls);
        Assert.True((await guard.GetStatusAsync("beispiel.de", TestContext.Current.CancellationToken)).IsBlocked);
    }

    [Fact]
    public async Task Run_DuringHalt_WithConfirmation_LiftsHalt()
    {
        HostRequestGuard guard = TestGuards.Create();
        await guard.StartCooldownAsync("beispiel.de", TestContext.Current.CancellationToken);
        CheckViewModel vm = BuildGuarded(OkFactory(), new FakeConfirmationService(confirms: true), guard);
        vm.AddressInput = "beispiel.de";

        await vm.RunCommand.ExecuteAsync(null);

        Assert.False((await guard.GetStatusAsync("beispiel.de", TestContext.Current.CancellationToken)).IsBlocked);
        Assert.Equal(CheckRunState.Completed, vm.State);
    }
}
