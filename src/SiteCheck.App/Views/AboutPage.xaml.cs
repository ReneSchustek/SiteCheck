using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SiteCheck.App.Services;
using SiteCheck.App.ViewModels;

namespace SiteCheck.App.Views;

/// <summary>Über-Seite: Zweck, Grenze, Version und freiwilliger Spenden-Einstieg.</summary>
[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "WinUI-3-Seite mit x:Class-Bindung muss public sein.")]
public sealed partial class AboutPage : Page
{
    /// <summary>Erstellt die Seite.</summary>
    public AboutPage()
    {
        Updates = App.Services.GetRequiredService<UpdateViewModel>();
        InitializeComponent();
    }

    /// <summary>Die Bedienung des Selbst-Updates.</summary>
    public UpdateViewModel Updates { get; }

    /// <inheritdoc/>
    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Updates.ExitRequested -= OnExitRequested;
        Updates.ExitRequested += OnExitRequested;

        Version? version = typeof(App).Assembly.GetName().Version;
        if (version is not null)
        {
            string number = $"{version.Major}.{version.Minor}.{version.Build}";
            VersionText.Text = App.Services.GetRequiredService<IStringResources>().GetFormatted("About_Version", number);
        }

        // Spenden-Einstieg nur zeigen, wenn ein echter Handle hinterlegt ist.
        SupportPanel.Visibility = SupportDonation.IsConfigured ? Visibility.Visible : Visibility.Collapsed;

        try
        {
            await Updates.LoadAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // Eine Ausnahme aus einem async-void-Handler beendet sonst die App; der Schalter zeigt dann die Vorgabe.
            App.Services.GetRequiredService<ILogger<AboutPage>>().UpdateCheckFailed(ex);
        }
    }

    /// <inheritdoc/>
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        Updates.ExitRequested -= OnExitRequested;
        base.OnNavigatedFrom(e);
    }

    private static void OnExitRequested(object? sender, EventArgs e) => Microsoft.UI.Xaml.Application.Current.Exit();

    private void OnSupportClick(object sender, RoutedEventArgs e) =>
        _ = SafeUrlLauncher.TryOpenInBrowser(SupportDonation.PayPalUrl);
}
