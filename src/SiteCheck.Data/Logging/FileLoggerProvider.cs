using System.Globalization;
using Microsoft.Extensions.Logging;

namespace SiteCheck.Data.Logging;

/// <summary>
/// Schreibt das Protokoll in eine Datei je Tag und hebt 14 Tage auf. Die Datei ist lesbar, auch
/// wenn das Programm nicht mehr startet; dafür allein lohnt kein weiteres Paket.
/// </summary>
public sealed class FileLoggerProvider(string directory, TimeProvider clock) : ILoggerProvider
{
    // Zwei Wochen reichen, um einen gemeldeten Fehler nachzuvollziehen, und bleiben klein.
    private const int RetainedFiles = 14;

    private readonly string _directory = directory;
    private readonly TimeProvider _clock = clock;
    private readonly Lock _gate = new();
    private string? _currentPath;

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    /// <inheritdoc/>
    public void Dispose()
    {
        // Jede Zeile öffnet und schließt die Datei selbst; offen bleibt nichts.
    }

    private void Write(string line)
    {
        DateTimeOffset now = _clock.GetLocalNow();
        string text = now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " " + line + Environment.NewLine;
        try
        {
            lock (_gate)
            {
                string path = Path.Combine(_directory, "sitecheck-" + now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");
                if (path != _currentPath)
                {
                    _ = Directory.CreateDirectory(_directory);
                    _currentPath = path;
                    RemoveOldFiles();
                }

                File.AppendAllText(path, text);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ein volles oder gesperrtes Laufwerk darf das Programm nicht beenden.
        }
    }

    private void RemoveOldFiles()
    {
        // Der Name enthält das Datum, also sortiert der Name auch nach dem Alter.
        foreach (string old in Directory.GetFiles(_directory, "sitecheck-*.log").Order(StringComparer.Ordinal).Reverse().Skip(RetainedFiles - 1))
        {
            File.Delete(old);
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            if (IsEnabled(logLevel))
            {
                string suffix = exception is null ? string.Empty : Environment.NewLine + exception;
                provider.Write($"{logLevel} {category}: {formatter(state, exception)}{suffix}");
            }
        }
    }
}
