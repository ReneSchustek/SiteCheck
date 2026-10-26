using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace SiteCheck.Data.Probes;

/// <summary>
/// Erzeugt die HTTP-Clients, die die Sonden brauchen, einheitlich konfiguriert: ein
/// browser-naher Kennzeichner, keine automatisch mitgeführten Cookies (damit die erste
/// <c>Set-Cookie</c>-Zeile sichtbar bleibt) und ein Timeout gegen hängende Verbindungen.
/// </summary>
public static class ProbeHttpClients
{
    private const string UserAgent = "Mozilla/5.0 (SiteCheck; eigene Seite)";

    // 10 s reichen einer gesunden Seite mit Abstand; ein langsamer oder drosselnder Host soll den
    // Lauf nicht lange aufhalten. Mit rund zwei Dutzend Anfragen je Lauf summieren sich längere
    // Timeouts spürbar. Antwortet eine echte Seite ausnahmsweise langsamer, zählt sie
    // als „nicht erreichbar“ und man prüft erneut.
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Erzeugt einen Client, der Umleitungen folgt.</summary>
    public static HttpClient CreateFollowing() => Create(allowRedirect: true, DefaultTimeout);

    /// <summary>Erzeugt einen Client, der Umleitungen <b>nicht</b> folgt.</summary>
    public static HttpClient CreateNonFollowing() => Create(allowRedirect: false, DefaultTimeout);

    /// <summary>
    /// Erzeugt den Client für den Lasttest. Folgt Umleitungen und bekommt ein knapperes
    /// Timeout, weil eine unter Last hängende Anfrage schnell als Fehlschlag zählen soll.
    /// </summary>
    public static HttpClient CreateForLoad() => Create(allowRedirect: true, TimeSpan.FromSeconds(10));

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "Der Handler wird über disposeHandler: true an den HttpClient übergeben und mit ihm freigegeben; der Analyzer erkennt diese Eigentumsübergabe nicht. Wirft der Konstruktor doch, gibt der catch-Zweig den Handler frei.")]
    private static HttpClient Create(bool allowRedirect, TimeSpan timeout)
    {
        HttpClientHandler handler = new()
        {
            AllowAutoRedirect = allowRedirect,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.All,
            // Gesperrte Zertifikate sollen auffallen; ohne diese Prüfung übergeht der Client sie stillschweigend.
            CheckCertificateRevocationList = true,
        };

        try
        {
            // disposeHandler: true übergibt die Freigabe des Handlers an den Client. Der try/catch
            // schließt die Lücke, falls der Konstruktor doch einmal wirft, bevor die Übergabe greift.
            HttpClient client = new(handler, disposeHandler: true) { Timeout = timeout };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("de-DE");
            return client;
        }
        catch
        {
            handler.Dispose();
            throw;
        }
    }
}
