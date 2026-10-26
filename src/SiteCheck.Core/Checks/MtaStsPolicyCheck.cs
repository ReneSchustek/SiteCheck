using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Probes;

namespace SiteCheck.Core.Checks;

/// <summary>
/// Liest die MTA-STS-Policy-Datei unter <c>https://mta-sts.&lt;domain&gt;/.well-known/mta-sts.txt</c>.
/// Erst mit <c>mode: enforce</c> wirkt MTA-STS scharf; der DNS-Eintrag allein ist nur die
/// Ankündigung. Fehlt die Datei, meldet diese Prüfung nichts — das übernimmt die DNS-Prüfung.
/// </summary>
public sealed class MtaStsPolicyCheck(IHttpProbe http) : ICheck
{
    private readonly IHttpProbe _http = http;

    /// <inheritdoc/>
    public string Id => "mail.mtasts.policy";

    /// <inheritdoc/>
    public CheckCategory Category => CheckCategory.Mail;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Finding>> RunAsync(CheckTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (target.IsIpAddress)
        {
            return [];
        }

        Uri policyUrl = new($"https://mta-sts.{target.MailDomain}/.well-known/mta-sts.txt");
        HttpProbeResponse? response = await _http.FetchAsync(policyUrl, cancellationToken).ConfigureAwait(false);

        // Keine erreichbare Policy-Datei: nichts melden, damit sich der Hinweis aus der
        // DNS-Prüfung ("MTA-STS nicht eingerichtet") nicht doppelt.
        if (response is not { StatusCode: 200 } || !response.BodyPrefix.Contains("mode:", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        string mode = ReadMode(response.BodyPrefix);
        return mode.Equals("enforce", StringComparison.OrdinalIgnoreCase)
            ? [new Finding($"{Id}.enforce", Category, Severity.Ok, "MTA-STS ist scharf gestellt (mode: enforce)")]
            :
            [
                new Finding(
                    $"{Id}.testing",
                    Category,
                    Severity.Info,
                    $"MTA-STS ist nur angekündigt (mode: {mode})",
                    "Im Modus testing oder none wird verschlüsselter Transport noch nicht erzwungen.",
                    new Recommendation("Nach einer Beobachtungszeit in der Policy-Datei auf mode: enforce stellen.")),
            ];
    }

    private static string ReadMode(string body)
    {
        foreach (string line in body.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("mode:", StringComparison.OrdinalIgnoreCase))
            {
                return line["mode:".Length..].Trim();
            }
        }

        return "?";
    }
}
