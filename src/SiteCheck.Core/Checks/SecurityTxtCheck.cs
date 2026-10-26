using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Probes;

namespace SiteCheck.Core.Checks;

/// <summary>
/// Gibt es eine <c>/.well-known/security.txt</c> (RFC 9116)? Darin steht, an wen man
/// Sicherheitslücken melden kann. Kein Muss, nur ein Reifegrad-Zeichen.
/// </summary>
public sealed class SecurityTxtCheck(IHttpProbe http) : ICheck
{
    /// <inheritdoc/>
    public string Id => "web.securitytxt";

    /// <inheritdoc/>
    public CheckCategory Category => CheckCategory.Web;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Finding>> RunAsync(CheckTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        // Ist die Startseite nicht erreichbar, lohnt die security.txt-Anfrage nicht.
        if (await http.FetchAsync(target.Address, cancellationToken).ConfigureAwait(false) is null)
        {
            return [new Finding(Id + ".skipped", Category, Severity.Info, "Nicht geprüft — die Seite war nicht erreichbar")];
        }

        Uri address = new(target.Address, "/.well-known/security.txt");
        HttpProbeResponse? response = await http.FetchAsync(address, cancellationToken).ConfigureAwait(false);

        // Eine echte security.txt nennt mindestens ein Kontakt-Feld; eine 200er-HTML-Seite
        // (weiche 404) erfüllt das nicht und zählt deshalb nicht als vorhanden.
        bool present = response is { StatusCode: 200 }
            && response.BodyPrefix.Contains("Contact:", StringComparison.OrdinalIgnoreCase);

        return present
            ? [new Finding(Id, Category, Severity.Ok, "security.txt vorhanden")]
            :
            [
                new Finding(
                    Id + ".absent",
                    Category,
                    Severity.Info,
                    "Keine security.txt hinterlegt",
                    "Unter /.well-known/security.txt kann stehen, an wen man Sicherheitslücken melden soll. Kein "
                    + "Muss, aber ein gutes Zeichen.",
                    new Recommendation("Eine security.txt nach RFC 9116 anlegen.", "Contact: mailto:security@deinedomain.de")),
            ];
    }
}
