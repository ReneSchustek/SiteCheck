using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Probes;

namespace SiteCheck.Core.Checks;

/// <summary>
/// Zeigt der Server an gängigen Pfaden eine offene Verzeichnis-Auflistung? Dann kann jeder die
/// abgelegten Dateien durchsehen. Erkennbar an der typischen „Index of /…“-Seite.
/// </summary>
public sealed class DirectoryListingCheck(IHttpProbe http) : ICheck
{
    private static readonly string[] Paths = ["/", "/assets/", "/uploads/", "/images/", "/files/", "/backup/"];

    /// <inheritdoc/>
    public string Id => "web.listing";

    /// <inheritdoc/>
    public CheckCategory Category => CheckCategory.Web;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Finding>> RunAsync(CheckTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        // Ist die Startseite nicht erreichbar, die Pfad-Anfragen überspringen (Cache-Treffer).
        if (await http.FetchAsync(target.Address, cancellationToken).ConfigureAwait(false) is null)
        {
            return [new Finding(Id + ".skipped", Category, Severity.Info, "Nicht geprüft — die Seite war nicht erreichbar")];
        }

        List<string> open = [];
        foreach (string path in Paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Uri address = new(target.Address, path);
            HttpProbeResponse? response = await http.FetchAsync(address, cancellationToken).ConfigureAwait(false);
            if (response is { StatusCode: 200 } && IsListing(response.BodyPrefix))
            {
                open.Add(path);
            }
        }

        return open.Count == 0
            ? [new Finding(Id + ".ok", Category, Severity.Ok, "Keine offene Verzeichnis-Auflistung an den geprüften Pfaden")]
            :
            [
                new Finding(
                    Id + ".open",
                    Category,
                    Severity.Warning,
                    $"Offene Verzeichnis-Auflistung an {open.Count} Pfad(en)",
                    string.Join("\n", open.Select(path => "- " + path)),
                    new Recommendation(
                        "Die Verzeichnis-Auflistung im Webserver abschalten.",
                        "Options -Indexes")),
            ];
    }

    // Apache und nginx leiten ihre Auflistung mit „Index of /…“ ein; das trennt sie von einer
    // gewöhnlichen Inhaltsseite, die zufällig unter dem Pfad liegt.
    private static bool IsListing(string body) =>
        body.Contains("Index of /", StringComparison.OrdinalIgnoreCase)
        || body.Contains("<title>Directory listing for", StringComparison.OrdinalIgnoreCase);
}
