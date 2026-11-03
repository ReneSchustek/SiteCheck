using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SiteCheck.App.Tests.Localization;

/// <summary>
/// Hält die Oberflächentexte vollständig und beide Sprachen deckungsgleich. Ein fehlender Schlüssel
/// fiele sonst erst in einer Ansicht auf, die vielleicht selten geöffnet wird.
/// </summary>
public sealed partial class ResourcesReswCompletenessTests
{
    private static readonly string Root = FindRepositoryRoot();

    private static string AppDirectory => Path.Combine(Root, "src", "SiteCheck.App");

    [Theory]
    [InlineData("de")]
    [InlineData("en-US")]
    public void ReswFile_HasBomAndUniqueFilledKeysInOrdinalOrder(string culture)
    {
        byte[] bytes = File.ReadAllBytes(ReswPath(culture));
        Assert.True(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble), "Visual Studio schreibt .resw mit BOM.");

        List<KeyValuePair<string, string>> entries = Read(culture);
        string[] keys = [.. entries.Select(entry => entry.Key)];

        Assert.Equal(keys.Distinct(StringComparer.Ordinal).Count(), keys.Length);
        Assert.Equal(keys.Order(StringComparer.Ordinal), keys);
        Assert.DoesNotContain(entries, entry => string.IsNullOrWhiteSpace(entry.Value));
        Assert.All(keys, key => Assert.Matches(KeyPattern(), key));
    }

    [Fact]
    public void BothCultures_ShareKeysAndPlaceholders()
    {
        List<KeyValuePair<string, string>> german = Read("de");
        Dictionary<string, string> english = Read("en-US").ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

        Assert.Equal(german.Select(entry => entry.Key), Read("en-US").Select(entry => entry.Key));
        foreach ((string key, string value) in german)
        {
            Assert.Equal(Placeholders(value), Placeholders(english[key]));
        }
    }

    [Fact]
    public void EveryKeyUsedInCode_Exists()
    {
        HashSet<string> keys = [.. Read("de").Select(entry => entry.Key)];
        string[] sources = Directory.GetFiles(AppDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .ToArray();

        List<string> missing = [];
        foreach (string source in sources)
        {
            foreach (Match match in KeyInCode().Matches(File.ReadAllText(source)))
            {
                if (!keys.Contains(match.Groups[1].Value))
                {
                    missing.Add(match.Groups[1].Value);
                }
            }
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryUidInMarkup_HasText_AndEveryMarkupKeyIsUsed()
    {
        string[] markupKeys = [.. Read("de").Select(entry => entry.Key).Where(key => key.Contains('.', StringComparison.Ordinal))];
        HashSet<string> uids = new(StringComparer.Ordinal);
        foreach (string file in Directory.GetFiles(AppDirectory, "*.xaml", SearchOption.AllDirectories).Where(path => !IsBuildOutput(path)))
        {
            foreach (Match match in UidInMarkup().Matches(File.ReadAllText(file)))
            {
                _ = uids.Add(match.Groups[1].Value);
            }
        }

        Assert.All(uids, uid => Assert.Contains(markupKeys, key => key.StartsWith(uid + ".", StringComparison.Ordinal)));
        Assert.All(markupKeys, key => Assert.Contains(key[..key.IndexOf('.', StringComparison.Ordinal)], uids));
    }

    private static List<KeyValuePair<string, string>> Read(string culture) =>
        XDocument.Load(ReswPath(culture))
            .Root!.Elements("data") // Wurzel ist da: XDocument.Load wirft sonst schon beim Lesen.
            .Select(data => new KeyValuePair<string, string>(data.Attribute("name")!.Value, data.Element("value")!.Value))
            .ToList();

    private static string ReswPath(string culture) => Path.Combine(AppDirectory, "Strings", culture, "Resources.resw");

    private static string[] Placeholders(string value) =>
        [.. PlaceholderPattern().Matches(value).Select(match => match.Value).Order(StringComparer.Ordinal)];

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SiteCheck.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("SiteCheck.slnx nicht gefunden.");
    }

    [GeneratedRegex(@"^[A-Z][A-Za-z0-9]*(_[A-Za-z0-9]+)+(\.[A-Z][A-Za-z]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();

    [GeneratedRegex(@"\{\d+(:[^}]*)?\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"\b(?:GetString|GetFormatted)\(\s*""([A-Za-z0-9_]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex KeyInCode();

    [GeneratedRegex(@"x:Uid=""([^""]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex UidInMarkup();
}
