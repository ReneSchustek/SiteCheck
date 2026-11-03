using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SiteCheck.App.Services;
using SiteCheck.App.Tests.Fakes;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Update;
using SiteCheck.Data.Update;

namespace SiteCheck.App.Tests.Services;

public sealed class UpdateOrchestratorTests : IDisposable
{
    private static readonly Version Current = new(1, 0, 0);

    private readonly string _stagingRoot = Path.Combine(Path.GetTempPath(), "sitecheck-orchestrator-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeSettingsStore _settings = new();
    private readonly FakeUpdateChecker _checker = new();
    private readonly FakeGetClient _http = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_stagingRoot))
        {
            Directory.Delete(_stagingRoot, recursive: true);
        }
    }

    private UpdateOrchestrator Build() => new(
        new UpdateInstallerService(_http, NullLogger<UpdateInstallerService>.Instance, _stagingRoot),
        _checker,
        _settings,
        _clock,
        Current,
        NullLogger<UpdateOrchestrator>.Instance);

    private static UpdateInfo Newer(string? package = "https://github.com/x/portable.zip") =>
        UpdateInfo.Create(Current, "v2.0.0", "https://github.com/x/releases/2.0.0", "Notizen", package, "https://github.com/x/portable.zip.sig");

    [Fact]
    public async Task CheckAtStartup_WhenDisabled_AsksNothing()
    {
        _settings.Current = AppSettings.Default with { AutoUpdateEnabled = false };

        await Build().CheckAtStartupAsync(Token);

        Assert.Equal(0, _checker.Calls);
    }

    [Fact]
    public async Task CheckAtStartup_WithinADay_AsksNothing()
    {
        _settings.Current = AppSettings.Default with { LastUpdateCheck = _clock.GetUtcNow().AddHours(-23) };

        await Build().CheckAtStartupAsync(Token);

        Assert.Equal(0, _checker.Calls);
    }

    [Fact]
    public async Task CheckAtStartup_NoUpdate_RemembersTheTime()
    {
        _checker.Result = UpdateInfo.NoUpdate(Current);

        await Build().CheckAtStartupAsync(Token);

        Assert.Equal(1, _checker.Calls);
        Assert.Equal(_clock.GetUtcNow(), _settings.Current.LastUpdateCheck);
    }

    [Fact]
    public async Task CheckAtStartup_WithoutNetwork_EndsQuietlyAndAsksAgainNextTime()
    {
        _checker.Fails = true;

        await Build().CheckAtStartupAsync(Token);

        Assert.Null(_settings.Current.LastUpdateCheck);
    }

    [Fact]
    public async Task CheckAtStartup_UnsignedOrBrokenPackage_ReportsButMarksNothingForInstall()
    {
        _checker.Result = Newer();
        UpdateOrchestrator orchestrator = Build();
        int notified = 0;
        orchestrator.UpdateAvailable += (_, _) => notified++;

        await orchestrator.CheckAtStartupAsync(Token);

        Assert.Equal(1, notified);
        Assert.Equal("2.0.0", orchestrator.Pending?.LatestVersion);
        Assert.Null(_settings.Current.PendingUpdateVersion);
    }

    [Fact]
    public async Task ApplyOnStartup_RecordWithoutPackage_IsCleared()
    {
        _settings.Current = AppSettings.Default with { PendingUpdateVersion = "1.0.0", PendingUpdateSha256 = "abc" };

        bool started = await Build().TryApplyPendingUpdateOnStartupAsync();

        Assert.False(started);
        Assert.Null(_settings.Current.PendingUpdateVersion);
        Assert.Null(_settings.Current.PendingUpdateSha256);
    }

    [Fact]
    public async Task InstallNow_WithoutPackage_StartsNothing()
    {
        _checker.Result = Newer(package: null);
        UpdateOrchestrator orchestrator = Build();
        _ = await orchestrator.CheckNowAsync(Token);

        Assert.False(await orchestrator.InstallNowAsync(Token));
    }

    [Fact]
    public async Task InstallNow_WithBrokenPackage_StartsNothing()
    {
        _checker.Result = Newer();
        UpdateOrchestrator orchestrator = Build();
        _ = await orchestrator.CheckNowAsync(Token);

        Assert.False(await orchestrator.InstallNowAsync(Token));
    }

    [Fact]
    public async Task CheckNow_WithoutNetwork_ReturnsNothing()
    {
        _checker.Fails = true;

        Assert.Null(await Build().CheckNowAsync(Token));
    }

    private sealed class FakeSettingsStore : ISettingsStore
    {
        public AppSettings Current { get; set; } = AppSettings.Default;

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(Current);

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
        {
            Current = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUpdateChecker : IUpdateChecker
    {
        public UpdateInfo Result { get; set; } = UpdateInfo.NoUpdate(Current);

        public bool Fails { get; set; }

        public int Calls { get; private set; }

        public Task<UpdateInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Fails ? throw new HttpRequestException("kein Netz") : Task.FromResult(Result);
        }
    }

    // Lädt nichts: Ein Paket ohne gültige Signatur muss überall abgewiesen werden.
    private sealed class FakeGetClient : IHttpGetClient
    {
        public Task<string?> GetStringAsync(Uri requestUri, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public Task<bool> DownloadFileAsync(Uri requestUri, string destinationPath, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
