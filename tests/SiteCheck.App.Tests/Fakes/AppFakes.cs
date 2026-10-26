using SiteCheck.App.Services;
using SiteCheck.Application;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Stress;

namespace SiteCheck.App.Tests.Fakes;

/// <summary>Fabrik, die je Aufruf einen Runner über die vorgegebenen Prüfungen baut.</summary>
internal sealed class FakeCheckRunnerFactory(params ICheck[] checks) : ICheckRunnerFactory
{
    public CheckRunner Create() => new(checks);
}

/// <summary>Lastsonde, die nie aufgerufen werden sollte (für Tests, die den Lauf vorher abfangen).</summary>
internal sealed class NoopLoadProbe : ILoadProbe
{
    public Task<RequestOutcome> SendAsync(Uri address, CancellationToken cancellationToken) =>
        Task.FromResult(new RequestOutcome(1, 200));
}

/// <summary>Prüfung mit festem Ergebnis für ViewModel-Tests.</summary>
internal sealed class StubCheck(CheckCategory category, IReadOnlyList<Finding> findings) : ICheck
{
    private readonly IReadOnlyList<Finding> _findings = findings;

    public CheckCategory Category { get; } = category;

    public string Id => "stub." + Category;

    public Task<IReadOnlyList<Finding>> RunAsync(CheckTarget target, CancellationToken cancellationToken) =>
        Task.FromResult(_findings);
}

/// <summary>Verlauf-Speicher, der nur mitschreibt, was angehängt wurde.</summary>
internal sealed class FakeHistoryStore : IHistoryStore
{
    private readonly List<HistoryEntry> _seeded = [];

    public List<(string Address, HistoryEntry Entry)> Appended { get; } = [];

    public List<string> Cleared { get; } = [];

    /// <summary>Legt einen vorigen Lauf an, den der nächste Vergleich heranzieht (neuester zuerst).</summary>
    public void Seed(HistoryEntry entry) => _seeded.Insert(0, entry);

    public Task<IReadOnlyList<HistoryEntry>> LoadAsync(string address, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<HistoryEntry>>([.. _seeded]);

    public Task AppendAsync(string address, HistoryEntry entry, CancellationToken cancellationToken)
    {
        Appended.Add((address, entry));
        return Task.CompletedTask;
    }

    public Task ClearAsync(string address, CancellationToken cancellationToken)
    {
        Cleared.Add(address);
        return Task.CompletedTask;
    }
}

/// <summary>Seiten-Speicher im Arbeitsspeicher.</summary>
internal sealed class FakeSiteStore : ISiteStore
{
    private List<Site> _sites = [];

    public Task<IReadOnlyList<Site>> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Site>>([.. _sites]);

    public Task SaveAsync(IReadOnlyList<Site> sites, CancellationToken cancellationToken)
    {
        _sites = [.. sites];
        return Task.CompletedTask;
    }
}

/// <summary>Export-Dienst, der sich nur die Aufrufe merkt.</summary>
internal sealed class FakeReportExportService : IReportExportService
{
    public int CopyCount { get; private set; }

    public void CopyTextToClipboard(CheckReport report) => CopyCount++;

    public Task<string?> SaveHtmlAsync(CheckReport report, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);
}

/// <summary>Bestätigungsdienst mit vorgegebener Antwort.</summary>
internal sealed class FakeConfirmationService(bool confirms) : IConfirmationService
{
    public int ConfirmCount { get; private set; }

    public Task ShowScopeNoticeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<bool> ConfirmStressTestOwnershipAsync(string address, CancellationToken cancellationToken)
    {
        ConfirmCount++;
        return Task.FromResult(confirms);
    }
}
