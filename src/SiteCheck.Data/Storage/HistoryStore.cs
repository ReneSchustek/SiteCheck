using System.Security.Cryptography;
using System.Text;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;

namespace SiteCheck.Data.Storage;

/// <summary>
/// Legt je Seite eine eigene Verlaufsdatei an. Der Dateiname ist der SHA-256-Kurzschlüssel der
/// Adresse: So kann jede Adresse — mit Schrägstrichen, Umlauten, Port — einen gültigen,
/// kollisionsfreien Dateinamen bekommen, ohne die Adresse selbst zu zerlegen.
/// </summary>
public sealed class HistoryStore(AppPaths paths) : IHistoryStore
{
    // Nach wie vielen Läufen der älteste wegfällt. 50 reicht für einen aussagekräftigen
    // Verlauf und hält die Datei klein; unbegrenztes Wachstum ist die Regelverletzung,
    // die man sonst erst Jahre später bemerkt.
    private const int MaxEntriesPerSite = 50;

    private readonly AppPaths _paths = paths;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<HistoryEntry>> LoadAsync(string address, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        List<StoredHistoryEntry> stored = await JsonFile
            .ReadAsync(FilePathFor(address), StorageJsonContext.Default.ListStoredHistoryEntry, [], cancellationToken)
            .ConfigureAwait(false);

        return stored
            .OrderByDescending(entry => entry.RanAt)
            .Select(entry => entry.ToModel())
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task AppendAsync(string address, HistoryEntry entry, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentNullException.ThrowIfNull(entry);

        string path = FilePathFor(address);
        List<StoredHistoryEntry> stored = await JsonFile
            .ReadAsync(path, StorageJsonContext.Default.ListStoredHistoryEntry, [], cancellationToken)
            .ConfigureAwait(false);

        stored.Add(StoredHistoryEntry.From(entry));

        // Nur die jüngsten Läufe behalten, am Zeitpunkt gemessen.
        List<StoredHistoryEntry> trimmed = stored
            .OrderByDescending(item => item.RanAt)
            .Take(MaxEntriesPerSite)
            .ToList();

        await JsonFile.WriteAsync(path, trimmed, StorageJsonContext.Default.ListStoredHistoryEntry, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task ClearAsync(string address, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        string path = FilePathFor(address);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string FilePathFor(string address)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(address));
        string name = Convert.ToHexStringLower(hash)[..16];
        return Path.Combine(_paths.HistoryDirectory, name + ".json");
    }
}
