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
        return new CheckViewModel(factory, new FakeTimeProvider(), history ?? new FakeHistoryStore(), new FakeReportExportService());
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
        Assert.Contains("1 kritisch", vm.StatusText, StringComparison.Ordinal);
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
        CheckViewModel vm = new(factory, new FakeTimeProvider(), new FakeHistoryStore(), export);
        vm.AddressInput = "beispiel.de";
        await vm.RunCommand.ExecuteAsync(null);

        Assert.True(vm.CopyCommand.CanExecute(null));
        vm.CopyCommand.Execute(null);

        Assert.Equal(1, export.CopyCount);
    }
}
