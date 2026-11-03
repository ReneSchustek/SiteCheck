using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SiteCheck.App.Services;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Update;

namespace SiteCheck.App.ViewModels;

/// <summary>Bedienung des Selbst-Updates auf der Über-Seite: Schalter, Suche, Einspielen.</summary>
public sealed partial class UpdateViewModel(UpdateOrchestrator orchestrator, ISettingsStore settings, IStringResources strings)
    : ObservableObject
{
    private readonly UpdateOrchestrator _orchestrator = orchestrator;
    private readonly ISettingsStore _settings = settings;
    private readonly IStringResources _strings = strings;

    /// <summary>Meldet, dass der Helfer läuft und das Programm sich beenden soll.</summary>
    public event EventHandler? ExitRequested;

    /// <summary>Ob einmal am Tag nach Updates gesucht wird.</summary>
    [ObservableProperty]
    public partial bool AutoUpdateEnabled { get; set; } = true;

    /// <summary>Was gerade Stand ist.</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = string.Empty;

    /// <summary>Ob eine neue Version bereitsteht.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    public partial bool HasUpdate { get; private set; }

    /// <summary>Liest die Einstellung und den bekannten Stand ein.</summary>
    /// <param name="cancellationToken">Bricht das Lesen ab.</param>
    /// <returns>Eine Aufgabe.</returns>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        AppSettings current = await _settings.LoadAsync(cancellationToken).ConfigureAwait(true);
        _loading = true;
        AutoUpdateEnabled = current.AutoUpdateEnabled;
        _loading = false;
        Show(_orchestrator.Pending);
    }

    private bool _loading;

    partial void OnAutoUpdateEnabledChanged(bool value)
    {
        if (!_loading)
        {
            _ = SaveChoiceAsync(value);
        }
    }

    private async Task SaveChoiceAsync(bool enabled)
    {
        try
        {
            AppSettings current = await _settings.LoadAsync(CancellationToken.None).ConfigureAwait(true);
            await _settings.SaveAsync(current with { AutoUpdateEnabled = enabled }, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText = _strings.GetFormatted("Status_Update_SaveFailed", ex.Message);
        }
    }

    /// <summary>Sucht jetzt nach einer neuen Version.</summary>
    [RelayCommand]
    private async Task CheckAsync()
    {
        StatusText = _strings.GetString("Status_Update_Checking");
        UpdateInfo? info = await _orchestrator.CheckNowAsync(CancellationToken.None).ConfigureAwait(true);
        if (info is null)
        {
            HasUpdate = false;
            StatusText = _strings.GetString("Status_Update_None");
            return;
        }

        Show(info);
    }

    /// <summary>Lädt, prüft und spielt die gefundene Version ein.</summary>
    [RelayCommand(CanExecute = nameof(HasUpdate))]
    private async Task InstallAsync()
    {
        StatusText = _strings.GetString("Status_Update_Installing");
        if (await _orchestrator.InstallNowAsync(CancellationToken.None).ConfigureAwait(true))
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Ohne gültige Signatur oder im schreibgeschützten Programmordner bleibt der Weg über die Release-Seite.
        StatusText = _strings.GetString("Status_Update_InstallFailed");
        if (_orchestrator.Pending?.DownloadUrl is { } page)
        {
            _ = SafeUrlLauncher.TryOpenInBrowser(page.ToString());
        }
    }

    private void Show(UpdateInfo? info)
    {
        HasUpdate = info is not null;
        if (info is not null)
        {
            StatusText = _strings.GetFormatted("Status_Update_Available", info.LatestVersion);
        }
    }
}
