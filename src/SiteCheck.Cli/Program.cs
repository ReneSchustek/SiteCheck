using System.Globalization;
using SiteCheck.Application;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;
using SiteCheck.Data.Probes;
using SiteCheck.Data.Storage;

// Schmaler Konsolen-Zugang auf derselben Prüflogik wie die Oberfläche. Klopft eine Adresse
// von außen ab und gibt die Befunde aus. Der Lasttest ist der Oberfläche vorbehalten, weil er eine
// ausdrückliche Bestätigung verlangt. Die Sperrwache gilt hier genauso: Ihr Stand liegt in derselben
// Datei wie bei der Oberfläche, denn der Hoster zählt die Anfragen beider zusammen.
//
// Aufruf:  sitecheck https://music.example.com [--sensitive-paths] [--retry-during-halt]

Console.OutputEncoding = System.Text.Encoding.UTF8;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine("Aufruf: sitecheck <adresse> [--sensitive-paths] [--retry-during-halt]");
    Console.WriteLine("Beispiel: sitecheck https://music.example.com");
    Console.WriteLine();
    Console.WriteLine("  --sensitive-paths    fragt auch .git, .env, /backup/ und andere ab. Kann den eigenen Anschluss sperren.");
    Console.WriteLine("  --retry-during-halt  hebt eine laufende Sperrzeit auf. Verlängert eine bestehende Hostersperre.");
    Console.WriteLine();
    Console.WriteLine("Rückgabewert: 0 in Ordnung, 1 kritischer Befund, 2 ungültige Eingabe, 3 Lauf angehalten.");
    return 0;
}

if (!CheckTarget.TryParse(args[0], out CheckTarget? target, out TargetParseError error))
{
    Console.Error.WriteLine(ErrorMessage(args[0], error));
    return 2;
}

using HttpClient following = ProbeHttpClients.CreateFollowing();
using HttpClient nonFollowing = ProbeHttpClients.CreateNonFollowing();
IHttpProbe httpProbe = new HttpProbe(following, nonFollowing);
ITlsProbe tlsProbe = new TlsProbe();
IDnsProbe dnsProbe = new DnsProbe();

HostRequestGuard guard = new(TimeProvider.System, new RequestGuardStore(new AppPaths()), dnsProbe);
if (args.Contains("--retry-during-halt"))
{
    await guard.ReleaseAsync(target.Host, CancellationToken.None);
}

CheckRunnerFactory runnerFactory = new(httpProbe, tlsProbe, dnsProbe, TimeProvider.System, guard);
CheckRunner runner = runnerFactory.Create(args.Contains("--sensitive-paths") ? CheckRunOptions.WithSensitivePaths : CheckRunOptions.Standard);

Console.WriteLine();
Console.WriteLine($"  Sicherheits-Check  {target}");
Console.WriteLine($"  {DateTime.Now:dd.MM.yyyy HH:mm}");
Console.WriteLine("  Fragt langsam ab und hält bei der ersten Abweisung an. Ein Hoster kann den Anschluss trotzdem sperren.");
Console.WriteLine(new string('-', 60));

CheckReport report = await runner.RunAsync(target, TimeProvider.System, progress: null, CancellationToken.None);

foreach (CheckCategory category in Enum.GetValues<CheckCategory>())
{
    Finding[] inCategory = report.Findings.Where(finding => finding.Category == category).ToArray();
    if (inCategory.Length == 0)
    {
        continue;
    }

    Console.WriteLine();
    Console.WriteLine("  " + CategoryTitle(category));
    Console.WriteLine();
    foreach (Finding finding in inCategory)
    {
        Write(finding);
    }
}

FindingSummary summary = report.Summary;
Console.WriteLine();
Console.WriteLine(new string('-', 60));
Console.WriteLine($"  Ergebnis: {summary.OkCount} in Ordnung, {summary.WarningCount} zu verbessern, {summary.CriticalCount} kritisch.");
Console.WriteLine();

// Rückgabewert für geplante Läufe: Ein angehaltener Lauf ist unvollständig und zählt deshalb
// weder als „in Ordnung“ noch als kritischer Befund über die Seite.
if (report.WasHalted)
{
    return 3;
}

return summary.CriticalCount > 0 ? 1 : 0;

static void Write(Finding finding)
{
    (string marker, ConsoleColor color) = finding.Severity switch
    {
        Severity.Ok => ("[ok]      ", ConsoleColor.Green),
        Severity.Info => ("[hinweis] ", ConsoleColor.Cyan),
        Severity.Warning => ("[!]       ", ConsoleColor.Yellow),
        Severity.Critical => ("[kritisch]", ConsoleColor.Red),
        _ => ("          ", ConsoleColor.Gray),
    };

    ConsoleColor previous = Console.ForegroundColor;
    Console.ForegroundColor = color;
    Console.Write("  " + marker);
    Console.ForegroundColor = previous;
    Console.WriteLine(" " + finding.Title);

    WriteIndentedBlock(finding.Detail);
    if (finding.Recommendation is { } recommendation)
    {
        Console.WriteLine("              → " + recommendation.Advice);
        WriteIndentedBlock(recommendation.Example, "                ");
    }
}

static void WriteIndentedBlock(string? text, string indent = "              ")
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return;
    }

    foreach (string line in text.Split('\n'))
    {
        Console.WriteLine(indent + line.TrimEnd());
    }
}

static string CategoryTitle(CheckCategory category) => category switch
{
    CheckCategory.Web => "Web",
    CheckCategory.Mail => "Mail-Einrichtung",
    _ => category.ToString(),
};

static string ErrorMessage(string input, TargetParseError error) => error switch
{
    TargetParseError.Empty => "Keine Adresse angegeben.",
    TargetParseError.UnsupportedScheme => $"Nur http- und https-Adressen werden geprüft: {input}",
    TargetParseError.ContainsCredentials => "Die Adresse darf keine Zugangsdaten enthalten.",
    _ => string.Create(CultureInfo.InvariantCulture, $"Keine gültige Web-Adresse: {input}"),
};
