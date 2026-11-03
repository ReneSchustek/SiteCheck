using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace SiteCheck.Core.Localization;

/// <summary>
/// Übersetzt die Texte der Befunde und Berichte in die Anzeigesprache. Der deutsche Satz selbst ist
/// der Schlüssel: Die Prüfungen bleiben so lesbar, und fehlt eine Übersetzung, steht dort Deutsch
/// statt einer Lücke. Eine Datei liegt eingebettet im Programm, daher braucht der Kern kein Paket.
/// </summary>
public static class DomainText
{
    // Kein Sprachkürzel im Dateinamen: MSBuild verschöbe die Datei sonst in eine eigene Satelliten-Assembly.
    private const string EnglishTableResource = "SiteCheck.Core.Localization.domain-texts-english.json";

    private static readonly Lazy<Dictionary<string, string>> English = new(LoadEnglish);

    /// <summary>Alle deutschen Schlüssel der englischen Tabelle, für die Vollständigkeitsprüfung.</summary>
    public static IReadOnlyCollection<string> EnglishKeys => English.Value.Keys;

    /// <summary>Übersetzt einen festen Text.</summary>
    /// <param name="german">Der deutsche Text.</param>
    /// <returns>Der Text in der Anzeigesprache.</returns>
    public static string Get(string german) => Translate(german);

    /// <summary>Übersetzt einen Text mit Werten; die Werte werden nach der aktuellen Kultur formatiert.</summary>
    /// <param name="german">Der deutsche Text als interpolierte Zeichenkette.</param>
    /// <returns>Der fertige Text in der Anzeigesprache.</returns>
    public static string Format(FormattableString german)
    {
        ArgumentNullException.ThrowIfNull(german);
        return string.Format(CultureInfo.CurrentCulture, Translate(german.Format), german.GetArguments());
    }

    // Deutsch fängt jede Sprache ohne eigene Tabelle auf, wie bei den Sprachdateien der Oberfläche.
    private static string Translate(string german) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
        && English.Value.TryGetValue(german, out string? english)
            ? english
            : german;

    private static Dictionary<string, string> LoadEnglish()
    {
        using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EnglishTableResource);
        if (stream is null)
        {
            return [];
        }

        using JsonDocument document = JsonDocument.Parse(stream);
        return document.RootElement.EnumerateObject()
            .ToDictionary(entry => entry.Name, entry => entry.Value.GetString() ?? entry.Name, StringComparer.Ordinal);
    }
}
