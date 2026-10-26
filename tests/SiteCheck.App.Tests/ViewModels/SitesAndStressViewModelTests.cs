using Microsoft.Extensions.Time.Testing;
using SiteCheck.App.Tests.Fakes;
using SiteCheck.App.ViewModels;
using SiteCheck.Application;
using SiteCheck.Core.Models;

namespace SiteCheck.App.Tests.ViewModels;

public sealed class SitesAndStressViewModelTests
{
    private static SitesViewModel BuildSites(FakeSiteStore store, FakeHistoryStore? history = null, params SiteCheck.Core.Abstractions.ICheck[] checks)
    {
        FakeCheckRunnerFactory factory = new(checks.Length == 0
            ? [new StubCheck(CheckCategory.Web, [new Finding("w", CheckCategory.Web, Severity.Ok, "ok")])]
            : checks);
        return new SitesViewModel(store, history ?? new FakeHistoryStore(), factory, new FakeTimeProvider());
    }

    [Fact]
    public async Task Sites_Add_NormalizesAndPersists()
    {
        FakeSiteStore store = new();
        SitesViewModel vm = BuildSites(store);
        await vm.LoadAsync(TestContext.Current.CancellationToken);
        vm.NewName = "Musik";
        vm.NewAddress = "music.vreka-music.com";

        await vm.AddCommand.ExecuteAsync(null);

        _ = Assert.Single(vm.Sites);
        Assert.Equal("https://music.vreka-music.com/", vm.Sites[0].Address);
        Assert.False(vm.IsEmpty);

        // Neu laden aus dem Speicher bestätigt, dass gespeichert wurde.
        SitesViewModel reloaded = BuildSites(store);
        await reloaded.LoadAsync(TestContext.Current.CancellationToken);
        _ = Assert.Single(reloaded.Sites);
    }

    [Fact]
    public async Task Sites_Add_RejectsDuplicate()
    {
        FakeSiteStore store = new();
        SitesViewModel vm = BuildSites(store);
        vm.NewAddress = "beispiel.de";
        await vm.AddCommand.ExecuteAsync(null);
        vm.NewAddress = "beispiel.de";

        await vm.AddCommand.ExecuteAsync(null);

        _ = Assert.Single(vm.Sites);
        Assert.True(vm.HasValidationMessage);
    }

    [Fact]
    public async Task Sites_Check_RaisesEventWithAddress()
    {
        SitesViewModel vm = BuildSites(new FakeSiteStore());
        string? requested = null;
        vm.CheckRequested += (_, e) => requested = e.Address;
        vm.NewAddress = "beispiel.de";
        await vm.AddCommand.ExecuteAsync(null);

        vm.CheckCommand.Execute(vm.Sites[0]);

        Assert.Equal("https://beispiel.de/", requested);
    }

    [Fact]
    public async Task Sites_CheckAll_SetsAmpelAndWritesHistory()
    {
        FakeSiteStore store = new();
        FakeHistoryStore history = new();
        SitesViewModel vm = BuildSites(
            store,
            history,
            new StubCheck(CheckCategory.Mail, [new Finding("m", CheckCategory.Mail, Severity.Warning, "Mangel")]));
        vm.NewAddress = "beispiel.de";
        await vm.AddCommand.ExecuteAsync(null);

        await vm.CheckAllCommand.ExecuteAsync(null);

        Assert.Equal(Severity.Warning, vm.Sites[0].LastWorst);
        _ = Assert.Single(history.Appended);
        Assert.False(vm.IsCheckingAll);
    }

    [Fact]
    public async Task Stress_WithoutConfirmation_DoesNotRun()
    {
        FakeConfirmationService confirmation = new(confirms: false);
        StressTestViewModel vm = new(new StressTestCoordinator(new SiteCheck.App.Tests.Fakes.NoopLoadProbe()), confirmation);
        vm.AddressInput = "beispiel.de";

        await vm.StartCommand.ExecuteAsync(null);

        Assert.Equal(1, confirmation.ConfirmCount);
        Assert.Equal(CheckRunState.Idle, vm.State);
        Assert.False(vm.HasResult);
    }

    [Fact]
    public async Task Stress_WithInvalidAddress_ShowsValidation()
    {
        StressTestViewModel vm = new(new StressTestCoordinator(new NoopLoadProbe()), new FakeConfirmationService(confirms: true));
        vm.AddressInput = "ohne punkt";

        await vm.StartCommand.ExecuteAsync(null);

        Assert.True(vm.HasValidationMessage);
        Assert.Equal(CheckRunState.Idle, vm.State);
    }
}
