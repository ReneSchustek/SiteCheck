using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;

namespace SiteCheck.Data.Storage;

/// <summary>Speichert die Einstellungen als JSON-Datei im Anwendungsdatenordner.</summary>
public sealed class SettingsStore(AppPaths paths) : ISettingsStore
{
    private readonly AppPaths _paths = paths;

    /// <inheritdoc/>
    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        StoredSettings stored = await JsonFile
            .ReadAsync(_paths.SettingsFile, StorageJsonContext.Default.StoredSettings, StoredSettings.From(AppSettings.Default), cancellationToken)
            .ConfigureAwait(false);
        return stored.ToModel();
    }

    /// <inheritdoc/>
    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return JsonFile.WriteAsync(_paths.SettingsFile, StoredSettings.From(settings), StorageJsonContext.Default.StoredSettings, cancellationToken);
    }
}
