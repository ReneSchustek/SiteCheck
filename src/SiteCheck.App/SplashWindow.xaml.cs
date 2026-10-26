using System.Diagnostics.CodeAnalysis;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;

namespace SiteCheck.App;

/// <summary>
/// Startbildschirm mit Produkt-Logo. Wird beim Start gezeigt und schließt sich, sobald das
/// Hauptfenster aktiviert ist.
/// </summary>
[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "WinUI-3-Window mit x:Class-Bindung muss public sein.")]
public sealed partial class SplashWindow : Window
{
    private const int WindowWidth = 460;
    private const int WindowHeight = 300;

    /// <summary>Erstellt den Startbildschirm, lädt das Logo und zentriert das Fenster.</summary>
    public SplashWindow()
    {
        InitializeComponent();
        LogoImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/logo.png"));
        CenterAndSize();
        RemoveTitleBar();
    }

    private void CenterAndSize()
    {
        AppWindow appWindow = AppWindow;
        appWindow.Resize(new SizeInt32(WindowWidth, WindowHeight));
        DisplayArea area = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Nearest);
        int x = (area.WorkArea.Width - WindowWidth) / 2;
        int y = (area.WorkArea.Height - WindowHeight) / 2;
        appWindow.Move(new PointInt32(x, y));
    }

    private void RemoveTitleBar()
    {
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }
    }
}
