using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;

namespace SiteCheck.App.ViewModels;

/// <summary>Holt die Warteschlange des aktuellen Fadens für Rückrufe aus dem Hintergrund.</summary>
internal static class UiDispatcher
{
    /// <summary>Die Warteschlange des aktuellen Fadens.</summary>
    /// <returns>
    /// Die Warteschlange; <see langword="null"/> abseits des UI-Fadens oder ohne Windows App Runtime, etwa
    /// auf einem Bau-Läufer. Dort wirft das System, statt leer zu antworten, und die Anzeige lässt sich direkt setzen.
    /// </returns>
    public static DispatcherQueue? TryGetCurrent()
    {
        try
        {
            return DispatcherQueue.GetForCurrentThread();
        }
        catch (COMException)
        {
            return null;
        }
    }
}
