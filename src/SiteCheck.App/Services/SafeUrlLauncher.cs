using System.Diagnostics;

namespace SiteCheck.App.Services;

/// <summary>
/// Öffnet eine Adresse im Standard-Browser, aber ausschließlich mit http/https-Schema. So kann
/// kein über eine andere Quelle untergeschobener Wert (z. B. <c>file://</c> auf eine .exe) ein
/// beliebiges Programm starten.
/// </summary>
internal static class SafeUrlLauncher
{
    /// <summary>Öffnet eine absolute http/https-Adresse im Browser.</summary>
    /// <param name="url">Die zu öffnende Adresse.</param>
    /// <returns><see langword="true"/>, wenn der Browser gestartet wurde.</returns>
    public static bool TryOpenInBrowser(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        _ = Process.Start(new ProcessStartInfo { FileName = uri.AbsoluteUri, UseShellExecute = true });
        return true;
    }
}
