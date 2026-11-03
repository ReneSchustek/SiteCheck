using Microsoft.Extensions.Logging;
using SiteCheck.Application;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Localization;
using SiteCheck.Core.Models;
using SiteCheck.Data.Logging;
using SiteCheck.Data.Probes;
using SiteCheck.Data.Storage;

using HttpClient following = ProbeHttpClients.CreateFollowing();
using HttpClient nonFollowing = ProbeHttpClients.CreateNonFollowing();

// Schmaler Konsolen-Zugang auf derselben Prüflogik wie die Oberfläche. Klopft eine Adresse
// von außen ab und gibt die Befunde aus. Der Lasttest ist der Oberfläche vorbehalten, weil er eine
// ausdrückliche Bestätigung verlangt. Die Sperrwache gilt hier genauso: Ihr Stand liegt in derselben
// Datei wie bei der Oberfläche, denn der Hoster zählt die Anfragen beider zusammen.
//
// Aufruf:  sitecheck https://music.example.com [--sensitive-paths] [--retry-during-halt]

Console.OutputEncoding = System.Text.Encoding.UTF8;

// Die Ausgabe ist hier die Benutzerschnittstelle. Sie läuft über einen Schreiber, damit klar bleibt,
// dass diese Zeilen zum Produkt gehören und keine vergessene Fehlersuche sind.
TextWriter output = Console.Out;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    output.WriteLine(DomainText.Get("Aufruf: sitecheck <adresse> [--sensitive-paths] [--retry-during-halt]"));
    output.WriteLine(DomainText.Get("Beispiel: sitecheck https://music.example.com"));
    output.WriteLine();
    output.WriteLine(DomainText.Get("  --sensitive-paths    fragt auch .git, .env, /backup/ und andere ab. Kann den eigenen Anschluss sperren."));
    output.WriteLine(DomainText.Get("  --retry-during-halt  hebt eine laufende Sperrzeit auf. Verlängert eine bestehende Hostersperre."));
    output.WriteLine();
    output.WriteLine(DomainText.Get("Rückgabewert: 0 in Ordnung, 1 kritischer Befund, 2 ungültige Eingabe, 3 Lauf angehalten."));
    return 0;
}

if (!CheckTarget.TryParse(args[0], out CheckTarget? target, out TargetParseError error))
{
    Console.Error.WriteLine(ErrorMessage(args[0], error));
    return 2;
}

IHttpProbe httpProbe = new HttpProbe(following, nonFollowing);
ITlsProbe tlsProbe = new TlsProbe();
IDnsProbe dnsProbe = new DnsProbe();

AppPaths paths = new();
using FileLoggerProvider logging = new(paths.LogDirectory, TimeProvider.System);
ILogger logger = logging.CreateLogger("Konsole");
HostRequestGuard guard = new(TimeProvider.System, new RequestGuardStore(paths), dnsProbe, logger);
if (args.Contains("--retry-during-halt"))
{
    await guard.ReleaseAsync(target.Host, CancellationToken.None);
}

CheckRunnerFactory runnerFactory = new(httpProbe, tlsProbe, dnsProbe, TimeProvider.System, guard, logger);
CheckRunner runner = runnerFactory.Create(args.Contains("--sensitive-paths") ? CheckRunOptions.WithSensitivePaths : CheckRunOptions.Standard);

output.WriteLine();
output.WriteLine(DomainText.Format($"  Sicherheits-Check  {target}"));
output.WriteLine($"  {TimeProvider.System.GetLocalNow():dd.MM.yyyy HH:mm}");
output.WriteLine(DomainText.Get("  Fragt langsam ab und hält bei der ersten Abweisung an. Ein Hoster kann den Anschluss trotzdem sperren."));
output.WriteLine(new string('-', 60));

CheckReport report = await runner.RunAsync(target, TimeProvider.System, progress: null, CancellationToken.None);

foreach (CheckCategory category in Enum.GetValues<CheckCategory>())
{
    Finding[] inCategory = report.Findings.Where(finding => finding.Category == category).ToArray();
    if (inCategory.Length == 0)
    {
        continue;
    }

    output.WriteLine();
    output.WriteLine("  " + CategoryTitle(category));
    output.WriteLine();
    foreach (Finding finding in inCategory)
    {
        Write(finding);
    }
}

FindingSummary summary = report.Summary;
output.WriteLine();
output.WriteLine(new string('-', 60));
output.WriteLine(DomainText.Format($"  Ergebnis: {summary.OkCount} in Ordnung, {summary.WarningCount} zu verbessern, {summary.CriticalCount} kritisch."));
output.WriteLine();

// Rückgabewert für geplante Läufe: Ein angehaltener Lauf ist unvollständig und zählt deshalb
// weder als „in Ordnung“ noch als kritischer Befund über die Seite.
if (report.WasHalted)
{
    return 3;
}

return summary.CriticalCount > 0 ? 1 : 0;

void Write(Finding finding)
{
    (string marker, ConsoleColor color) = finding.Severity switch
    {
        Severity.Ok => ("[ok]      ", ConsoleColor.Green),
        Severity.Info => (("[" + DomainText.Get("Hinweis") + "]").PadRight(10), ConsoleColor.Cyan),
        Severity.Warning => ("[!]       ", ConsoleColor.Yellow),
        Severity.Critical => ("[" + DomainText.Get("kritisch") + "]", ConsoleColor.Red),
        _ => ("          ", ConsoleColor.Gray),
    };

    ConsoleColor previous = Console.ForegroundColor;
    Console.ForegroundColor = color;
    output.Write("  " + marker);
    Console.ForegroundColor = previous;
    output.WriteLine(" " + finding.Title);

    WriteIndentedBlock(finding.Detail);
    if (finding.Recommendation is { } recommendation)
    {
        output.WriteLine("              → " + recommendation.Advice);
        WriteIndentedBlock(recommendation.Example, "                ");
    }
}

void WriteIndentedBlock(string? text, string indent = "              ")
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return;
    }

    foreach (string line in text.Split('\n'))
    {
        output.WriteLine(indent + line.TrimEnd());
    }
}

static string CategoryTitle(CheckCategory category) => category switch
{
    CheckCategory.Web => DomainText.Get("Web"),
    CheckCategory.Mail => DomainText.Get("Mail-Einrichtung"),
    _ => category.ToString(),
};

static string ErrorMessage(string input, TargetParseError error) => error switch
{
    TargetParseError.Empty => DomainText.Get("Keine Adresse angegeben."),
    TargetParseError.UnsupportedScheme => DomainText.Format($"Nur http- und https-Adressen werden geprüft: {input}"),
    TargetParseError.ContainsCredentials => DomainText.Get("Die Adresse darf keine Zugangsdaten enthalten."),
    _ => DomainText.Format($"Keine gültige Web-Adresse: {input}"),
};
