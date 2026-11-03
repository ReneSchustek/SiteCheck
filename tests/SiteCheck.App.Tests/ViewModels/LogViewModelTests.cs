using SiteCheck.App.Tests.Fakes;
using SiteCheck.App.ViewModels;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;

namespace SiteCheck.App.Tests.ViewModels;

public sealed class LogViewModelTests
{
    private static async Task<LogViewModel> LoadedAsync(params LogEntry[] entries)
    {
        LogViewModel vm = new(new FixedReader(entries), new FakeStringResources());
        await vm.ReloadCommand.ExecuteAsync(null).ConfigureAwait(true);
        return vm;
    }

    [Fact]
    public async Task Reload_WithoutEntries_SaysNothingThereYet()
    {
        LogViewModel vm = await LoadedAsync();

        Assert.True(vm.IsEmpty);
        Assert.Equal("Log_Empty_None", vm.EmptyText);
    }

    [Fact]
    public async Task Search_FiltersWhileTyping_AndSaysNothingFound()
    {
        LogViewModel vm = await LoadedAsync(new LogEntry("t", LogEntryLevel.Information, "Prüflauf beendet"));

        vm.SearchText = "PRÜFLAUF";
        _ = Assert.Single(vm.Entries);

        vm.SearchText = "gibt es nicht";
        Assert.True(vm.IsEmpty);
        Assert.Equal("Log_Empty_NoMatch", vm.EmptyText);
    }

    [Fact]
    public async Task Filter_ShowsOnlyThatLevelAndAbove()
    {
        LogViewModel vm = await LoadedAsync(
            new LogEntry("t", LogEntryLevel.Information, "a"),
            new LogEntry("t", LogEntryLevel.Warning, "b"),
            new LogEntry("t", LogEntryLevel.Error, "c"));
        Assert.True(vm.ShowsAll);

        vm.FilterCommand.Execute(LogEntryLevel.Warning);
        Assert.Equal(2, vm.Entries.Count);
        Assert.True(vm.ShowsWarnings);

        vm.FilterCommand.Execute(LogEntryLevel.Error);
        Assert.Equal("c", Assert.Single(vm.Entries).Text);
        Assert.True(vm.ShowsErrors);
    }

    private sealed class FixedReader(LogEntry[] entries) : ILogReader
    {
        public Task<IReadOnlyList<LogEntry>> ReadAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LogEntry>>(entries);
    }
}
