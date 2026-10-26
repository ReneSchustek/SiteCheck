using SiteCheck.Data.Probes;
using SiteCheck.Data.Storage;

namespace SiteCheck.Data.Tests.Probes;

public sealed class ProbeHttpClientsTests
{
    [Fact]
    public void CreateFollowing_HasUserAgentAndTimeout()
    {
        using HttpClient client = ProbeHttpClients.CreateFollowing();

        Assert.Contains("SiteCheck", client.DefaultRequestHeaders.UserAgent.ToString(), StringComparison.Ordinal);
        Assert.True(client.Timeout > TimeSpan.Zero);
    }

    [Fact]
    public void CreateNonFollowing_AndForLoad_AreUsable()
    {
        using HttpClient nonFollowing = ProbeHttpClients.CreateNonFollowing();
        using HttpClient load = ProbeHttpClients.CreateForLoad();

        Assert.NotNull(nonFollowing);
        // Der Lasttest-Client bekommt ein knapperes Timeout als die gründliche Prüfung.
        using HttpClient following = ProbeHttpClients.CreateFollowing();
        Assert.True(load.Timeout <= following.Timeout);
    }

    [Fact]
    public void AppPaths_Default_PlacesFilesUnderLocalAppData()
    {
        AppPaths paths = new();

        string expectedRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SiteCheck");
        Assert.Equal(expectedRoot, paths.RootDirectory);
        Assert.StartsWith(expectedRoot, paths.SettingsFile, StringComparison.Ordinal);
        Assert.StartsWith(expectedRoot, paths.HistoryDirectory, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadProbe_OnConnectionError_ReturnsFailureOutcome()
    {
        using HttpClient client = new(new FailingHandler()) { Timeout = TimeSpan.FromSeconds(5) };
        LoadProbe probe = new(client);

        Core.Models.Stress.RequestOutcome outcome = await probe.SendAsync(new Uri("https://beispiel.de/"), TestContext.Current.CancellationToken);

        Assert.False(outcome.IsSuccess);
        Assert.Null(outcome.StatusCode);
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("verbindung fehlgeschlagen");
    }
}
