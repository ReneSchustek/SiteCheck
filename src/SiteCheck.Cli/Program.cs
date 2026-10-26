using System.Globalization;
using SiteCheck.Application;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;
using SiteCheck.Data.Probes;

// Schmaler Konsolen-Zugang auf derselben Prüflogik wie die Oberfläche. Klopft eine Adresse
// von außen ab und gibt die Befunde aus. Alles passiv; der Lasttest ist der Oberfläche
// vorbehalten, weil er eine ausdrückliche Bestätigung verlangt.
//
// Aufruf:  sitecheck https://music.example.com

Console.OutputEncoding = System.Text.Encoding.UTF8;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine("Aufruf: sitecheck <adresse>");
    Console.WriteLine("Beispiel: sitecheck https://music.example.com");
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

CheckRunnerFactory runnerFactory = new(httpProbe, tlsProbe, dnsProbe, TimeProvider.System);
CheckRunner runner = runnerFactory.Create();

Console.WriteLine();
Console.WriteLine($"  Sicherheits-Check  {target}");
Console.WriteLine($"  {DateTime.Now:dd.MM.yyyy HH:mm}");
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

// Rückgabewert für geplante Läufe: >0, sobald etwas Kritisches dabei ist.
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
