using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SiteCheck.App.ViewModels;
using SiteCheck.App.Views;

namespace SiteCheck.App;

/// <summary>
/// Hauptfenster mit der Navigationsleiste und dem Inhaltsbereich. Hält den Code-Behind auf
/// reine Navigation und Verdrahtung beschränkt; die Fachlogik liegt in den ViewModels.
/// </summary>
[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "WinUI-3-Window mit x:Class-Bindung muss public sein.")]
public sealed partial class MainWindow : Window
{
    /// <summary>Erstellt das Hauptfenster, setzt Größe und Symbol und verbindet die Seitenliste.</summary>
    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1100, 760));
        Title = "SiteCheck";

        // Icon für Taskleiste und Titelleiste. Absoluter Pfad ab dem App-Verzeichnis, damit es
        // auch greift, wenn die App nicht aus ihrem Ordner heraus gestartet wird.
        AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));

        // Aus der Seitenliste heraus eine Prüfung anstoßen: zur Prüf-Ansicht wechseln, Adresse
        // setzen und loslaufen. Einmal verbunden, solange das Fenster lebt.
        SitesViewModel sites = App.Services.GetRequiredService<SitesViewModel>();
        sites.CheckRequested += OnCheckRequested;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _ = ContentFrame.Navigate(typeof(CheckPage));
        try
        {
            await App.ShowFirstRunNoticeIfNeededAsync();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // Eine Ausnahme aus einem async-void-Handler bleibt unbeobachtet und beendet die App.
            // Der Erststart-Hinweis ist eine Beigabe (gesperrte Einstellungsdatei, bereits offener
            // Dialog): scheitert er, läuft die App trotzdem weiter.
            System.Diagnostics.Debug.WriteLine("Erststart-Hinweis fehlgeschlagen: " + ex);
        }
    }

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item)
        {
            return;
        }

        Type target = item.Tag switch
        {
            "Check" => typeof(CheckPage),
            "Sites" => typeof(SitesPage),
            "Stress" => typeof(StressTestPage),
            "About" => typeof(AboutPage),
            _ => typeof(CheckPage),
        };

        if (ContentFrame.CurrentSourcePageType != target)
        {
            _ = ContentFrame.Navigate(target);
        }
    }

    private void OnCheckRequested(object? sender, CheckRequestedEventArgs e)
    {
        CheckViewModel check = App.Services.GetRequiredService<CheckViewModel>();
        check.AddressInput = e.Address;

        SelectMenuItem("Check");
        if (ContentFrame.CurrentSourcePageType != typeof(CheckPage))
        {
            _ = ContentFrame.Navigate(typeof(CheckPage));
        }

        if (check.RunCommand.CanExecute(null))
        {
            check.RunCommand.Execute(null);
        }
    }

    private void SelectMenuItem(string tag)
    {
        foreach (object entry in NavView.MenuItems)
        {
            if (entry is NavigationViewItem item && (item.Tag as string) == tag)
            {
                NavView.SelectedItem = item;
                return;
            }
        }
    }
}
