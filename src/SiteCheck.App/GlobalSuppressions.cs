using System.Diagnostics.CodeAnalysis;

// CA1515 (öffentliche Typen intern machen) ist für Bibliotheken gedacht. SiteCheck.App ist ein
// WinUI-3-Desktop-EXE, dessen XAML-Databinding, x:Class-Generator und ICommand-Bindings auf
// public Typen in ViewModels, Views, Converters und Diensten aufsetzen. Diese public-Pflicht ist
// framework-bedingt und nicht im eigenen Code auflösbar.
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal",
    Scope = "namespaceanddescendants", Target = "~N:SiteCheck.App.ViewModels",
    Justification = "WinUI-3-XAML-Databinding benötigt public ViewModels.")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal",
    Scope = "namespaceanddescendants", Target = "~N:SiteCheck.App.Views",
    Justification = "WinUI-3-XAML-Code-Behind (x:Class) benötigt public View-Klassen.")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal",
    Scope = "namespaceanddescendants", Target = "~N:SiteCheck.App.Converters",
    Justification = "In XAML als StaticResource referenzierte Konverter müssen public sein.")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal",
    Scope = "namespaceanddescendants", Target = "~N:SiteCheck.App.Services",
    Justification = "Dienste werden von public ViewModels und Views in Signaturen referenziert; inkonsistente Sichtbarkeit verhindert internal.")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal",
    Scope = "namespaceanddescendants", Target = "~N:SiteCheck.App.Composition",
    Justification = "Die Composition Root wird vom App-Einstiegspunkt aufgerufen.")]
