using SiteCheck.Core.Models;
using SiteCheck.Data.Storage;

namespace SiteCheck.Data.Tests.Storage;

/// <summary>
/// Prüft die JSON-Speicher gegen einen echten, aber eigenen temporären Ordner. Kein Netz,
/// kein gemeinsamer Zustand: Jeder Test bekommt seinen eigenen Pfad und räumt ihn wieder weg.
/// </summary>
public sealed class StoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sitecheck-test-" + Guid.NewGuid().ToString("N"));
    private readonly AppPaths _paths;

    public StoreTests() => _paths = new AppPaths(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Settings_RoundTrip()
    {
        SettingsStore store = new(_paths);
        AppSettings settings = new(1200, 750, ScopeNoticeAcknowledged: true);

        await store.SaveAsync(settings, TestContext.Current.CancellationToken);
        AppSettings loaded = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(settings, loaded);
    }

    [Fact]
    public async Task Settings_WhenMissing_ReturnsDefault()
    {
        SettingsStore store = new(_paths);

        AppSettings loaded = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AppSettings.Default, loaded);
    }

    [Fact]
    public async Task Settings_WhenFileCorrupt_ReturnsDefaultWithoutThrowing()
    {
        _ = Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(_paths.SettingsFile, "{ kaputt", TestContext.Current.CancellationToken);
        SettingsStore store = new(_paths);

        AppSettings loaded = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AppSettings.Default, loaded);
    }

    [Fact]
    public async Task Sites_RoundTrip_PreservesOrder()
    {
        SiteStore store = new(_paths);
        IReadOnlyList<Site> sites =
        [
            new Site("Musik", "https://music.vreka-music.com/"),
            new Site("Haupt", "https://vreka-music.com/"),
        ];

        await store.SaveAsync(sites, TestContext.Current.CancellationToken);
        IReadOnlyList<Site> loaded = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(sites, loaded);
    }

    [Fact]
    public async Task History_AppendThenLoad_NewestFirst()
    {
        HistoryStore store = new(_paths);
        const string address = "https://beispiel.de/";
        HistoryEntry older = Entry(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        HistoryEntry newer = Entry(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));

        await store.AppendAsync(address, older, TestContext.Current.CancellationToken);
        await store.AppendAsync(address, newer, TestContext.Current.CancellationToken);
        IReadOnlyList<HistoryEntry> loaded = await store.LoadAsync(address, TestContext.Current.CancellationToken);

        Assert.Equal(2, loaded.Count);
        Assert.Equal(newer.RanAt, loaded[0].RanAt);
    }

    [Fact]
    public async Task History_Clear_RemovesEntries()
    {
        HistoryStore store = new(_paths);
        const string address = "https://beispiel.de/";
        await store.AppendAsync(address, Entry(DateTimeOffset.UnixEpoch), TestContext.Current.CancellationToken);

        await store.ClearAsync(address, TestContext.Current.CancellationToken);
        IReadOnlyList<HistoryEntry> loaded = await store.LoadAsync(address, TestContext.Current.CancellationToken);

        Assert.Empty(loaded);
    }

    [Fact]
    public async Task History_PreservesFindingsForComparison()
    {
        HistoryStore store = new(_paths);
        const string address = "https://beispiel.de/";
        Finding finding = new("web.tls", CheckCategory.Web, Severity.Warning, "Titel", "Detail", new Recommendation("Rat", "Beispiel"));
        HistoryEntry entry = new(DateTimeOffset.UnixEpoch, FindingSummary.From([finding]), [finding]);

        await store.AppendAsync(address, entry, TestContext.Current.CancellationToken);
        IReadOnlyList<HistoryEntry> loaded = await store.LoadAsync(address, TestContext.Current.CancellationToken);

        Finding restored = Assert.Single(loaded[0].Findings);
        Assert.Equal(finding, restored);
    }

    private static HistoryEntry Entry(DateTimeOffset ranAt) =>
        new(ranAt, new FindingSummary(1, 0, 0, 0), [new Finding("ok", CheckCategory.Web, Severity.Ok, "ok")]);
}
