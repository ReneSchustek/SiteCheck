using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SiteCheck.App.Services;

/// <summary>
/// Setzt Hinweise und Bestätigungen als <see cref="ContentDialog"/> um. Der Dialog braucht die
/// Zeichenwurzel des Hauptfensters; die liefert <see cref="App.MainWindowRoot"/>.
/// </summary>
public sealed class ConfirmationService : IConfirmationService
{
    /// <inheritdoc/>
    public async Task ShowScopeNoticeAsync(CancellationToken cancellationToken)
    {
        XamlRoot? root = App.MainWindowRoot;
        if (root is null)
        {
            return;
        }

        ContentDialog dialog = new()
        {
            XamlRoot = root,
            Title = "Nur eigene Seiten, nur passiv",
            CloseButtonText = "Verstanden",
            DefaultButton = ContentDialogButton.Close,
            Content = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = "SiteCheck prüft Seiten passiv von außen: gewöhnliche Anfragen und DNS-Abfragen, "
                    + "nichts wird angegriffen. Benutze es nur für Seiten, die dir gehören oder für die du "
                    + "die ausdrückliche Erlaubnis hast. Viele Anbieter werten schon viele schnelle Anfragen "
                    + "als Angriff und sperren die Adresse vorübergehend.",
            },
        };

        _ = await dialog.ShowAsync();
    }

    /// <inheritdoc/>
    public async Task<bool> ConfirmStressTestOwnershipAsync(string address, CancellationToken cancellationToken)
    {
        XamlRoot? root = App.MainWindowRoot;
        if (root is null)
        {
            return false;
        }

        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = $"Der Lasttest schickt für kurze Zeit viele gleichzeitige Anfragen an {address}. "
                + "Das ist eine aktive Belastung — anders als die übrigen Prüfungen.",
        });
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = "Bestätige nur, wenn diese Seite dir gehört und du sie selbst hostest. Einen fremden "
                + "oder gemieteten Server so zu belasten, kann als Angriff gelten.",
        });

        ContentDialog dialog = new()
        {
            XamlRoot = root,
            Title = "Gehört dir diese Seite?",
            PrimaryButtonText = "Ja, es ist meine Seite",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Close,
            Content = panel,
        };

        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}
