using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models.Guard;

namespace SiteCheck.Data.Storage;

/// <summary>Speichert den Stand der Sperrwache als JSON-Datei im Anwendungsdatenordner.</summary>
public sealed class RequestGuardStore(AppPaths paths) : IRequestGuardStore
{
    private readonly AppPaths _paths = paths;

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, HostGuardEntry>> LoadAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, StoredHostGuard> stored = await JsonFile
            .ReadAsync(_paths.RequestGuardFile, StorageJsonContext.Default.DictionaryStringStoredHostGuard, [], cancellationToken)
            .ConfigureAwait(false);
        return stored.ToDictionary(pair => pair.Key, pair => pair.Value.ToModel(), StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public Task SaveAsync(IReadOnlyDictionary<string, HostGuardEntry> hosts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hosts);
        Dictionary<string, StoredHostGuard> stored = hosts.ToDictionary(
            pair => pair.Key,
            pair => StoredHostGuard.From(pair.Value),
            StringComparer.OrdinalIgnoreCase);
        return JsonFile.WriteAsync(_paths.RequestGuardFile, stored, StorageJsonContext.Default.DictionaryStringStoredHostGuard, cancellationToken);
    }
}
