using System.Text.RegularExpressions;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;

namespace SiteCheck.Data.Logging;

/// <summary>Liest die Tagesdateien von <see cref="FileLoggerProvider"/> zurück in Einträge.</summary>
public sealed partial class LogFileReader(string directory) : ILogReader
{
    // Mehr zeigt die Ansicht nicht: Wer weiter zurück will, öffnet die Datei.
    private const int MaxEntries = 2000;

    private readonly string _directory = directory;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<LogEntry>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_directory))
        {
            return [];
        }

        List<LogEntry> entries = [];
        foreach (string file in Directory.GetFiles(_directory, "sitecheck-*.log").Order(StringComparer.Ordinal).Reverse())
        {
            string[] lines;
            try
            {
                lines = await File.ReadAllLinesAsync(file, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Eine gerade gesperrte Tagesdatei fehlt beim nächsten Aktualisieren nicht mehr.
                continue;
            }

            entries.AddRange(Enumerable.Reverse(Parse(lines)));
            if (entries.Count >= MaxEntries)
            {
                break;
            }
        }

        return entries.Count > MaxEntries ? entries[..MaxEntries] : entries;
    }

    // Zeilen ohne Zeitstempel gehören zum Eintrag davor, etwa der Stapel einer Ausnahme.
    private static List<LogEntry> Parse(string[] lines)
    {
        List<LogEntry> entries = [];
        foreach (string line in lines)
        {
            Match match = EntryStart().Match(line);
            if (match.Success)
            {
                entries.Add(new LogEntry(match.Groups["time"].Value, ToLevel(match.Groups["level"].Value), match.Groups["text"].Value));
            }
            else if (entries.Count > 0)
            {
                entries[^1] = entries[^1] with { Text = entries[^1].Text + Environment.NewLine + line };
            }
        }

        return entries;
    }

    private static LogEntryLevel ToLevel(string level) => level switch
    {
        "Warning" => LogEntryLevel.Warning,
        "Error" or "Critical" => LogEntryLevel.Error,
        _ => LogEntryLevel.Information,
    };

    [GeneratedRegex(@"^(?<time>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}) (?<level>[A-Za-z]+) (?<text>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex EntryStart();
}
