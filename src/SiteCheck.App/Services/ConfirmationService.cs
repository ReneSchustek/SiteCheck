using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SiteCheck.App.Services;

/// <summary>
/// Setzt Hinweise und Bestätigungen als <see cref="ContentDialog"/> um. Der Dialog braucht die
/// Zeichenwurzel des Hauptfensters; die liefert <see cref="App.MainWindowRoot"/>.
/// </summary>
public sealed class ConfirmationService(IStringResources strings) : IConfirmationService
{
    private readonly IStringResources _strings = strings;

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
            Title = _strings.GetString("Dialog_ScopeNotice_Title"),
            CloseButtonText = _strings.GetString("Dialog_ScopeNotice_Close"),
            DefaultButton = ContentDialogButton.Close,
            Content = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = _strings.GetString("Dialog_ScopeNotice_Text"),
            },
        };

        _ = await dialog.ShowAsync();
    }

    /// <inheritdoc/>
    public async Task<bool> ConfirmStressTestOwnershipAsync(string address, CancellationToken cancellationToken)
    {
        return await ConfirmAsync(
            _strings.GetString("Dialog_StressOwner_Title"),
            _strings.GetString("Dialog_StressOwner_Primary"),
            [
                _strings.GetFormatted("Dialog_StressOwner_Load", address),
                _strings.GetString("Dialog_StressOwner_Only"),
                _strings.GetString("Dialog_StressOwner_Lockout"),
            ]).ConfigureAwait(true);
    }

    /// <inheritdoc/>
    public Task<bool> ConfirmHostNoticeAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken) =>
        ConfirmAsync(
            _strings.GetString("Dialog_HostNotice_Title"),
            _strings.GetString("Dialog_HostNotice_Primary"),
            [
                _strings.GetFormatted("Dialog_HostNotice_First", string.Join(", ", addresses)),
                _strings.GetString("Dialog_HostNotice_Risk"),
                _strings.GetString("Dialog_HostNotice_Advice"),
            ]);

    /// <inheritdoc/>
    public Task<bool> ConfirmSensitivePathsAsync(string address, CancellationToken cancellationToken) =>
        ConfirmAsync(
            _strings.GetString("Dialog_SensitivePaths_Title"),
            _strings.GetString("Dialog_SensitivePaths_Primary"),
            [
                _strings.GetFormatted("Dialog_SensitivePaths_Scope", address),
                _strings.GetString("Dialog_SensitivePaths_Risk"),
            ]);

    /// <inheritdoc/>
    public Task<bool> ConfirmRetryDuringHaltAsync(string address, string reason, CancellationToken cancellationToken) =>
        ConfirmAsync(
            _strings.GetString("Dialog_Retry_Title"),
            _strings.GetString("Dialog_Retry_Primary"),
            [reason, _strings.GetFormatted("Dialog_Retry_Risk", address)]);

    private async Task<bool> ConfirmAsync(string title, string primaryText, string[] paragraphs)
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
            CloseButtonText = _strings.GetString("Action_Cancel"),
            DefaultButton = ContentDialogButton.Close,
            Content = panel,
        };

        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}
