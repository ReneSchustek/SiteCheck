using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SiteCheck.App.Services;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;

namespace SiteCheck.App.ViewModels;

/// <summary>Zeigt das Protokoll mit Suche beim Tippen und einem Filter nach Stufe.</summary>
public sealed partial class LogViewModel(ILogReader reader, IStringResources strings) : ObservableObject
{
    private readonly ILogReader _reader = reader;
    private readonly IStringResources _strings = strings;
    private IReadOnlyList<LogEntry> _all = [];

    /// <summary>Die Einträge nach Suche und Filter.</summary>
    public ObservableCollection<LogEntry> Entries { get; } = [];

    /// <summary>Der Suchtext.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>Die niedrigste angezeigte Stufe.</summary>
    [ObservableProperty]
    public partial LogEntryLevel MinimumLevel { get; private set; } = LogEntryLevel.Information;

    /// <summary>Ob alle Stufen gezeigt werden.</summary>
    public bool ShowsAll => MinimumLevel == LogEntryLevel.Information;

    /// <summary>Ob Warnungen und Fehler gezeigt werden.</summary>
    public bool ShowsWarnings => MinimumLevel == LogEntryLevel.Warning;

    /// <summary>Ob nur Fehler gezeigt werden.</summary>
    public bool ShowsErrors => MinimumLevel == LogEntryLevel.Error;

    /// <summary>Ob die Liste leer ist.</summary>
    public bool IsEmpty => Entries.Count == 0;

    /// <summary>Unterscheidet „noch nichts da“ von „nichts gefunden“.</summary>
    public string EmptyText => _strings.GetString(_all.Count == 0 ? "Log_Empty_None" : "Log_Empty_NoMatch");

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnMinimumLevelChanged(LogEntryLevel value)
    {
        OnPropertyChanged(nameof(ShowsAll));
        OnPropertyChanged(nameof(ShowsWarnings));
        OnPropertyChanged(nameof(ShowsErrors));
        ApplyFilter();
    }

    /// <summary>Liest das Protokoll neu ein.</summary>
    [RelayCommand]
    private async Task ReloadAsync()
    {
        _all = await _reader.ReadAsync(CancellationToken.None).ConfigureAwait(true);
        ApplyFilter();
    }

    /// <summary>Setzt den Filter.</summary>
    /// <param name="level">Die niedrigste anzuzeigende Stufe.</param>
    [RelayCommand]
    private void Filter(LogEntryLevel level) => MinimumLevel = level;

    private void ApplyFilter()
    {
        Entries.Clear();
        foreach (LogEntry entry in _all.Where(Matches))
        {
            Entries.Add(entry);
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }

    private bool Matches(LogEntry entry) =>
        entry.Level >= MinimumLevel
        && (string.IsNullOrWhiteSpace(SearchText) || entry.Text.Contains(SearchText.Trim(), StringComparison.CurrentCultureIgnoreCase));
}
