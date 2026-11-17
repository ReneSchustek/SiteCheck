using SiteCheck.Core.Models;

namespace SiteCheck.Data.Demo;

/// <summary>Stand der Vorführseiten: ein früherer und der heutige, damit der Vergleich etwas zeigt.</summary>
public enum DemoEra
{
    /// <summary>Vor einigen Tagen: HSTS und DMARC fehlen auf der Hauptseite noch.</summary>
    Earlier = 0,

    /// <summary>Heute: die Hauptseite ist nachgebessert, der Shop weist Anfragen ab.</summary>
    Current = 1,
}

/// <summary>
/// Die erfundenen Seiten des Vorführmodus. Alle Namen liegen unter der reservierten Endung
/// <c>.example</c> (RFC 6761), alle Adressen im Dokumentationsnetz 192.0.2.0/24 (RFC 5737); keine
/// davon gehört jemandem, keine ist erreichbar.
/// </summary>
public static class DemoWorld
{
    /// <summary>Gemischte Befunde, mit früherem Stand zum Vergleich.</summary>
    public const string MainHost = "meine-seite.example";

    /// <summary>Weist heute die Abfrage von security.txt mit 429 ab und löst damit den Halt aus.</summary>
    public const string ShopHost = "shop.example";

    /// <summary>Gut eingerichtet, durchgehend grün.</summary>
    public const string BlogHost = "blog.example";

    /// <summary>Die Einträge unter „Meine Seiten“.</summary>
    public static IReadOnlyList<Site> Sites { get; } =
    [
        new("Meine Seite", "https://" + MainHost + "/"),
        new("Shop", "https://" + ShopHost + "/"),
        new("Blog", "https://" + BlogHost + "/"),
    ];

    /// <summary>Ob ein Name zur Vorführwelt gehört; alles andere bleibt ohne Antwort.</summary>
    /// <param name="host">Hostname, auch mit Subdomain.</param>
    /// <returns><see langword="true"/>, wenn der Name unter einer der drei Domains liegt.</returns>
    public static bool IsKnown(string host) => DomainOf(host) is not null;

    /// <summary>Die Vorführdomain, zu der ein Name gehört, oder <see langword="null"/>.</summary>
    /// <param name="host">Hostname, auch mit Subdomain.</param>
    /// <returns>Eine der drei Domains oder <see langword="null"/>.</returns>
    public static string? DomainOf(string host)
    {
        ArgumentNullException.ThrowIfNull(host);
        string name = host.TrimEnd('.');
        foreach (string domain in (string[])[MainHost, ShopHost, BlogHost])
        {
            if (name.Equals(domain, StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase))
            {
                return domain;
            }
        }

        return null;
    }

    /// <summary>Die Dokumentationsadresse einer Vorführdomain.</summary>
    /// <param name="domain">Eine der drei Domains.</param>
    /// <returns>Eine Adresse aus 192.0.2.0/24.</returns>
    public static string AddressOf(string domain) => domain switch
    {
        MainHost => "192.0.2.10",
        ShopHost => "192.0.2.20",
        _ => "192.0.2.30",
    };
}
