using SiteCheck.Data.Update;
using Microsoft.Extensions.Logging.Abstractions;

namespace SiteCheck.Data.Tests.Update;

/// <summary>
/// Tests für die Sicherheits-Vorbedingungen von <see cref="HttpGetClient"/>.
/// Der HTTPS-Zwang greift vor jedem Netzwerkzugriff und ist daher ohne Server testbar.
/// </summary>
public sealed class HttpGetClientTests
{
    [Fact]
    public async Task DownloadFileAsync_RejectsNonHttpsScheme_ReturnsFalseAndWritesNothing()
    {
        using HttpGetClient client = new(NullLogger<HttpGetClient>.Instance);
        string destination = Path.Combine(Path.GetTempPath(), $"sitecheck-http-test-{Guid.NewGuid():N}.zip");

        bool result = await client.DownloadFileAsync(new Uri("http://github.com/x.zip"), destination, TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.False(File.Exists(destination));
    }
}
