using System.Globalization;
using System.Net;
using System.Text;
using SiteCheck.Core.Models;

namespace SiteCheck.Application;

/// <summary>
/// Macht aus einem Prüfbericht Text zum Kopieren und eine schlichte HTML-Datei zum Speichern.
/// Beides ist für jemanden geschrieben, der kein Fachmann ist: je Befund ein klarer Satz, eine
/// Ampel in Worten und, wo es hilft, der konkrete Rat samt Beispiel zum Übernehmen. Enthält
/// nur Adresse und Befunde, nie personenbezogene Daten.
/// </summary>
public static class ReportBuilder
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Baut den Textbericht einer Seite.</summary>
    /// <param name="report">Der Prüfbericht.</param>
    /// <returns>Der Bericht als reiner Text.</returns>
    public static string BuildText(CheckReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        StringBuilder text = new();
        _ = text.Append("Sicherheits-Check für ").AppendLine(report.Address);
        _ = text.Append("Geprüft am ").AppendLine(report.StartedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm", German));
        _ = text.AppendLine();
        _ = text.AppendLine(SummaryLine(report.Summary));
        _ = text.AppendLine();
        _ = text.AppendLine("Was bedeuten die Zeichen: [ok] ist in Ordnung, [Hinweis] nur zur Kenntnis,");
        _ = text.AppendLine("[!] sollte verbessert werden, [kritisch] bitte zeitnah beheben.");
        _ = text.AppendLine();

        foreach (CheckCategory category in Enum.GetValues<CheckCategory>())
        {
            Finding[] inCategory = report.Findings.Where(finding => finding.Category == category).ToArray();
            if (inCategory.Length == 0)
            {
                continue;
            }

            _ = text.AppendLine(CategoryTitle(category).ToUpper(German));
            foreach (Finding finding in inCategory)
            {
                AppendTextFinding(text, finding);
            }

            _ = text.AppendLine();
        }

        return text.ToString().TrimEnd() + Environment.NewLine;
    }

    /// <summary>Baut den HTML-Bericht einer Seite, hell und dunkel lesbar.</summary>
    /// <param name="report">Der Prüfbericht.</param>
    /// <returns>Ein vollständiges HTML-Dokument.</returns>
    public static string BuildHtml(CheckReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        FindingSummary summary = report.Summary;
        StringBuilder html = new();
        _ = html.AppendLine("<!DOCTYPE html>");
        _ = html.AppendLine("<html lang=\"de\">");
        _ = html.AppendLine("<head>");
        _ = html.AppendLine("<meta charset=\"utf-8\">");
        _ = html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        _ = html.Append("<title>Sicherheits-Check ").Append(Encode(report.Address)).AppendLine("</title>");
        _ = html.AppendLine(Styles);
        _ = html.AppendLine("</head>");
        _ = html.AppendLine("<body>");
        _ = html.AppendLine("<main>");
        _ = html.AppendLine("<h1>Sicherheits-Check</h1>");
        _ = html.Append("<p class=\"addr\">").Append(Encode(report.Address)).AppendLine("</p>");
        _ = html.Append("<p class=\"meta\">Geprüft am ")
            .Append(Encode(report.StartedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm 'Uhr'", German)))
            .AppendLine("</p>");

        AppendHtmlSummary(html, summary);
        _ = html.AppendLine("<p class=\"legend\">Die Farbe und das Wort vor jedem Punkt sagen, wie dringend er ist.</p>");

        foreach (CheckCategory category in Enum.GetValues<CheckCategory>())
        {
            Finding[] inCategory = report.Findings.Where(finding => finding.Category == category).ToArray();
            if (inCategory.Length == 0)
            {
                continue;
            }

            _ = html.Append("<h2>").Append(Encode(CategoryTitle(category))).AppendLine("</h2>");
            foreach (Finding finding in inCategory)
            {
                AppendHtmlFinding(html, finding);
            }
        }

        _ = html.AppendLine("</main>");
        _ = html.AppendLine("</body>");
        _ = html.AppendLine("</html>");
        return html.ToString();
    }

    private static void AppendTextFinding(StringBuilder text, Finding finding)
    {
        string title = finding.Confidence == Confidence.Inferred ? finding.Title + "  (aus Sicht von außen)" : finding.Title;
        _ = text.Append("  ").Append(Marker(finding.Severity)).Append(' ').AppendLine(title);
        if (!string.IsNullOrWhiteSpace(finding.Detail))
        {
            foreach (string line in SplitLines(finding.Detail))
            {
                _ = text.Append("      ").AppendLine(line);
            }
        }

        if (finding.Recommendation is { } recommendation)
        {
            _ = text.Append("      → ").AppendLine(recommendation.Advice);
            if (!string.IsNullOrWhiteSpace(recommendation.Example))
            {
                foreach (string line in SplitLines(recommendation.Example))
                {
                    _ = text.Append("        ").AppendLine(line);
                }
            }
        }
    }

    private static void AppendHtmlSummary(StringBuilder html, FindingSummary summary)
    {
        _ = html.AppendLine("<div class=\"summary\">");
        _ = html.Append("<span class=\"pill ok\">").Append(summary.OkCount).AppendLine(" in Ordnung</span>");
        _ = html.Append("<span class=\"pill warning\">").Append(summary.WarningCount).AppendLine(" zu verbessern</span>");
        _ = html.Append("<span class=\"pill critical\">").Append(summary.CriticalCount).AppendLine(" kritisch</span>");
        _ = html.AppendLine("</div>");
    }

    private static void AppendHtmlFinding(StringBuilder html, Finding finding)
    {
        _ = html.Append("<div class=\"finding ").Append(CssClass(finding.Severity)).AppendLine("\">");
        _ = html.Append("<p class=\"title\"><span class=\"badge\">").Append(Encode(SeverityWord(finding.Severity)))
            .Append("</span> ").Append(Encode(finding.Title));
        if (finding.Confidence == Confidence.Inferred)
        {
            _ = html.Append(" <span class=\"inferred\">aus Sicht von außen</span>");
        }

        _ = html.AppendLine("</p>");

        if (!string.IsNullOrWhiteSpace(finding.Detail))
        {
            _ = html.Append("<p class=\"detail\">").Append(EncodeMultiline(finding.Detail)).AppendLine("</p>");
        }

        if (finding.Recommendation is { } recommendation)
        {
            _ = html.Append("<p class=\"advice\"><strong>Empfehlung:</strong> ").Append(Encode(recommendation.Advice)).AppendLine("</p>");
            if (!string.IsNullOrWhiteSpace(recommendation.Example))
            {
                _ = html.Append("<pre>").Append(EncodeMultiline(recommendation.Example)).AppendLine("</pre>");
            }
        }

        _ = html.AppendLine("</div>");
    }

    private static string SummaryLine(FindingSummary summary) =>
        $"Ergebnis: {summary.OkCount} in Ordnung, {summary.WarningCount} zu verbessern, {summary.CriticalCount} kritisch.";

    private static string CategoryTitle(CheckCategory category) => category switch
    {
        CheckCategory.Web => "Web",
        CheckCategory.Mail => "Mail-Einrichtung",
        _ => category.ToString(),
    };

    private static string Marker(Severity severity) => severity switch
    {
        Severity.Ok => "[ok]",
        Severity.Info => "[Hinweis]",
        Severity.Warning => "[!]",
        Severity.Critical => "[kritisch]",
        _ => "[?]",
    };

    // Feste Kleinschreibung je Stufe als CSS-Klassenname. Bewusst eine Zuordnung statt
    // ToLowerInvariant: Der Klassenname ist ein fester Bezeichner, keine umzuwandelnde Eingabe.
    private static string CssClass(Severity severity) => severity switch
    {
        Severity.Ok => "ok",
        Severity.Info => "info",
        Severity.Warning => "warning",
        Severity.Critical => "critical",
        _ => "info",
    };

    private static string SeverityWord(Severity severity) => severity switch
    {
        Severity.Ok => "In Ordnung",
        Severity.Info => "Hinweis",
        Severity.Warning => "Zu verbessern",
        Severity.Critical => "Kritisch",
        _ => "Unbekannt",
    };

    private static string[] SplitLines(string value) =>
        value.Split('\n', StringSplitOptions.None).Select(line => line.TrimEnd()).ToArray();

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private static string EncodeMultiline(string value) => Encode(value).Replace("\n", "<br>", StringComparison.Ordinal);

    // Farben als CSS-Variablen mit heller und dunkler Belegung, damit die Datei in beiden
    // Erscheinungsbildern lesbar bleibt. Gedeckte Statusfarben nach der Gestaltungslinie;
    // der Zustand steht zusätzlich als Wort vor jedem Punkt, nie allein über die Farbe.
    private const string Styles = """
        <style>
          :root {
            color-scheme: light dark;
            --bg: #f5f5f5; --surface: #ffffff; --text: #1a1a1a; --muted: #5a5a5a;
            --border: rgba(0,0,0,0.12);
            --ok: #1e7a45; --info: #26708f; --warning: #8a5a00; --critical: #b3261e;
          }
          @media (prefers-color-scheme: dark) {
            :root {
              --bg: #0f1923; --surface: #152030; --text: #e4edf2; --muted: #8aa3b0;
              --border: rgba(255,255,255,0.1);
            }
          }
          body { font-family: system-ui, -apple-system, "Segoe UI", sans-serif; line-height: 1.6;
                 background: var(--bg); color: var(--text); margin: 0; padding: 24px; }
          main { max-width: 760px; margin: 0 auto; }
          h1 { font-size: 1.6rem; margin: 0 0 4px; }
          h2 { font-size: 0.8rem; text-transform: uppercase; letter-spacing: 0.08em;
               color: var(--muted); margin: 28px 0 10px; }
          .addr { font-size: 1.05rem; margin: 0; word-break: break-all; }
          .meta { color: var(--muted); margin: 2px 0 14px; }
          .summary { display: flex; flex-wrap: wrap; gap: 8px; margin: 0 0 6px; }
          .pill { border-radius: 999px; padding: 4px 12px; font-weight: 600; font-size: 0.9rem;
                  border: 1px solid var(--border); }
          .pill.ok { color: var(--ok); } .pill.warning { color: var(--warning); } .pill.critical { color: var(--critical); }
          .legend { color: var(--muted); font-size: 0.85rem; margin: 0 0 8px; }
          .finding { background: var(--surface); border: 1px solid var(--border);
                     border-left-width: 4px; border-radius: 8px; padding: 10px 14px; margin: 8px 0; }
          .finding.ok { border-left-color: var(--ok); }
          .finding.info { border-left-color: var(--info); }
          .finding.warning { border-left-color: var(--warning); }
          .finding.critical { border-left-color: var(--critical); }
          .title { margin: 0; font-weight: 600; }
          .badge { display: inline-block; font-size: 0.72rem; text-transform: uppercase; letter-spacing: 0.05em;
                   padding: 1px 7px; border-radius: 4px; border: 1px solid var(--border); margin-right: 6px; }
          .ok .badge { color: var(--ok); } .info .badge { color: var(--info); }
          .warning .badge { color: var(--warning); } .critical .badge { color: var(--critical); }
          .inferred { font-size: 0.72rem; color: var(--muted); font-style: italic; margin-left: 6px; }
          .detail { margin: 6px 0 0; color: var(--muted); }
          .advice { margin: 6px 0 0; }
          pre { background: var(--bg); border: 1px solid var(--border); border-radius: 6px;
                padding: 8px 10px; overflow-x: auto; font-family: "Roboto Mono", ui-monospace, monospace;
                font-size: 0.85rem; }
        </style>
        """;
}
