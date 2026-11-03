using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Update;
using SiteCheck.Data.Update;

namespace SiteCheck.App.Services;

/// <summary>
/// Steuert das Selbst-Update: einmal am Tag nachsehen, ein neues Paket im Hintergrund laden und
/// prüfen, es beim nächsten Start einspielen. Gespielt wird nur ein Paket, dessen Signatur stimmt und
/// dessen Programmdatei den vermerkten Hash trägt. Fehler hindern das Programm nie am Starten.
/// <para>
/// Die Abfragen gehen an GitHub und nie an eine geprüfte Seite, die Sperrwache betrifft sie nicht.
/// </para>
/// </summary>
public sealed partial class UpdateOrchestrator(
    UpdateInstallerService installer,
    IUpdateChecker checker,
    ISettingsStore settings,
    TimeProvider clock,
    Version appVersion,
    ILogger<UpdateOrchestrator> logger)
{
    // Öfter bringt nichts: Releases erscheinen selten, und die GitHub-Schnittstelle begrenzt anonyme Abfragen.
    private static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);

    // So lange darf die alte Instanz brauchen, um sich zu beenden, bevor der Helfer trotzdem kopiert.
    private static readonly TimeSpan ProcessExitTimeout = TimeSpan.FromSeconds(30);

    private readonly UpdateInstallerService _installer = installer;
    private readonly IUpdateChecker _checker = checker;
    private readonly ISettingsStore _settings = settings;
    private readonly TimeProvider _clock = clock;
    private readonly Version _appVersion = appVersion;
    private readonly ILogger<UpdateOrchestrator> _logger = logger;

    /// <summary>Meldet, dass eine neuere Version gefunden wurde.</summary>
    public event EventHandler? UpdateAvailable;

    /// <summary>Die gefundene Aktualisierung; leer, solange keine bekannt ist.</summary>
    public UpdateInfo? Pending { get; private set; }

    /// <summary>Sieht nach, falls die Prüfung fällig ist, und lädt ein neues Paket im Hintergrund.</summary>
    /// <param name="cancellationToken">Bricht Prüfung und Download ab.</param>
    /// <returns>Eine Aufgabe, die auch bei Fehlern regulär endet.</returns>
    public async Task CheckAtStartupAsync(CancellationToken cancellationToken)
    {
        AppSettings current = await _settings.LoadAsync(cancellationToken).ConfigureAwait(true);
        DateTimeOffset now = _clock.GetUtcNow();
        if (!current.AutoUpdateEnabled || now - current.LastUpdateCheck < CheckInterval)
        {
            return;
        }

        UpdateInfo info;
        try
        {
            info = await _checker.CheckForUpdateAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (HttpRequestException ex)
        {
            // Ein Start ohne Netz ist Alltag und kein Fehler; nachgesehen wird beim nächsten Mal.
            CheckFailed(_logger, ex);
            return;
        }

        await SaveAsync(current with { LastUpdateCheck = now }, cancellationToken).ConfigureAwait(true);
        if (!info.IsUpdateAvailable)
        {
            return;
        }

        Pending = info;
        UpdateAvailable?.Invoke(this, EventArgs.Empty);

        if (info.PackageUrl is not null && !IsAlreadyStaged(info, current))
        {
            StagedUpdate? staged = await _installer.DownloadAndStageAsync(info, cancellationToken).ConfigureAwait(true);
            if (staged is not null)
            {
                AppSettings latest = await _settings.LoadAsync(cancellationToken).ConfigureAwait(true);
                await SaveAsync(latest with { PendingUpdateVersion = staged.Version, PendingUpdateSha256 = staged.ExecutableSha256 }, cancellationToken).ConfigureAwait(true);
            }
        }
    }

    /// <summary>Sucht auf Wunsch sofort nach einer neuen Version, unabhängig von Einstellung und Takt.</summary>
    /// <param name="cancellationToken">Bricht die Prüfung ab.</param>
    /// <returns>Die gefundene Aktualisierung oder <see langword="null"/>, wenn nichts Neues da ist oder das Netz fehlt.</returns>
    public async Task<UpdateInfo?> CheckNowAsync(CancellationToken cancellationToken)
    {
        try
        {
            UpdateInfo info = await _checker.CheckForUpdateAsync(cancellationToken).ConfigureAwait(true);
            Pending = info.IsUpdateAvailable ? info : null;
            return Pending;
        }
        catch (HttpRequestException ex)
        {
            CheckFailed(_logger, ex);
            return null;
        }
    }

    /// <summary>Lädt das gefundene Update, prüft es und startet das Einspielen.</summary>
    /// <param name="cancellationToken">Bricht den Download ab.</param>
    /// <returns>
    /// <see langword="true"/>, wenn der Helfer läuft und das Programm sich beenden soll. Sonst
    /// <see langword="false"/>, etwa weil der Programmordner schreibgeschützt ist oder die Signatur nicht stimmt.
    /// </returns>
    public async Task<bool> InstallNowAsync(CancellationToken cancellationToken)
    {
        string target = AppContext.BaseDirectory;
        if (Pending?.PackageUrl is null || !UpdateInstallerService.IsDirectoryWritable(target))
        {
            return false;
        }

        try
        {
            StagedUpdate? staged = await _installer.DownloadAndStageAsync(Pending, cancellationToken).ConfigureAwait(true);
            if (staged is null)
            {
                return false;
            }

            // Der Helfer prüft den Hash noch einmal; er vertraut seinen Startargumenten nicht.
            AppSettings latest = await _settings.LoadAsync(cancellationToken).ConfigureAwait(true);
            await SaveAsync(latest with { PendingUpdateVersion = staged.Version, PendingUpdateSha256 = staged.ExecutableSha256 }, cancellationToken).ConfigureAwait(true);

            UpdateProcess.StartApply(
                UpdateInstallerService.ExecutablePathIn(staged.Directory),
                new UpdateApplyArgs(Environment.ProcessId, staged.Directory, target));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
        {
            ApplyFailed(_logger, ex);
            return false;
        }
    }

    /// <summary>Spielt beim Start ein vermerktes und geprüftes Update ein.</summary>
    /// <returns><see langword="true"/>, wenn der Helfer läuft und dieser Start enden soll.</returns>
    public async Task<bool> TryApplyPendingUpdateOnStartupAsync()
    {
        try
        {
            string target = AppContext.BaseDirectory;
            AppSettings current = await _settings.LoadAsync(CancellationToken.None).ConfigureAwait(true);
            string? staged = _installer.FindVerifiedPendingUpdateDirectory(_appVersion, current.PendingUpdateVersion, current.PendingUpdateSha256);
            if (staged is null)
            {
                // Entweder erledigt, denn nach dem Einspielen läuft genau diese Version, oder unbrauchbar.
                // Stehen bleiben darf der Vermerk nicht, sonst wird er bei jedem Start erneut gesucht.
                _installer.CleanObsolete(_appVersion);
                if (current.PendingUpdateVersion is not null || current.PendingUpdateSha256 is not null)
                {
                    await SaveAsync(current with { PendingUpdateVersion = null, PendingUpdateSha256 = null }, CancellationToken.None).ConfigureAwait(true);
                }

                return false;
            }

            if (!UpdateInstallerService.IsDirectoryWritable(target))
            {
                // Installation für alle Benutzer unter "Programme": Das Paket bleibt liegen, kein Tausch.
                TargetNotWritable(_logger, target);
                return false;
            }

            UpdateProcess.StartApply(UpdateInstallerService.ExecutablePathIn(staged), new UpdateApplyArgs(Environment.ProcessId, staged, target));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
        {
            ApplyFailed(_logger, ex);
            return false;
        }
    }

    /// <summary>Läuft im Helfer-Prozess aus dem Staging-Ordner: tauscht die Dateien und startet die Installation neu.</summary>
    /// <param name="apply">Die Startargumente des Helfers; ihnen wird nicht vertraut.</param>
    /// <returns>Eine Aufgabe, die mit dem Ende des Prozesses nicht mehr zurückkehrt.</returns>
    public async Task RunHelperApplyAsync(UpdateApplyArgs apply)
    {
        try
        {
            AppSettings current = await _settings.LoadAsync(CancellationToken.None).ConfigureAwait(false);
            if (!_installer.IsTrustedStagingDirectory(apply.Source, current.PendingUpdateSha256)
                || !_installer.IsTrustedTargetDirectory(apply.Target))
            {
                ApplyRejected(_logger, apply.Source);
                return;
            }

            WaitForProcessExit(apply.Pid);
            _installer.ApplyStagedFiles(apply.Source, apply.Target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // ApplyStagedFiles hat auf den vorigen Stand zurückgerollt.
            ApplyFailed(_logger, ex);
        }
        finally
        {
            // Neuer Stand bei Erfolg, zurückgerollter bei Fehler: Lauffähig ist die Installation in beiden Fällen.
            StartInstalledApp(apply.Target);
            Environment.Exit(0);
        }
    }

    private static void WaitForProcessExit(int pid)
    {
        try
        {
            using Process process = Process.GetProcessById(pid);
            _ = process.WaitForExit(ProcessExitTimeout);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // Die alte Instanz hat sich bereits beendet.
        }
    }

    private bool IsAlreadyStaged(UpdateInfo info, AppSettings current) =>
        string.Equals(current.PendingUpdateVersion, info.LatestVersion, StringComparison.OrdinalIgnoreCase)
        && _installer.FindVerifiedPendingUpdateDirectory(_appVersion, current.PendingUpdateVersion, current.PendingUpdateSha256) is not null;

    private async Task SaveAsync(AppSettings updated, CancellationToken cancellationToken)
    {
        try
        {
            await _settings.SaveAsync(updated, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ohne Vermerk wird beim nächsten Start einfach erneut nachgesehen.
            CheckFailed(_logger, ex);
        }
    }

    private void StartInstalledApp(string target)
    {
        try
        {
            UpdateProcess.StartApp(UpdateInstallerService.ExecutablePathIn(target));
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            ApplyFailed(_logger, ex);
        }
    }

    [LoggerMessage(EventId = 1101, Level = LogLevel.Warning, Message = "Update-Prüfung fehlgeschlagen")]
    private static partial void CheckFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Error, Message = "Einspielen des Updates fehlgeschlagen")]
    private static partial void ApplyFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1103, Level = LogLevel.Warning, Message = "Update abgelehnt, Quelle oder Ziel nicht vertrauenswürdig: {Source}")]
    private static partial void ApplyRejected(ILogger logger, string source);

    [LoggerMessage(EventId = 1104, Level = LogLevel.Information, Message = "Programmordner nicht beschreibbar, Update wird nicht eingespielt: {Target}")]
    private static partial void TargetNotWritable(ILogger logger, string target);
}
