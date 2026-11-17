using System.Reflection;
using System.Text;

namespace SiteCheck.Application;

/// <summary>
/// Liefert die Schriften des HTML-Berichts als <c>@font-face</c>-Regeln mit eingebetteten Daten.
/// Der Bericht ist eine eigenständige Datei, die weitergegeben wird; er lädt nichts von fremden
/// Hosts, also muss die Schrift in ihm stecken. Die Dateien sind verkleinerte Teilmengen von
/// Roboto Flex und Roboto Mono (siehe <c>tools/build-report-fonts.ps1</c>).
/// </summary>
internal static class ReportFonts
{
    internal const string FlexResourceName = "SiteCheck.Fonts.roboto-flex-latin.woff2";
    internal const string MonoResourceName = "SiteCheck.Fonts.roboto-mono-latin.woff2";

    // Gelesen wird einmal je Prozess: Die Regeln sind unveränderlich, und jeder Bericht bräuchte
    // sonst rund 80 KB Lesen und Umwandeln aufs Neue.
    private static readonly Lazy<string> CachedStyle = new(BuildStyle, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Der vollständige <c>&lt;style&gt;</c>-Block mit beiden Schriften.</summary>
    internal static string Style => CachedStyle.Value;

    private static string BuildStyle()
    {
        StringBuilder css = new();
        _ = css.AppendLine("<style>");
        AppendFace(css, "Roboto Flex", "300 900", FlexResourceName);
        AppendFace(css, "Roboto Mono", "100 700", MonoResourceName);
        _ = css.AppendLine("</style>");
        return css.ToString();
    }

    // font-display: swap zeigt den Text sofort; bei eingebetteten Daten ist der Wechsel ohnehin
    // nicht zu sehen, verhindert aber unsichtbaren Text, falls ein Browser das Dekodieren aufschiebt.
    private static void AppendFace(StringBuilder css, string family, string weightRange, string resourceName)
    {
        _ = css.Append("  @font-face { font-family: \"").Append(family)
            .Append("\"; font-style: normal; font-weight: ").Append(weightRange)
            .Append("; font-display: swap; src: url(data:font/woff2;base64,")
            .Append(Convert.ToBase64String(ReadResource(resourceName)))
            .AppendLine(") format(\"woff2\"); }");
    }

    internal static byte[] ReadResource(string resourceName)
    {
        Assembly assembly = typeof(ReportFonts).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Die Berichtsschrift '{resourceName}' fehlt in {assembly.GetName().Name}. Die Datei muss als EmbeddedResource eingebunden sein.");
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
