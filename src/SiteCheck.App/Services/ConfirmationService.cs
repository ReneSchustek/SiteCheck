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
                Text = "SiteCheck prüft Seiten von außen: gewöhnliche Anfragen und DNS-Abfragen, langsam und "
                    + "mit Halt bei der ersten Abweisung. Benutze es nur für Seiten, die dir gehören oder für "
                    + "die du die ausdrückliche Erlaubnis hast. Viele Anbieter werten Prüfungen trotzdem als "
                    + "Angriff und sperren deine Adresse vorübergehend.",
            },
        };

        _ = await dialog.ShowAsync();
    }

    /// <inheritdoc/>
    public async Task<bool> ConfirmStressTestOwnershipAsync(string address, CancellationToken cancellationToken)
    {
        return await ConfirmAsync(
            "Gehört dir diese Seite?",
            "Ja, es ist meine Seite",
            [
                $"Der Lasttest schickt für kurze Zeit viele gleichzeitige Anfragen an {address}. "
                + "Das ist eine aktive Belastung, anders als die übrigen Prüfungen.",
                "Bestätige nur, wenn diese Seite dir gehört und du sie selbst hostest. Einen fremden "
                + "oder gemieteten Server so zu belasten, kann als Angriff gelten.",
                "Ein Hoster sperrt dafür deinen ganzen Anschluss, unter Umständen tagelang. Dann erreichst "
                + "du weder die Website noch die Postfächer auf diesem Server. Der Test endet bei der ersten "
                + "Abweisung, danach ruht die Seite eine Viertelstunde.",
            ]).ConfigureAwait(true);
    }

    /// <inheritdoc/>
    public Task<bool> ConfirmHostNoticeAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken) =>
        ConfirmAsync(
            "Kann dein Anschluss gesperrt werden?",
            "Verstanden, prüfen",
            [
                "Erste Prüfung von " + string.Join(", ", addresses) + ".",
                "Ein Hoster kann auch langsame Prüfungen als Angriff werten und deinen Anschluss sperren. Dann "
                + "erreichst du weder die Website noch die Postfächer auf diesem Server, unter Umständen tagelang.",
                "SiteCheck fragt deshalb langsam ab und hält bei der ersten Abweisung an. Ganz ausschließen lässt "
                + "sich eine Sperre von außen nicht. Sicherer ist es, die eigene Adresse beim Hoster freizuschalten, "
                + "falls er das anbietet.",
            ]);

    /// <inheritdoc/>
    public Task<bool> ConfirmSensitivePathsAsync(string address, CancellationToken cancellationToken) =>
        ConfirmAsync(
            "Heikle Dateien wirklich abfragen?",
            "Ja, für diesen Lauf",
            [
                $"Dieser Lauf fragt bei {address} Pfade wie .git, .env, config.php und /backup/ ab.",
                "Genau danach suchen Angreifer, und die Abwehr vieler Hoster sperrt dafür den Anschluss schon "
                + "nach wenigen Anfragen. Das ist das größte Risiko dieser Prüfung.",
            ]);

    /// <inheritdoc/>
    public Task<bool> ConfirmRetryDuringHaltAsync(string address, string reason, CancellationToken cancellationToken) =>
        ConfirmAsync(
            "Trotzdem erneut prüfen?",
            "Ja, erneut prüfen",
            [
                reason,
                $"Ist dein Anschluss gesperrt, verlängert jede weitere Anfrage an {address} die Sperre. "
                + "Prüfe zuerst von einem anderen Anschluss, ob die Seite läuft.",
            ]);

    private static async Task<bool> ConfirmAsync(string title, string primaryText, string[] paragraphs)
    {
        XamlRoot? root = App.MainWindowRoot;
        if (root is null)
        {
            // Ohne Fenster gibt es keine Bestätigung, also auch keine Anfrage.
            return false;
        }

        StackPanel panel = new() { Spacing = 8 };
        foreach (string paragraph in paragraphs)
        {
            panel.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap, Text = paragraph });
        }

        ContentDialog dialog = new()
        {
            XamlRoot = root,
            Title = title,
            PrimaryButtonText = primaryText,
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Close,
            Content = panel,
        };

        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}
