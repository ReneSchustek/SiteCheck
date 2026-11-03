using System.Globalization;
using System.Text.RegularExpressions;
using SiteCheck.Core.Localization;

namespace SiteCheck.Core.Tests.Localization;

/// <summary>
/// Hält die englische Tabelle vollständig: Jeder Text im Quellcode muss dort stehen, sonst bliebe er
/// auf englischem Windows still deutsch. Geprüft wird gegen den Quelltext, weil nur der alle Texte kennt.
/// </summary>
public sealed partial class DomainTextTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void EveryTextInSource_HasEnglishEntry_AndNoEntryIsUnused()
    {
        HashSet<string> used = [.. SourceKeys()];
        HashSet<string> table = [.. DomainText.EnglishKeys];

        Assert.Empty(used.Except(table));
        Assert.Empty(table.Except(used));
    }

    [Fact]
    public void NoGermanTextInChecks_BypassesTheTable()
    {
        List<string> bypassing = [];
        foreach (string file in Directory.GetFiles(Path.Combine(Root, "src", "SiteCheck.Core", "Checks"), "*.cs"))
        {
            foreach (string line in File.ReadAllLines(file))
            {
                string code = line.TrimStart();
                bool isComment = code.StartsWith("//", StringComparison.Ordinal);
                if (!isComment && !code.Contains("DomainText.", StringComparison.Ordinal) && GermanLiteral().IsMatch(code))
                {
                    bypassing.Add(Path.GetFileName(file) + ": " + code);
                }
            }
        }

        Assert.Empty(bypassing);
    }

    [Fact]
    public void EnglishEntries_KeepPlaceholderNumbers()
    {
        foreach (string key in DomainText.EnglishKeys)
        {
            string english = WithCulture("en-US", () => DomainText.Get(key));
            Assert.Equal(PlaceholderNumbers(key), PlaceholderNumbers(english));
        }
    }

    [Fact]
    public void Get_FollowsTheDisplayLanguage_AndFallsBackToGerman()
    {
        const string Key = "Kritisch";

        Assert.Equal("Critical", WithCulture("en-GB", () => DomainText.Get(Key)));
        Assert.Equal(Key, WithCulture("de-DE", () => DomainText.Get(Key)));
        Assert.Equal(Key, WithCulture("fr-FR", () => DomainText.Get(Key)));
    }

    [Fact]
    public void Get_UnknownText_ReturnsItUnchanged()
    {
        Assert.Equal("Unbekannter Satz", WithCulture("en-US", () => DomainText.Get("Unbekannter Satz")));
    }

    [Fact]
    public void Format_InsertsValuesIntoTranslation()
    {
        int port = 443;

        string english = WithCulture("en-US", () => DomainText.Format($"DKIM-Schlüssel ausreichend lang ({port} Bit)"));

        Assert.Contains("443", english, StringComparison.Ordinal);
        Assert.DoesNotContain("Schlüssel", english, StringComparison.Ordinal);
    }

    private static T WithCulture<T>(string culture, Func<T> action)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            return action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private static IEnumerable<string> SourceKeys()
    {
        string[] projects = ["SiteCheck.Core", "SiteCheck.Application", "SiteCheck.Cli"];
        foreach (string file in projects.SelectMany(project => Directory.GetFiles(Path.Combine(Root, "src", project), "*.cs", SearchOption.AllDirectories)))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.EndsWith("DomainText.cs", StringComparison.Ordinal))
            {
                continue;
            }

            string source = File.ReadAllText(file);
            foreach (Match match in Call().Matches(source))
            {
                yield return ReadKey(source, match.Index + match.Length, interpolated: match.Groups[1].Value == "Format");
            }
        }
    }

    // Liest das Literal ab der öffnenden Stelle und macht aus jeder Lücke {n}, wie FormattableString.Format.
    private static string ReadKey(string source, int start, bool interpolated)
    {
        System.Text.StringBuilder key = new();
        int index = start;
        int hole = 0;
        while (source[index] != '"')
        {
            char current = source[index];
            if (current == '\\')
            {
                _ = key.Append(source[index + 1] switch { 'n' => '\n', 'r' => '\r', 't' => '\t', char other => other });
                index += 2;
            }
            else if (interpolated && current == '{' && source[index + 1] != '{')
            {
                (string format, int end) = ReadHole(source, index);
                _ = key.Append('{').Append(hole++).Append(format).Append('}');
                index = end + 1;
            }
            else if (interpolated && (current is '{' or '}'))
            {
                _ = key.Append(current);
                _ = key.Append(current);
                index += 2;
            }
            else
            {
                _ = key.Append(current);
                index++;
            }
        }

        return key.ToString();
    }

    private static (string Format, int End) ReadHole(string source, int open)
    {
        int depth = 0;
        int colon = -1;
        for (int index = open; ; index++)
        {
            char current = source[index];
            if (current == '"')
            {
                index = source.IndexOf('"', index + 1);
            }
            else if (current is '{' or '(' or '[')
            {
                depth++;
            }
            else if (current is ')' or ']')
            {
                depth--;
            }
            else if (current == '}' && --depth == 0)
            {
                return (colon < 0 ? string.Empty : source[colon..index], index);
            }
            else if (current == ':' && depth == 1 && colon < 0)
            {
                colon = index;
            }
        }
    }

    private static string[] PlaceholderNumbers(string text) =>
        [.. Placeholder().Matches(text).Select(match => match.Groups[1].Value).Distinct().Order(StringComparer.Ordinal)];

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SiteCheck.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("SiteCheck.slnx nicht gefunden.");
    }

    [GeneratedRegex(@"""[^""]*[äöüÄÖÜß][^""]*""", RegexOptions.CultureInvariant)]
    private static partial Regex GermanLiteral();

    [GeneratedRegex(@"DomainText\.(Get|Format)\(\$?""", RegexOptions.CultureInvariant)]
    private static partial Regex Call();

    [GeneratedRegex(@"\{(\d+)(:[^}]*)?\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();
}
