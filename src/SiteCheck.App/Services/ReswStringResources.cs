using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Windows.ApplicationModel.Resources;

namespace SiteCheck.App.Services;

/// <summary>
/// Liest die Texte über den Resource-Loader des Windows App SDK. Der Loader der älteren
/// WinRT-Schnittstelle braucht eine Paket-Identität und stürzt in der portablen Fassung ab.
/// </summary>
public sealed class ReswStringResources : IStringResources
{
    // Erst beim ersten Text anlegen: Ohne resources.pri scheitert schon der Konstruktor, und das
    // Programm soll dann mit sichtbaren Lücken starten statt gar nicht.
    private readonly Lazy<ResourceLoader?> _loader = new(CreateLoader);

    /// <inheritdoc/>
    public string GetString(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            string? value = _loader.Value?.GetString(key);
            return string.IsNullOrEmpty(value) ? $"[?{key}?]" : value;
        }
        catch (COMException)
        {
            return $"[?{key}?]";
        }
    }

    /// <inheritdoc/>
    public string GetFormatted(string key, params object[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, GetString(key), arguments);

    private static ResourceLoader? CreateLoader()
    {
        try
        {
            return new ResourceLoader();
        }
        catch (COMException)
        {
            return null;
        }
    }
}
