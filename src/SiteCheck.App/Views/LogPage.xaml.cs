using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SiteCheck.App.Services;
using SiteCheck.App.ViewModels;
using SiteCheck.Core.Models;

namespace SiteCheck.App.Views;

/// <summary>Das Protokoll in der App.</summary>
[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "WinUI-3-Seite mit x:Class-Bindung muss public sein.")]
public sealed partial class LogPage : Page
{
    /// <summary>Erstellt die Seite und bezieht ihr ViewModel aus dem Container.</summary>
    public LogPage()
    {
        ViewModel = App.Services.GetRequiredService<LogViewModel>();
        InitializeComponent();
    }

    /// <summary>Das ViewModel der Seite.</summary>
    public LogViewModel ViewModel { get; }

    /// <inheritdoc/>
    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        try
        {
            await ViewModel.ReloadCommand.ExecuteAsync(null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // Eine Ausnahme aus einem async-void-Handler beendet sonst die App; die Liste bleibt dann leer.
            App.Services.GetRequiredService<ILogger<LogPage>>().LogReadFailed(ex);
        }
    }

    // Ein angeklickter Filter bleibt gedrückt: Der Zustand kommt aus dem ViewModel, nicht vom Klick.
    private void OnFilterAll(object sender, RoutedEventArgs e) => SetFilter(sender, LogEntryLevel.Information);

    private void OnFilterWarnings(object sender, RoutedEventArgs e) => SetFilter(sender, LogEntryLevel.Warning);

    private void OnFilterErrors(object sender, RoutedEventArgs e) => SetFilter(sender, LogEntryLevel.Error);

    private void SetFilter(object sender, LogEntryLevel level)
    {
        ViewModel.FilterCommand.Execute(level);
        if (sender is Microsoft.UI.Xaml.Controls.Primitives.ToggleButton button)
        {
            button.IsChecked = true;
        }
    }
}
