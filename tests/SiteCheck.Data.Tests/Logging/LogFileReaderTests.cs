using SiteCheck.Core.Models;
using SiteCheck.Data.Logging;

namespace SiteCheck.Data.Tests.Logging;

public sealed class LogFileReaderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "sitecheck-logread-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task LogFileReader_ReadsNewestFirst_AndKeepsExceptionWithItsEntry()
    {
        _ = Directory.CreateDirectory(_directory);
        await File.WriteAllLinesAsync(
            Path.Combine(_directory, "sitecheck-20261102.log"),
            ["2026-11-02 10:00:00 Information Bereich: alt"],
            TestContext.Current.CancellationToken);
        await File.WriteAllLinesAsync(
            Path.Combine(_directory, "sitecheck-20261103.log"),
            ["2026-11-03 10:00:00 Warning Bereich: erst", "2026-11-03 11:00:00 Error Bereich: kaputt", "   at Stapel"],
            TestContext.Current.CancellationToken);

        IReadOnlyList<LogEntry> entries = await new LogFileReader(_directory).ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, entries.Count);
        Assert.Equal(LogEntryLevel.Error, entries[0].Level);
        Assert.Contains("at Stapel", entries[0].Text, StringComparison.Ordinal);
        Assert.Equal(LogEntryLevel.Warning, entries[1].Level);
        Assert.Equal("Bereich: alt", entries[2].Text);
    }

    [Fact]
    public async Task LogFileReader_WithoutDirectory_IsEmpty()
    {
        Assert.Empty(await new LogFileReader(_directory).ReadAsync(TestContext.Current.CancellationToken));
    }
}
