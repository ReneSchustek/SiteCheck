using SiteCheck.App.Services;
using SiteCheck.Application;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Guard;
using SiteCheck.Core.Models.Stress;

namespace SiteCheck.App.Tests.Fakes;

/// <summary>Fabrik, die je Aufruf einen Runner über die vorgegebenen Prüfungen baut.</summary>
internal sealed class FakeCheckRunnerFactory(params ICheck[] checks) : ICheckRunnerFactory
{
    public CheckRunOptions? LastOptions { get; private set; }

    public CheckRunner Create(CheckRunOptions options)
    {
        LastOptions = options;
        return new CheckRunner(checks);
    }
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

    public string Name => "Stub " + Category;

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

    public int NoticeCount { get; private set; }

    public int SensitivePathsCount { get; private set; }

    public int RetryCount { get; private set; }

    public Task<bool> ConfirmHostNoticeAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken)
    {
        NoticeCount++;
        return Task.FromResult(confirms);
    }

    public Task<bool> ConfirmSensitivePathsAsync(string address, CancellationToken cancellationToken)
    {
        SensitivePathsCount++;
        return Task.FromResult(confirms);
    }

    public Task<bool> ConfirmRetryDuringHaltAsync(string address, string reason, CancellationToken cancellationToken)
    {
        RetryCount++;
        return Task.FromResult(confirms);
    }

    public Task ShowScopeNoticeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<bool> ConfirmStressTestOwnershipAsync(string address, CancellationToken cancellationToken)
    {
        ConfirmCount++;
        return Task.FromResult(confirms);
    }
}

/// <summary>Speicher der Sperrwache im Arbeitsspeicher.</summary>
internal sealed class InMemoryGuardStore : IRequestGuardStore
{
    private IReadOnlyDictionary<string, HostGuardEntry> _hosts = new Dictionary<string, HostGuardEntry>();

    public Task<IReadOnlyDictionary<string, HostGuardEntry>> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(_hosts);

    public Task SaveAsync(IReadOnlyDictionary<string, HostGuardEntry> hosts, CancellationToken cancellationToken)
    {
        _hosts = hosts;
        return Task.CompletedTask;
    }
}

/// <summary>DNS, das nichts kennt; die ViewModel-Tests senden nichts ins Netz.</summary>
internal sealed class EmptyDnsProbe : IDnsProbe
{
    public Task<IReadOnlyList<string>> QueryMailExchangersAsync(string domain, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);

    public Task<IReadOnlyList<string>> QueryTextRecordsAsync(string name, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);

    public Task<IReadOnlyList<string>> ResolveIpv4Async(string host, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);

    public Task<string?> QueryPointerAsync(string ipAddress, CancellationToken cancellationToken) => Task.FromResult<string?>(null);

    public Task<bool> IsDnssecSignedAsync(string domain, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task<IReadOnlyList<string>> QueryCaaAsync(string domain, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);

    public Task<bool> HasTlsaAsync(string name, CancellationToken cancellationToken) => Task.FromResult(false);
}

/// <summary>Baut eine Sperrwache für ViewModel-Tests.</summary>
internal static class TestGuards
{
    public static HostRequestGuard Create(TimeProvider? clock = null) =>
        new(clock ?? TimeProvider.System, new InMemoryGuardStore(), new EmptyDnsProbe());
}

/// <summary>Prüfung, die wie eine angehaltene Sonde sofort den Halt meldet.</summary>
internal sealed class HaltingCheck : ICheck
{
    public int Calls { get; private set; }

    public string Id => "stub.halting";

    public string Name => "Stub, hält an";

    public CheckCategory Category => CheckCategory.Web;

    public Task<IReadOnlyList<Finding>> RunAsync(CheckTarget target, CancellationToken cancellationToken)
    {
        Calls++;
        throw new RequestsHaltedException(RequestHaltReason.HostRejected, target.Host, DateTimeOffset.UnixEpoch);
    }
}

/// <summary>Liefert den Schlüssel statt des Textes, damit Tests die Aussage prüfen und keinen Satzteil.</summary>
internal sealed class FakeStringResources : IStringResources
{
    public string GetString(string key) => key;

    public string GetFormatted(string key, params object[] arguments) => key + "|" + string.Join("|", arguments);
}
