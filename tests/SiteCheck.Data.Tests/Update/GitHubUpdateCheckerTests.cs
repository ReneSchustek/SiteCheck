using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models.Update;
using SiteCheck.Data.Update;
using Microsoft.Extensions.Logging.Abstractions;

namespace SiteCheck.Data.Tests.Update;

/// <summary>
/// Tests für den <see cref="GitHubUpdateChecker"/> mit gefaketem HTTP-Zugriff.
/// </summary>
public sealed class GitHubUpdateCheckerTests
{
    private sealed class FakeHttpGetClient : IHttpGetClient
    {
        private readonly string? _response;

        public FakeHttpGetClient(string? response) => _response = response;

        public Task<string?> GetStringAsync(Uri requestUri, CancellationToken cancellationToken = default)
            => Task.FromResult(_response);

        public Task<bool> DownloadFileAsync(Uri requestUri, string destinationPath, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private static GitHubUpdateChecker CreateChecker(string? response, Version current)
        => new(new FakeHttpGetClient(response), current, NullLogger<GitHubUpdateChecker>.Instance);

    [Fact]
    public async Task CheckForUpdate_ReportsANewerVersion()
    {
        const string json = """{ "tag_name": "v2.0.0", "html_url": "https://github.com/x/releases/2.0.0", "body": "Neu" }""";
        GitHubUpdateChecker checker = CreateChecker(json, new Version(1, 0, 0));

        UpdateInfo info = await checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        Assert.True(info.IsUpdateAvailable);
        Assert.Equal("2.0.0", info.LatestVersion);
        Assert.NotNull(info.DownloadUrl);
    }

    [Fact]
    public async Task CheckForUpdate_ReadsThePortableArchiveFromTheAssets()
    {
        const string json = """
        {
          "tag_name": "v2.0.0",
          "html_url": "https://github.com/x/releases/2.0.0",
          "body": "Neu",
          "assets": [
            { "name": "SiteCheck-Setup-v2.0.0.exe", "browser_download_url": "https://github.com/x/setup.exe" },
            { "name": "SiteCheck-Portable-v2.0.0.zip", "browser_download_url": "https://github.com/x/portable.zip" }
          ]
        }
        """;
        GitHubUpdateChecker checker = CreateChecker(json, new Version(1, 0, 0));

        UpdateInfo info = await checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        Assert.True(info.IsUpdateAvailable);
        Assert.NotNull(info.PackageUrl);
        Assert.Equal("https://github.com/x/portable.zip", info.PackageUrl!.ToString());
    }

    [Fact]
    public async Task CheckForUpdate_FallsBackToTheFirstArchiveWithoutAPortableOne()
    {
        const string json = """
        {
          "tag_name": "v2.0.0", "html_url": "https://github.com/x", "body": "",
          "assets": [
            { "name": "SiteCheck-v2.0.0.zip", "browser_download_url": "https://github.com/x/a.zip" },
            { "name": "extra-v2.0.0.zip", "browser_download_url": "https://github.com/x/b.zip" }
          ]
        }
        """;
        GitHubUpdateChecker checker = CreateChecker(json, new Version(1, 0, 0));

        UpdateInfo info = await checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        Assert.Equal("https://github.com/x/a.zip", info.PackageUrl!.ToString());
    }

    [Fact]
    public async Task CheckForUpdate_RejectsNonGitHubAndPlainHttpAssets()
    {
        const string json = """
        {
          "tag_name": "v2.0.0", "html_url": "https://github.com/x", "body": "",
          "assets": [
            { "name": "evil-portable.zip", "browser_download_url": "https://evil.example.com/p.zip" },
            { "name": "plain-portable.zip", "browser_download_url": "http://github.com/x/p.zip" }
          ]
        }
        """;
        GitHubUpdateChecker checker = CreateChecker(json, new Version(1, 0, 0));

        UpdateInfo info = await checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        Assert.Null(info.PackageUrl);
    }

    [Fact]
    public async Task CheckForUpdate_AcceptsTheGitHubUserContentHost()
    {
        const string json = """
        {
          "tag_name": "v2.0.0", "html_url": "https://github.com/x", "body": "",
          "assets": [ { "name": "SiteCheck-Portable-v2.0.0.zip", "browser_download_url": "https://objects.githubusercontent.com/abc/p.zip" } ]
        }
        """;
        GitHubUpdateChecker checker = CreateChecker(json, new Version(1, 0, 0));

        UpdateInfo info = await checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        Assert.Equal("https://objects.githubusercontent.com/abc/p.zip", info.PackageUrl!.ToString());
    }

    [Fact]
    public async Task CheckForUpdate_IgnoresNonArchiveAssets()
    {
        const string json = """
        {
          "tag_name": "v2.0.0", "html_url": "https://github.com/x", "body": "",
          "assets": [ { "name": "SiteCheck-Setup.exe", "browser_download_url": "https://github.com/x/s.exe" } ]
        }
        """;
        GitHubUpdateChecker checker = CreateChecker(json, new Version(1, 0, 0));

        UpdateInfo info = await checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        Assert.Null(info.PackageUrl);
    }

    [Fact]
    public async Task CheckForUpdate_WithoutAssets_HasNoPackageAddress()
    {
        const string json = """{ "tag_name": "v2.0.0", "html_url": "https://github.com/x", "body": "" }""";
        GitHubUpdateChecker checker = CreateChecker(json, new Version(1, 0, 0));

        UpdateInfo info = await checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        Assert.Null(info.PackageUrl);
    }

    [Fact]
    public async Task CheckForUpdate_WithTheSameVersion_ReportsNoUpdate()
    {
        const string json = """{ "tag_name": "v1.0.0", "html_url": "https://github.com/x", "body": "" }""";
        GitHubUpdateChecker checker = CreateChecker(json, new Version(1, 0, 0));

        UpdateInfo info = await checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        Assert.False(info.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdate_TreatsANetworkErrorAsNoUpdate()
    {
        GitHubUpdateChecker checker = CreateChecker(response: null, new Version(1, 0, 0));

        UpdateInfo info = await checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        Assert.False(info.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdate_TreatsInvalidJsonAsNoUpdate()
    {
        GitHubUpdateChecker checker = CreateChecker("nicht json", new Version(1, 0, 0));

        UpdateInfo info = await checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        Assert.False(info.IsUpdateAvailable);
    }
}
