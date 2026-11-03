using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using SiteCheck.Data.Logging;

namespace SiteCheck.Data.Tests.Logging;

public sealed class FileLoggerProviderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "sitecheck-log-test-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Log_WritesLevelCategoryMessageAndException()
    {
        using FileLoggerProvider provider = new(_directory, _clock);

        provider.CreateLogger("Bereich").Log(LogLevel.Error, new EventId(1), "Meldung", new IOException("kaputt"), (state, _) => state);

        string text = File.ReadAllText(Assert.Single(Directory.GetFiles(_directory)));
        Assert.Contains("Error Bereich: Meldung", text, StringComparison.Ordinal);
        Assert.Contains("kaputt", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Log_BelowInformation_WritesNothing()
    {
        using FileLoggerProvider provider = new(_directory, _clock);

        provider.CreateLogger("Bereich").Log(LogLevel.Debug, new EventId(1), "Meldung", null, (state, _) => state);

        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public void Log_OnNewDay_KeepsOnlyTheLastFourteenFiles()
    {
        using FileLoggerProvider provider = new(_directory, _clock);
        ILogger logger = provider.CreateLogger("Bereich");

        for (int day = 0; day < 20; day++)
        {
            logger.Log(LogLevel.Information, new EventId(1), "Meldung", null, (state, _) => state);
            _clock.Advance(TimeSpan.FromDays(1));
        }

        string[] files = [.. Directory.GetFiles(_directory).Select(Path.GetFileName).Order(StringComparer.Ordinal)!];
        Assert.Equal(14, files.Length);
        Assert.Contains(_clock.GetLocalNow().AddDays(-1).ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture), files[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void Log_WhenDirectoryCannotBeCreated_DoesNotThrow()
    {
        string blocker = _directory + ".blocker";
        File.WriteAllText(blocker, string.Empty);
        try
        {
            using FileLoggerProvider provider = new(Path.Combine(blocker, "logs"), _clock);

            provider.CreateLogger("Bereich").Log(LogLevel.Error, new EventId(1), "Meldung", null, (state, _) => state);
        }
        finally
        {
            File.Delete(blocker);
        }
    }
}
