using System.Text.RegularExpressions;

namespace SiteCheck.App.Tests;

/// <summary>
/// Wächter der Gestaltungslinie: In den Ansichten (Views, Hauptfenster) darf kein fester
/// Farbwert stehen. Farben kommen aus der Palette über <c>ThemeResource</c>. Ausgenommen ist
/// der Startbildschirm, der vor dem Laden der Paletten erscheint und deshalb feste Markenfarben
/// tragen muss. Geht der Test rot, hat sich eine Farbe in eine Ansicht geschlichen.
/// </summary>
public sealed partial class GestaltungslinieGuardTests
{
    [GeneratedRegex(@"#[0-9A-Fa-f]{3,8}\b")]
    private static partial Regex HexColor();

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
