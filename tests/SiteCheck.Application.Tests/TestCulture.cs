using System.Globalization;
using System.Runtime.CompilerServices;

namespace SiteCheck.Application.Tests;

/// <summary>Feste Kultur für den Testlauf; der GitHub-Läufer ist englisch, die Tests prüfen deutsche Texte.</summary>
internal static class TestCulture
{
    [ModuleInitializer]
    public static void Initialize()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
    }
}
