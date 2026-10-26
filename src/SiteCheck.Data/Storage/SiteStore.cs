using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;

namespace SiteCheck.Data.Storage;

/// <summary>Speichert die Liste der eigenen Seiten als JSON-Datei. Nur Name und Adresse, keine Geheimnisse.</summary>
public sealed class SiteStore(AppPaths paths) : ISiteStore
{
    private readonly AppPaths _paths = paths;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Site>> LoadAsync(CancellationToken cancellationToken)
    {
        List<StoredSite> stored = await JsonFile
            .ReadAsync(_paths.SitesFile, StorageJsonContext.Default.ListStoredSite, [], cancellationToken)
            .ConfigureAwait(false);
        return stored.Select(site => site.ToModel()).ToArray();
    }

    /// <inheritdoc/>
    public Task SaveAsync(IReadOnlyList<Site> sites, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sites);
        List<StoredSite> stored = sites.Select(StoredSite.From).ToList();
        return JsonFile.WriteAsync(_paths.SitesFile, stored, StorageJsonContext.Default.ListStoredSite, cancellationToken);
    }
}
