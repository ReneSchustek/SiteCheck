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
            new SilentHttpProbe(), new SilentTlsProbe(), new StubDnsProbe(resolves: false), new FakeTimeProvider(), CheckRunOptions.Standard);

        Assert.NotEmpty(checks);
        int firstMail = checks.ToList().FindIndex(c => c.Category == CheckCategory.Mail);
        int lastWeb = checks.ToList().FindLastIndex(c => c.Category == CheckCategory.Web);
        Assert.True(lastWeb < firstMail, "Alle Web-Prüfungen stehen vor den Mail-Prüfungen.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EveryCheck_HasAReadableNameInGermanAndEnglish(bool sensitivePaths)
    {
        IReadOnlyList<ICheck> checks = CheckCatalog.CreateDefault(
            new SilentHttpProbe(), new SilentTlsProbe(), new StubDnsProbe(resolves: false), new FakeTimeProvider(),
            sensitivePaths ? CheckRunOptions.WithSensitivePaths : CheckRunOptions.Standard);

        List<string> german = [.. checks.Select(check => check.Name)];
        List<string> english = InCulture("en-US", () => checks.Select(check => check.Name).ToList());

        for (int index = 0; index < checks.Count; index++)
        {
            string id = checks[index].Id;
            Assert.False(string.IsNullOrWhiteSpace(german[index]), id);
            Assert.DoesNotContain(id, german[index], StringComparison.Ordinal);
            Assert.Contains(german[index], Core.Localization.DomainText.EnglishKeys);
            Assert.False(string.IsNullOrWhiteSpace(english[index]), id);
        }

        Assert.Equal(german.Count, german.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void HaltedCheck_NamesTheCheckWithoutItsId()
    {
        Core.Checks.MtaStsPolicyCheck check = new(new SilentHttpProbe());

        Finding german = HaltedFindings.ForCheck(check);
        Finding english = InCulture("en-US", () => HaltedFindings.ForCheck(check));

        Assert.Equal("Nicht geprüft: MTA-STS-Richtlinie", german.Title);
        Assert.Equal("Not checked: MTA-STS policy", english.Title);
        Assert.All([german.Title, german.Detail ?? string.Empty, english.Title, english.Detail ?? string.Empty], text =>
            Assert.DoesNotContain(check.Id, text, StringComparison.Ordinal));
        Assert.Equal(check.Id + CheckReport.NotCheckedKeySuffix, german.Key);
    }

    [Fact]
    public async Task CheckRunnerFactory_Create_ProducesWorkingRunner()
    {
        FakeTimeProvider clock = new() { AutoAdvanceAmount = TimeSpan.FromSeconds(2) };
        StubDnsProbe dns = new(resolves: false);
        CheckRunnerFactory factory = new(new SilentHttpProbe(), new SilentTlsProbe(), dns, clock, new HostRequestGuard(clock, new InMemoryGuardStore(), dns));
        Assert.True(CheckTarget.TryParse("https://beispiel.de", out CheckTarget? target, out _));

        CheckReport report = await factory.Create(CheckRunOptions.Standard).RunAsync(target!, new FakeTimeProvider(), null, TestContext.Current.CancellationToken);

        Assert.Equal("https://beispiel.de/", report.Address);
        Assert.NotEmpty(report.Findings);
    }

    [Fact]
    public async Task StressTest_ReportsProgress()
    {
        List<StressTestProgress> updates = [];
        StressTestCoordinator coordinator = new(new FakeLoadProbe(elapsedMs: 1), new HostRequestGuard(TimeProvider.System, new InMemoryGuardStore(), new StubDnsProbe()));
        Assert.True(CheckTarget.TryParse("https://beispiel.de", out CheckTarget? target, out _));
        Progress<StressTestProgress> progress = new(updates.Add);

        StressTestResult result = await coordinator.RunAsync(
            target!, new StressTestOptions(2, TimeSpan.FromSeconds(5)), ownershipConfirmed: true, progress, TestContext.Current.CancellationToken);

        // Mindestens der Abschluss-Fortschritt wird gemeldet.
        Assert.NotEmpty(updates);
        Assert.Equal(result.TotalRequests, updates[^1].CompletedRequests);
    }

    private static T InCulture<T>(string culture, Func<T> action)
    {
        System.Globalization.CultureInfo previous = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo(culture);
        try
        {
            return action();
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = previous;
        }
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
}
