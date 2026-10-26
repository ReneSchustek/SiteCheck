using System.Diagnostics.CodeAnalysis;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace SiteCheck.App.Converters;

/// <summary><see langword="false"/> wird zu <see cref="Visibility.Visible"/>, <see langword="true"/> zu <see cref="Visibility.Collapsed"/>.</summary>
[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "In XAML als StaticResource referenzierte Konverter müssen public sein.")]
public sealed partial class InverseBoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Visibility.Collapsed;
}
