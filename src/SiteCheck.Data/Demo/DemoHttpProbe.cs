using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models.Probes;

namespace SiteCheck.Data.Demo;

/// <summary>
/// HTTP-Antworten der Vorführwelt aus einer festen Tabelle. Kein Socket, kein Name wird aufgelöst.
/// Die Antworten sind so gewählt, dass die echten Prüfungen eine gemischte, glaubhafte Lage
/// ergeben; unbekannte Hosts bleiben ohne Antwort wie eine Seite, die es nicht gibt.
/// </summary>
/// <param name="era">Welcher Stand der Vorführseiten gilt.</param>
public sealed class DemoHttpProbe(DemoEra era) : IHttpProbe
{
    private const string Html = "<!DOCTYPE html><html lang=\"de\"><head><meta charset=\"utf-8\"><title>Startseite</title></head><body></body></html>";

    private const string DirectoryIndex = "<html><head><title>Index of /uploads</title></head><body><h1>Index of /uploads</h1></body></html>";

    private const string EnvFile = "APP_ENV=production\nAPP_DEBUG=false\n";

    private const string MtaStsPolicy = "version: STSv1\nmode: enforce\nmx: mail.blog.example\nmax_age: 604800\n";

    private readonly DemoEra _era = era;

    /// <inheritdoc/>
    public Task<HttpProbeResponse?> FetchAsync(Uri address, CancellationToken cancellationToken) =>
        Task.FromResult(Respond(address, followRedirects: true));

    /// <inheritdoc/>
    public Task<HttpProbeResponse?> FetchWithoutRedirectAsync(Uri address, CancellationToken cancellationToken) =>
        Task.FromResult(Respond(address, followRedirects: false));

    private HttpProbeResponse? Respond(Uri address, bool followRedirects)
    {
        ArgumentNullException.ThrowIfNull(address);
        string? domain = DemoWorld.DomainOf(address.Host);
        if (domain is null)
        {
            return null;
        }

        if (address.Scheme == Uri.UriSchemeHttp && !followRedirects)
        {
            return Response(301, new Uri("https://" + address.Host + address.AbsolutePath));
        }

        return address.AbsolutePath switch
        {
            "/" => HomePage(domain),
            "/.well-known/security.txt" => SecurityTxtFor(domain),
            "/.well-known/mta-sts.txt" when domain == DemoWorld.BlogHost => Response(200, body: MtaStsPolicy),
            "/uploads/" when domain == DemoWorld.MainHost => Response(200, body: DirectoryIndex),

            // Früher lag auf der Hauptseite eine .env offen; heute ist sie weg. Das ergibt im
            // Vergleich einen behobenen kritischen Befund.
            "/.env" when domain == DemoWorld.MainHost && _era == DemoEra.Earlier => Response(200, body: EnvFile),
            _ => Response(404),
        };
    }

    private HttpProbeResponse HomePage(string domain)
    {
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = "text/html; charset=utf-8",
            ["X-Content-Type-Options"] = "nosniff",
            ["X-Frame-Options"] = "SAMEORIGIN",
            ["Referrer-Policy"] = "strict-origin-when-cross-origin",
        };

        string cookie = domain switch
        {
            DemoWorld.MainHost => AddMainHeaders(headers),
            DemoWorld.ShopHost => AddShopHeaders(headers),
            _ => AddCompleteHardening(headers),
        };
        return new HttpProbeResponse(200, null, headers, [cookie], Html);
    }

    // Die Hauptseite hat heute HSTS, früher nicht; CSP und Permissions-Policy fehlen ihr, der Server
    // nennt seine Version, das Sitzungs-Cookie trägt kein Secure.
    private string AddMainHeaders(Dictionary<string, string> headers)
    {
        headers["Server"] = "nginx/1.24.0";
        if (_era == DemoEra.Current)
        {
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }

        return "sitzung=4f2a9c; Path=/; HttpOnly";
    }

    private static string AddShopHeaders(Dictionary<string, string> headers)
    {
        headers["Server"] = "nginx";
        headers["Strict-Transport-Security"] = "max-age=31536000";
        headers["Content-Security-Policy"] = "default-src 'self'";
        return "warenkorb=81d0e3; Path=/; Secure; HttpOnly; SameSite=Lax";
    }

    private static string AddCompleteHardening(Dictionary<string, string> headers)
    {
        headers["Strict-Transport-Security"] = "max-age=63072000; includeSubDomains; preload";
        headers["Content-Security-Policy"] = "default-src 'self'; img-src 'self' data:; frame-ancestors 'none'";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        return "einstellungen=hell; Path=/; Secure; HttpOnly; SameSite=Strict";
    }

    // Der Shop weist heute ab: 429 auf security.txt ist der Anlass für den Halt in der Vorführung.
    private HttpProbeResponse SecurityTxtFor(string domain) => (domain, _era) switch
    {
        (DemoWorld.ShopHost, DemoEra.Current) => Response(429),
        (DemoWorld.ShopHost, DemoEra.Earlier) => Response(404),
        (DemoWorld.MainHost, DemoEra.Earlier) => Response(404),
        _ => Response(200, body: SecurityTxtBody(domain)),
    };

    // Ein festes Ablaufdatum statt "heute + n": security.txt wird ohne Uhr geprüft, und die Bilder
    // sollen bei jedem Lauf dasselbe zeigen.
    private static string SecurityTxtBody(string domain) =>
        $"Contact: mailto:sicherheit@{domain}\nExpires: 2027-12-31T23:00:00.000Z\nPreferred-Languages: de, en\n";

    private static HttpProbeResponse Response(int statusCode, Uri? location = null, string body = "") =>
        new(statusCode, location, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), [], body);
}
