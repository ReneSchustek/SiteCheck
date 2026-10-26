using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SiteCheck.App.ViewModels;
using Windows.System;

namespace SiteCheck.App.Views;

/// <summary>Die Prüf-Ansicht: Adresse eingeben, prüfen, Ergebnis sehen und weitergeben.</summary>
[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "WinUI-3-Seite mit x:Class-Bindung muss public sein.")]
public sealed partial class CheckPage : Page
{
    /// <summary>Erstellt die Seite und bezieht ihr ViewModel aus dem Container.</summary>
    public CheckPage()
    {
        ViewModel = App.Services.GetRequiredService<CheckViewModel>();
        InitializeComponent();
    }

    /// <summary>Das ViewModel der Seite.</summary>
    public CheckViewModel ViewModel { get; }

    private void OnAddressKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Eingabetaste löst die Prüfung aus, wie der Knopf.
        if (e.Key == VirtualKey.Enter && ViewModel.RunCommand.CanExecute(null))
        {
            ViewModel.RunCommand.Execute(null);
            e.Handled = true;
        }
    }
}
