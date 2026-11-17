using Microsoft.Extensions.DependencyInjection;
using SiteCheck.App.Composition;
using SiteCheck.Core.Abstractions;
using SiteCheck.Data.Demo;
using SiteCheck.Data.Probes;
using SiteCheck.Data.Storage;
using SiteCheck.Data.Update;

namespace SiteCheck.App.Tests.Services;

/// <summary>
/// Der Vorführmodus verspricht: keine Netzanfrage und die echten Daten bleiben unberührt. Beides
/// entscheidet sich in der Verdrahtung, deshalb wird sie hier geprüft.
/// </summary>
public sealed class DemoModeTests
{
    [Theory]
    [InlineData(new[] { "SiteCheck.exe", "--demo" }, true)]
    [InlineData(new[] { "SiteCheck.exe", "--DEMO" }, true)]
    [InlineData(new[] { "SiteCheck.exe" }, false)]
    [InlineData(new[] { "--demo" }, false)]
    [InlineData(new[] { "SiteCheck.exe", "--apply-update" }, false)]
    public void IsRequested_LooksOnlyAtRealArguments(string[] arguments, bool expected) =>
        Assert.Equal(expected, DemoMode.IsRequested(arguments));

    [Fact]
    public void PathsBeside_KeepsDemoDataInItsOwnFolder()
    {
        AppPaths normal = new(Path.Combine(Path.GetTempPath(), "SiteCheck"));

        AppPaths demo = DemoMode.PathsBeside(normal);

        Assert.Equal(Path.Combine(normal.RootDirectory, "demo"), demo.RootDirectory);
        Assert.NotEqual(normal.SitesFile, demo.SitesFile);
        Assert.NotEqual(normal.RequestGuardFile, demo.RequestGuardFile);
        Assert.StartsWith(demo.RootDirectory, demo.LogDirectory, StringComparison.Ordinal);
    }

    [Fact]
    public void ResetDirectory_RemovesEarlierDemoData()
    {
        AppPaths paths = new(Path.Combine(Path.GetTempPath(), "sitecheck-demo-" + Guid.NewGuid().ToString("N")));
        try
        {
            _ = Directory.CreateDirectory(paths.HistoryDirectory);
            File.WriteAllText(paths.SitesFile, "[]");

            DemoMode.ResetDirectory(paths);

            Assert.True(Directory.Exists(paths.RootDirectory));
            Assert.Empty(Directory.EnumerateFileSystemEntries(paths.RootDirectory));
        }
        finally
        {
            Directory.Delete(paths.RootDirectory, recursive: true);
        }
    }

    [Fact]
    public void Demo_WiresNoNetworkAccess()
    {
        IServiceProvider services = ServiceRegistration.Build(new AppRunMode(IsDemo: true), new AppPaths(Path.GetTempPath()));

        _ = Assert.IsType<DemoHttpProbe>(services.GetRequiredService<IHttpProbe>());
        _ = Assert.IsType<DemoTlsProbe>(services.GetRequiredService<ITlsProbe>());
        _ = Assert.IsType<DemoDnsProbe>(services.GetRequiredService<IDnsProbe>());
        _ = Assert.IsType<DemoLoadProbe>(services.GetRequiredService<ILoadProbe>());
        _ = Assert.IsType<OfflineHttpGetClient>(services.GetRequiredService<IHttpGetClient>());
        _ = Assert.IsType<DemoUpdateChecker>(services.GetRequiredService<IUpdateChecker>());
    }

    [Fact]
    public void Normal_WiresTheRealProbes()
    {
        IServiceProvider services = ServiceRegistration.Build(AppRunMode.Normal, new AppPaths(Path.GetTempPath()));

        _ = Assert.IsType<HttpProbe>(services.GetRequiredService<IHttpProbe>());
        _ = Assert.IsType<HttpGetClient>(services.GetRequiredService<IHttpGetClient>());
        Assert.False(services.GetRequiredService<AppRunMode>().IsDemo);
    }
}
