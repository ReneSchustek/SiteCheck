using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Localization;
using SiteCheck.Core.Models.Probes;
using SiteCheck.Core.Models;

namespace SiteCheck.Core.Checks;

/// <summary>
/// Sind die gesetzten Cookies sauber (Secure, HttpOnly, SameSite)? Geprüft wird jede
/// <c>Set-Cookie</c>-Zeile der ersten Antwort einzeln.
/// </summary>
public sealed class CookieCheck(IHttpProbe http) : ICheck
{
    private static readonly string[] RequiredAttributes = ["Secure", "HttpOnly", "SameSite"];

    /// <inheritdoc/>
    public string Id => "web.cookies";

    /// <inheritdoc/>
    public CheckCategory Category => CheckCategory.Web;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Finding>> RunAsync(CheckTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        HttpProbeResponse? response = await http.FetchAsync(target.Address, cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            // Nicht erreichbar heißt nicht „in Ordnung“: Ohne Antwort ist über die Cookies nichts
            // bekannt. Früher gab dieser Zweig fälschlich ein Ok aus, auch wenn die Seite nie antwortete.
            return [new Finding(Id + ".skipped", Category, Severity.Info, DomainText.Get("Cookies nicht geprüft — die Seite war nicht erreichbar"))];
        }

        if (response.SetCookieHeaders.Count == 0)
        {
            // Echte Antwort ohne Cookie ist kein Mangel: Viele Seiten setzen erst nach dem Login eines.
            return [new Finding(Id + ".none", Category, Severity.Ok, DomainText.Get("Die Startseite setzt kein Sitzungs-Cookie"))];
        }

        List<Finding> findings = [];
        foreach (string cookie in response.SetCookieHeaders)
        {
            string name = cookie.Split('=', 2)[0].Trim();
            string[] missing = RequiredAttributes
                .Where(attribute => !cookie.Contains(attribute, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            // SameSite=None hebt den Schutz vor fremdem Mitschicken auf und verlangt zwingend
            // Secure; sonst weisen Browser das Cookie ganz ab.
            bool sameSiteNone = cookie.Contains("SameSite=None", StringComparison.OrdinalIgnoreCase);
            bool hasSecure = cookie.Contains("Secure", StringComparison.OrdinalIgnoreCase);

            if (missing.Length == 0 && sameSiteNone && hasSecure)
            {
                findings.Add(new Finding(
                    $"{Id}.samesitenone.{name}",
                    Category,
                    Severity.Info,
                    DomainText.Format($"Cookie {name}: SameSite=None erlaubt fremdes Mitschicken"),
                    DomainText.Get("Mit SameSite=None wird das Cookie auch bei Anfragen von fremden Seiten mitgeschickt. Nur nötig, wenn die Seite das ausdrücklich braucht."),
                    new Recommendation(DomainText.Get("Wenn kein fremder Kontext nötig ist, auf SameSite=Lax oder Strict stellen."))));
                continue;
            }

            findings.Add(missing.Length == 0
                ? new Finding($"{Id}.ok.{name}", Category, Severity.Ok, DomainText.Format($"Cookie sauber gesetzt: {name} (Secure, HttpOnly, SameSite)"))
                : new Finding(
                    $"{Id}.weak.{name}",
                    Category,
                    Severity.Warning,
                    DomainText.Format($"Cookie {name}: {string.Join(", ", missing)} fehlt"),
                    DomainText.Get("Ohne diese Angaben lässt sich das Cookie leichter abgreifen oder ungewollt mitschicken."),
                    new Recommendation(DomainText.Get("Dem Cookie Secure, HttpOnly und SameSite mitgeben."), $"Set-Cookie: {name}=…; Secure; HttpOnly; SameSite=Lax")));
        }

        return findings;
    }
}
