namespace SiteCheck.App.Services;

/// <summary>
/// Fest verdrahtetes, freiwilliges Spendenziel für den „Kaffee spendieren“-Einstieg. Die
/// Adresse ist eine Compile-Zeit-Konstante und wird niemals aus Konfiguration, Datei oder Netz
/// gelesen, damit es zur Laufzeit keinen austauschbaren Wert gibt.
/// </summary>
internal static class SupportDonation
{
    private const string Placeholder = "PLATZHALTER";

    /// <summary>Offizielle PayPal-Spendenadresse (HTTPS, ohne Laufzeit-Parameter).</summary>
    public const string PayPalUrl = "https://paypal.me/rschustek";

    /// <summary>Ob ein echter Handle hinterlegt ist; sonst bleibt der Einstieg ausgeblendet.</summary>
    public static bool IsConfigured => !PayPalUrl.Contains(Placeholder, StringComparison.Ordinal);
}
