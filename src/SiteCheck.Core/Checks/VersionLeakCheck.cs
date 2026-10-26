using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Probes;

namespace SiteCheck.Core.Checks;

/// <summary>
/// Verrät der Server seine genaue Version? Eine Versionsnummer in <c>Server</c> oder
/// <c>X-Powered-By</c> sagt einem Angreifer sofort, welche bekannten Lücken in Frage kommen.
/// </summary>
public sealed class VersionLeakCheck(IHttpProbe http) : ICheck
{
    private static readonly string[] RevealingHeaders = ["Server", "X-Powered-By"];

    /// <inheritdoc/>
    public string Id => "web.version";

    /// <inheritdoc/>
    public CheckCategory Category => CheckCategory.Web;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Finding>> RunAsync(CheckTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        HttpProbeResponse? response = await http.FetchAsync(target.Address, cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            return [];
        }

        List<Finding> findings = [];
        foreach (string headerName in RevealingHeaders)
        {
            string? value = response.GetHeader(headerName);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            // Erst eine Ziffer macht aus dem Namen eine Version ("nginx" allein ist harmlos,
            // "nginx/1.24.0" nennt die angreifbare Fassung).
            if (value.Any(char.IsDigit))
            {
                findings.Add(new Finding(
                    $"{Id}.{headerName}",
                    Category,
                    Severity.Warning,
                    $"{headerName} verrät die Version: {value}",
                    "Ein Angreifer sieht sofort, welche Fassung er sich vornehmen kann.",
                    new Recommendation($"Die Versionsangabe aus der {headerName}-Kopfzeile entfernen oder kürzen.")));
            }
        }

        if (findings.Count == 0)
        {
            findings.Add(new Finding(Id + ".ok", Category, Severity.Ok, "Der Server nennt keine genaue Version"));
        }

        return findings;
    }
}
