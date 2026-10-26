using Microsoft.Extensions.Time.Testing;
using SiteCheck.Application.Tests.Fakes;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Probes;
using SiteCheck.Core.Models.Stress;

namespace SiteCheck.Application.Tests;

public sealed class CatalogAndFactoryTests
{
    [Fact]
    public void CheckCatalog_CreateDefault_ReturnsWebThenMailInOrder()
    {
        IReadOnlyList<ICheck> checks = CheckCatalog.CreateDefault(
            new SilentHttpProbe(), new SilentTlsProbe(), new SilentDnsProbe(), new FakeTimeProvider());

        Assert.NotEmpty(checks);
        int firstMail = checks.ToList().FindIndex(c => c.Category == CheckCategory.Mail);
        int lastWeb = checks.ToList().FindLastIndex(c => c.Category == CheckCategory.Web);
        Assert.True(lastWeb < firstMail, "Alle Web-Prüfungen stehen vor den Mail-Prüfungen.");
    }

    [Fact]
    public async Task CheckRunnerFactory_Create_ProducesWorkingRunner()
    {
        CheckRunnerFactory factory = new(new SilentHttpProbe(), new SilentTlsProbe(), new SilentDnsProbe(), new FakeTimeProvider());
        Assert.True(CheckTarget.TryParse("https://beispiel.de", out CheckTarget? target, out _));

        CheckReport report = await factory.Create().RunAsync(target!, new FakeTimeProvider(), null, TestContext.Current.CancellationToken);

        Assert.Equal("https://beispiel.de/", report.Address);
        Assert.NotEmpty(report.Findings);
    }

    [Fact]
    public async Task StressTest_ReportsProgress()
    {
        List<StressTestProgress> updates = [];
        StressTestCoordinator coordinator = new(new FakeLoadProbe(elapsedMs: 1));
        Assert.True(CheckTarget.TryParse("https://beispiel.de", out CheckTarget? target, out _));
        Progress<StressTestProgress> progress = new(updates.Add);

        StressTestResult result = await coordinator.RunAsync(
            target!, new StressTestOptions(2, TimeSpan.FromSeconds(5)), ownershipConfirmed: true, progress, TestContext.Current.CancellationToken);

        // Mindestens der Abschluss-Fortschritt wird gemeldet.
        Assert.NotEmpty(updates);
        Assert.Equal(result.TotalRequests, updates[^1].CompletedRequests);
    }

    private sealed class SilentHttpProbe : IHttpProbe
    {
        public Task<HttpProbeResponse?> FetchAsync(Uri address, CancellationToken cancellationToken) => Task.FromResult<HttpProbeResponse?>(null);
        public Task<HttpProbeResponse?> FetchWithoutRedirectAsync(Uri address, CancellationToken cancellationToken) => Task.FromResult<HttpProbeResponse?>(null);
    }

    private sealed class SilentTlsProbe : ITlsProbe
    {
        public Task<TlsHandshakeResult?> HandshakeAsync(string host, int port, CancellationToken cancellationToken) => Task.FromResult<TlsHandshakeResult?>(null);
    }

    private sealed class SilentDnsProbe : IDnsProbe
    {
        public Task<IReadOnlyList<string>> QueryMailExchangersAsync(string domain, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyList<string>> QueryTextRecordsAsync(string name, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyList<string>> ResolveIpv4Async(string host, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<string?> QueryPointerAsync(string ipAddress, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        public Task<bool> IsDnssecSignedAsync(string domain, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<IReadOnlyList<string>> QueryCaaAsync(string domain, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<bool> HasTlsaAsync(string name, CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
