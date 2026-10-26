using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using SiteCheck.App.ViewModels;

namespace SiteCheck.App.Views;

/// <summary>Die Lasttest-Ansicht.</summary>
[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "WinUI-3-Seite mit x:Class-Bindung muss public sein.")]
public sealed partial class StressTestPage : Page
{
    /// <summary>Erstellt die Seite und bezieht ihr ViewModel aus dem Container.</summary>
    public StressTestPage()
    {
        ViewModel = App.Services.GetRequiredService<StressTestViewModel>();
        InitializeComponent();
    }

    /// <summary>Das ViewModel der Seite.</summary>
    public StressTestViewModel ViewModel { get; }
}
