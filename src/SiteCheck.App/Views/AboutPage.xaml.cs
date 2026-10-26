using System.Diagnostics.CodeAnalysis;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SiteCheck.App.Services;

namespace SiteCheck.App.Views;

/// <summary>Über-Seite: Zweck, Grenze, Version und freiwilliger Spenden-Einstieg.</summary>
[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "WinUI-3-Seite mit x:Class-Bindung muss public sein.")]
public sealed partial class AboutPage : Page
{
    /// <summary>Erstellt die Seite.</summary>
    public AboutPage() => InitializeComponent();

    /// <inheritdoc/>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        Version? version = typeof(App).Assembly.GetName().Version;
        VersionText.Text = version is not null ? $"Version {version.Major}.{version.Minor}.{version.Build}" : "Version 0.1.0";

        // Spenden-Einstieg nur zeigen, wenn ein echter Handle hinterlegt ist.
        SupportPanel.Visibility = SupportDonation.IsConfigured ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSupportClick(object sender, RoutedEventArgs e) =>
        _ = SafeUrlLauncher.TryOpenInBrowser(SupportDonation.PayPalUrl);
}
