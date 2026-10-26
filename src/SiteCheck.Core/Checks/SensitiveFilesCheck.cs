using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Probes;

namespace SiteCheck.Core.Checks;

/// <summary>
/// Sind heikle Dateien von außen lesbar? Ein fester, kurzer Satz an Pfaden wird abgefragt;
/// erwartet wird je 403 oder 404. Eine 200 mit passendem Inhalt ist kritisch.
/// </summary>
public sealed class SensitiveFilesCheck(IHttpProbe http) : ICheck
{
    // Pfad und eine kurze Signatur, die im Anfang des Inhalts stehen muss. Die Signatur
    // trennt die echte Datei von einer Seite, die auf alles mit 200 und HTML antwortet
    // (SPA-Catch-all, weiche 404). Leere Signatur: schon die 200 genügt als Verdacht.
    private static readonly SensitiveFile[] Files =
    [
        new(".git/HEAD", "ref:"),
        new(".git/config", "[core]"),
        new(".env", "="),
        new("composer.json", "{"),
        new("composer.lock", "{"),
        new(".htaccess", ""),
        new("config.php", "<?php"),
        new("backup.zip", "PK"),
        new(".DS_Store", ""),
    ];

    /// <inheritdoc/>
    public string Id => "web.files";

    /// <inheritdoc/>
    public CheckCategory Category => CheckCategory.Web;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Finding>> RunAsync(CheckTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        // Zuerst die Startseite (Cache-Treffer, wenn eine vorige Prüfung sie schon geholt hat).
        // Ist sie nicht erreichbar, sparen wir uns die vielen Pfad-Anfragen, die sonst nur in
        // Timeouts liefen.
        if (await http.FetchAsync(target.Address, cancellationToken).ConfigureAwait(false) is null)
        {
            return [new Finding(Id + ".skipped", Category, Severity.Info, "Nicht geprüft — die Seite war nicht erreichbar")];
        }

        List<string> exposed = [];
        foreach (SensitiveFile file in Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Uri probeAddress = new(target.Address, file.Path);
            HttpProbeResponse? response = await http.FetchAsync(probeAddress, cancellationToken).ConfigureAwait(false);
            if (response is not null && IsExposed(response, file))
            {
                exposed.Add(file.Path);
            }
        }

        if (exposed.Count == 0)
        {
            return
            [
                new Finding(Id + ".ok", Category, Severity.Ok, "Keine der geprüften heiklen Dateien ist offen erreichbar"),
            ];
        }

        return
        [
            new Finding(
                Id + ".exposed",
                Category,
                Severity.Critical,
                $"{exposed.Count} heikle Datei(en) offen lesbar",
                string.Join("\n", exposed.Select(path => "- " + path)),
                new Recommendation(
                    "Den Zugriff auf diese Pfade serverseitig sperren und die Dateien aus dem Web-Wurzelverzeichnis nehmen.",
                    "RewriteRule \"(^|/)\\.(git|env|htaccess)\" - [F]")),
        ];
    }

    private static bool IsExposed(HttpProbeResponse response, SensitiveFile file)
    {
        if (response.StatusCode != 200)
        {
            return false;
        }

        return file.BodySignature.Length == 0
            || response.BodyPrefix.Contains(file.BodySignature, StringComparison.Ordinal);
    }

    private sealed record SensitiveFile(string Path, string BodySignature);
}
