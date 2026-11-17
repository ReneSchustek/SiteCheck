using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SiteCheck.App.Tests;

/// <summary>
/// Wächter der Gestaltungslinie: In den Ansichten (Views, Hauptfenster) darf kein fester
/// Farbwert stehen. Farben kommen aus der Palette über <c>ThemeResource</c>. Ausgenommen ist
/// der Startbildschirm, der vor dem Laden der Paletten erscheint und deshalb feste Markenfarben
/// tragen muss. Geht der Test rot, hat sich eine Farbe in eine Ansicht geschlichen.
/// Dazu die Schrift: Was die Palette nennt, muss als Datei im Programm liegen, sonst fällt die
/// Oberfläche beim Nutzer still auf die Ersatzschrift zurück. Und der Akzent: Fehlt eine Stufe,
/// setzt Windows dort die Akzentfarbe des Nutzers ein.
/// </summary>
public sealed partial class GestaltungslinieGuardTests
{
    private static readonly string[] FontKeys = ["ThemeFontFamily", "ThemeMonoFontFamily", "ContentControlThemeFontFamily"];

    private static readonly string[] AccentKeys =
    [
        "SystemAccentColor",
        "SystemAccentColorLight1",
        "SystemAccentColorLight2",
        "SystemAccentColorLight3",
        "SystemAccentColorDark1",
        "SystemAccentColorDark2",
        "SystemAccentColorDark3",
        "TextOnAccentFillColorPrimary",
    ];

    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Palette_DefinesEveryAccentStep(string theme)
    {
        Dictionary<string, string> values = ReadThemeColors(theme);

        string[] missing = AccentKeys.Where(key => !values.ContainsKey(key)).ToArray();
        Assert.True(missing.Length == 0, $"In der Belegung {theme} fehlen Akzentstufen: {string.Join(", ", missing)}");
    }

    // Die Stufe, aus der WinUI die Akzentflächen baut, ist im Hellen Dark1, im Dunklen Light2.
    // Sie muss primary sein und der Text darauf on-primary, sonst trägt die Hauptaktion eine
    // andere Farbe als der Rest der Linie.
    [Theory]
    [InlineData("Light", "SystemAccentColorDark1")]
    [InlineData("Dark", "SystemAccentColorLight2")]
    public void Palette_AccentFill_IsPrimaryWithOnPrimaryText(string theme, string fillKey)
    {
        Dictionary<string, string> values = ReadThemeColors(theme);

        Assert.Equal(values["PrimaryBrush"], values[fillKey], ignoreCase: true);
        Assert.Equal(values["OnPrimaryBrush"], values["TextOnAccentFillColorPrimary"], ignoreCase: true);
    }

    // Ein eigener Pinsel unter diesem Schlüssel ließ das Fenster schwarz; die Farbe gehört in den
    // Color-Schlüssel, aus dem WinUI den Pinsel selbst baut.
    [Fact]
    public void Palette_DoesNotReplaceTheAccentTextBrush()
    {
        string palette = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "SiteCheck.App", "Themes", "Palette.xaml"));

        Assert.DoesNotContain("x:Key=\"TextOnAccentFillColorPrimaryBrush\"", palette, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"#[0-9A-Fa-f]{3,8}\b")]
    private static partial Regex HexColor();

    [GeneratedRegex(@"ms-appx:///Assets/Fonts/(?<file>[^#<]+)#")]
    private static partial Regex ShippedFontReference();

    [Fact]
    public void Palette_FontKeys_PointToShippedFiles()
    {
        string appDirectory = Path.Combine(FindRepositoryRoot(), "src", "SiteCheck.App");
        string palette = File.ReadAllText(Path.Combine(appDirectory, "Themes", "Palette.xaml"));

        foreach (string key in FontKeys)
        {
            Match entry = Regex.Match(palette, $"x:Key=\"{key}\">(?<value>[^<]+)<");
            Assert.True(entry.Success, $"Schlüssel {key} fehlt in Palette.xaml.");

            Match reference = ShippedFontReference().Match(entry.Groups["value"].Value);
            Assert.True(reference.Success, $"{key} nennt keine mitgelieferte Schriftdatei.");

            string file = reference.Groups["file"].Value;
            Assert.True(
                File.Exists(Path.Combine(appDirectory, "Assets", "Fonts", file)),
                $"{key} verweist auf {file}, die Datei fehlt.");
        }
    }

    [Fact]
    public void ShippedFonts_AreCopiedToOutputAndCarryTheirLicense()
    {
        string appDirectory = Path.Combine(FindRepositoryRoot(), "src", "SiteCheck.App");
        string fontDirectory = Path.Combine(appDirectory, "Assets", "Fonts");
        string project = File.ReadAllText(Path.Combine(appDirectory, "SiteCheck.App.csproj"));
        string[] fontFiles = Directory.GetFiles(fontDirectory, "*.ttf");

        Assert.NotEmpty(fontFiles);
        foreach (string file in Directory.GetFiles(fontDirectory))
        {
            string include = $"<Content Include=\"Assets\\Fonts\\{Path.GetFileName(file)}\" CopyToOutputDirectory=\"PreserveNewest\" />";
            Assert.Contains(include, project, StringComparison.Ordinal);
        }

        // Die Lizenz folgt dem Dateinamen: roboto-flex-variable.ttf gehört zu OFL-roboto-flex.txt.
        foreach (string font in fontFiles)
        {
            string family = Path.GetFileNameWithoutExtension(font).Replace("-variable", string.Empty, StringComparison.Ordinal);
            string license = Path.Combine(fontDirectory, $"OFL-{family}.txt");
            Assert.True(File.Exists(license), $"Zu {Path.GetFileName(font)} fehlt der Lizenztext OFL-{family}.txt.");
            Assert.Contains("SIL Open Font License, Version 1.1", File.ReadAllText(license), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Views_ContainNoHardcodedHexColors()
    {
        string appDirectory = Path.Combine(FindRepositoryRoot(), "src", "SiteCheck.App");
        string[] viewFiles =
        [
            .. Directory.GetFiles(Path.Combine(appDirectory, "Views"), "*.xaml"),
            Path.Combine(appDirectory, "MainWindow.xaml"),
        ];

        List<string> offenders = [];
        foreach (string file in viewFiles)
        {
            string content = File.ReadAllText(file);
            if (HexColor().IsMatch(content))
            {
                offenders.Add(Path.GetFileName(file));
            }
        }

        Assert.True(offenders.Count == 0, "Feste Farbwerte in Ansichten gefunden: " + string.Join(", ", offenders));
    }

    // Schlüssel → Farbwert einer Belegung; Color-Elemente tragen den Wert als Inhalt,
    // SolidColorBrush-Elemente im Attribut Color.
    private static Dictionary<string, string> ReadThemeColors(string theme)
    {
        string palettePath = Path.Combine(FindRepositoryRoot(), "src", "SiteCheck.App", "Themes", "Palette.xaml");
        XElement themeDictionary = XDocument.Load(palettePath)
            .Descendants()
            .Single(element => element.Name.LocalName == "ResourceDictionary"
                && (string?)element.Attribute(XamlNamespace + "Key") == theme);

        Dictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (XElement entry in themeDictionary.Elements())
        {
            string? key = (string?)entry.Attribute(XamlNamespace + "Key");
            string? value = entry.Name.LocalName == "Color" ? entry.Value.Trim() : (string?)entry.Attribute("Color");
            if (key is not null && value is not null)
            {
                values[key] = value;
            }
        }

        return values;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SiteCheck.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
