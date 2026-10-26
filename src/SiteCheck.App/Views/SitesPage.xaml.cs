using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SiteCheck.App.ViewModels;

namespace SiteCheck.App.Views;

/// <summary>Verwaltung der gespeicherten Seiten.</summary>
[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "WinUI-3-Seite mit x:Class-Bindung muss public sein.")]
public sealed partial class SitesPage : Page
{
    private bool _loaded;

    /// <summary>Erstellt die Seite und bezieht ihr ViewModel aus dem Container.</summary>
    public SitesPage()
    {
        ViewModel = App.Services.GetRequiredService<SitesViewModel>();
        InitializeComponent();
    }

    /// <summary>Das ViewModel der Seite.</summary>
    public SitesViewModel ViewModel { get; }

    /// <inheritdoc/>
    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        // Einmal laden; danach hält das Singleton-ViewModel die Liste über Seitenwechsel hinweg.
        if (!_loaded)
        {
            _loaded = true;
            try
            {
                await ViewModel.LoadAsync(CancellationToken.None);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                // Ausnahme aus einem async-void-Handler bleibt sonst unbeobachtet und beendet die App.
                // Die Liste bleibt dann leer, statt dass das Öffnen der Seite abstürzt.
                System.Diagnostics.Debug.WriteLine("Laden der Seitenliste fehlgeschlagen: " + ex);
            }
        }
    }
}
