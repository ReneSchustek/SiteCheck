using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Probes;

namespace SiteCheck.Core.Checks;

/// <summary>
/// Leitet die unverschlüsselte Adresse fest auf https um? Gefragt wird die http-Fassung ohne
/// Folgen der Umleitung, damit die erste Antwort sichtbar bleibt.
/// </summary>
public sealed class RedirectCheck(IHttpProbe http) : ICheck
{
    /// <inheritdoc/>
    public string Id => "web.redirect";

    /// <inheritdoc/>
    public CheckCategory Category => CheckCategory.Web;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Finding>> RunAsync(CheckTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        // Dieselbe Seite über http ansprechen (Port 80 ergibt sich aus dem Schema).
        Uri httpAddress = new UriBuilder(target.Address) { Scheme = Uri.UriSchemeHttp, Port = -1 }.Uri;
        HttpProbeResponse? response = await http.FetchWithoutRedirectAsync(httpAddress, cancellationToken).ConfigureAwait(false);

        if (response is null)
        {
            return
            [
                new Finding(
                    Id + ".unreachable",
                    Category,
                    Severity.Info,
                    "http-Adresse nicht erreichbar",
                    "Die Seite antwortet nur über https. Das ist in Ordnung, nur ließ sich die Umleitung nicht nachstellen."),
            ];
        }

        bool isRedirect = response.StatusCode is >= 300 and < 400;
        bool toHttps = response.Location is not null
            && response.Location.IsAbsoluteUri
            && response.Location.Scheme == Uri.UriSchemeHttps;

        if (isRedirect && toHttps)
        {
            // 301 ist die feste Umleitung; eine 302/307 leitet zwar auch um, sagt dem Browser
            // aber nicht, dass es dauerhaft so bleibt, und wird beim nächsten Mal erneut unverschlüsselt angefragt.
            bool isPermanent = response.StatusCode is 301 or 308;
            return
            [
                new Finding(
                    Id,
                    Category,
                    isPermanent ? Severity.Ok : Severity.Info,
                    isPermanent
                        ? $"http leitet fest auf https um ({response.StatusCode})"
                        : $"http leitet auf https um, aber nicht dauerhaft ({response.StatusCode})",
                    isPermanent
                        ? null
                        : "Eine 301 (oder 308) sagt dem Browser, dass es dauerhaft https ist; er merkt es sich.",
                    isPermanent
                        ? null
                        : new Recommendation("Die Umleitung auf eine feste (301) umstellen.")),
            ];
        }

        return
        [
            new Finding(
                Id + ".missing",
                Category,
                Severity.Warning,
                "http leitet nicht sauber auf https um",
                $"Die http-Adresse antwortet mit {response.StatusCode}. Eine feste Umleitung (301) auf https fehlt.",
                new Recommendation(
                    "Alle http-Aufrufe serverseitig fest auf https umleiten.",
                    "RewriteEngine On\nRewriteCond %{HTTPS} off\nRewriteRule ^ https://%{HTTP_HOST}%{REQUEST_URI} [R=301,L]")),
        ];
    }
}
