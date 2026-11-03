using System.Text.Json;
using System.Text.Json.Serialization;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Guard;

namespace SiteCheck.Data.Storage;

/// <summary>
/// Quellcode-generierter Serialisierungskontext für die gespeicherten Daten. Vermeidet die
/// Laufzeit-Reflexion von <see cref="JsonSerializer"/> und damit die zugehörigen Trimming-
/// und AOT-Warnungen; außerdem schneller und ohne versteckte Abhängigkeiten.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(StoredSettings))]
[JsonSerializable(typeof(List<StoredSite>))]
[JsonSerializable(typeof(List<StoredHistoryEntry>))]
[JsonSerializable(typeof(Dictionary<string, StoredHostGuard>))]
internal sealed partial class StorageJsonContext : JsonSerializerContext;

/// <summary>Gespeicherte Fassung der Einstellungen. Eigener Typ, damit das Dateiformat nicht am Domänenmodell klebt.</summary>
internal sealed record StoredSettings(
    int? WindowWidth,
    int? WindowHeight,
    bool ScopeNoticeAcknowledged,
    bool AutoUpdateEnabled = true,
    DateTimeOffset? LastUpdateCheck = null,
    string? PendingUpdateVersion = null,
    string? PendingUpdateSha256 = null)
{
    public static StoredSettings From(AppSettings settings) => new(
        settings.WindowWidth,
        settings.WindowHeight,
        settings.ScopeNoticeAcknowledged,
        settings.AutoUpdateEnabled,
        settings.LastUpdateCheck,
        settings.PendingUpdateVersion,
        settings.PendingUpdateSha256);

    public AppSettings ToModel() => new(
        WindowWidth,
        WindowHeight,
        ScopeNoticeAcknowledged,
        AutoUpdateEnabled,
        LastUpdateCheck,
        PendingUpdateVersion,
        PendingUpdateSha256);
}

/// <summary>Gespeicherter Stand der Sperrwache für einen Host.</summary>
internal sealed record StoredHostGuard(
    List<DateTimeOffset> RecentRequests,
    DateTimeOffset? BlockedUntil,
    RequestHaltReason? BlockReason,
    bool NoticeAcknowledged)
{
    public static StoredHostGuard From(HostGuardEntry entry) =>
        new([.. entry.RecentRequests], entry.BlockedUntil, entry.BlockReason, entry.NoticeAcknowledged);

    public HostGuardEntry ToModel() => new(RecentRequests, BlockedUntil, BlockReason, NoticeAcknowledged);
}

/// <summary>Gespeicherte Fassung einer Seite.</summary>
internal sealed record StoredSite(string Name, string Address)
{
    public static StoredSite From(Site site) => new(site.Name, site.Address);

    public Site ToModel() => new(Name, Address);
}

/// <summary>Gespeicherte Fassung eines Verlaufseintrags samt seiner Befunde.</summary>
internal sealed record StoredHistoryEntry(
    DateTimeOffset RanAt,
    int OkCount,
    int InfoCount,
    int WarningCount,
    int CriticalCount,
    List<StoredFinding> Findings)
{
    public static StoredHistoryEntry From(HistoryEntry entry) => new(
        entry.RanAt,
        entry.Summary.OkCount,
        entry.Summary.InfoCount,
        entry.Summary.WarningCount,
        entry.Summary.CriticalCount,
        entry.Findings.Select(StoredFinding.From).ToList());

    public HistoryEntry ToModel() => new(
        RanAt,
        new FindingSummary(OkCount, InfoCount, WarningCount, CriticalCount),
        Findings.Select(finding => finding.ToModel()).ToList());
}

/// <summary>Gespeicherte Fassung eines Befunds. Flach gehalten, damit die Datei lesbar bleibt.</summary>
internal sealed record StoredFinding(
    string Key,
    CheckCategory Category,
    Severity Severity,
    string Title,
    string? Detail,
    string? Advice,
    string? Example,
    Confidence Confidence = Confidence.Measured)
{
    public static StoredFinding From(Finding finding) => new(
        finding.Key,
        finding.Category,
        finding.Severity,
        finding.Title,
        finding.Detail,
        finding.Recommendation?.Advice,
        finding.Recommendation?.Example,
        finding.Confidence);

    public Finding ToModel() => new(
        Key,
        Category,
        Severity,
        Title,
        Detail,
        Advice is null ? null : new Recommendation(Advice, Example))
    {
        Confidence = Confidence,
    };
}
