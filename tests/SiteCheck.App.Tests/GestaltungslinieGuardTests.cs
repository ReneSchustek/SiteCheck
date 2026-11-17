using System.Text.RegularExpressions;

namespace SiteCheck.App.Tests;

/// <summary>
/// Wächter der Gestaltungslinie: In den Ansichten (Views, Hauptfenster) darf kein fester
/// Farbwert stehen. Farben kommen aus der Palette über <c>ThemeResource</c>. Ausgenommen ist
/// der Startbildschirm, der vor dem Laden der Paletten erscheint und deshalb feste Markenfarben
/// tragen muss. Geht der Test rot, hat sich eine Farbe in eine Ansicht geschlichen.
/// Dazu die Schrift: Was die Palette nennt, muss als Datei im Programm liegen, sonst fällt die
/// Oberfläche beim Nutzer still auf die Ersatzschrift zurück.
/// </summary>
public sealed partial class GestaltungslinieGuardTests
{
    private static readonly string[] FontKeys = ["ThemeFontFamily", "ThemeMonoFontFamily", "ContentControlThemeFontFamily"];

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
