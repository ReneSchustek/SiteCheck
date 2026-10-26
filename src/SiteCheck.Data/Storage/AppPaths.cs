namespace SiteCheck.Data.Storage;

/// <summary>
/// Legt fest, wo das Programm seine Dateien ablegt: im Anwendungsdatenordner des Nutzers,
/// nicht im Installationsverzeichnis, damit auch eine schreibgeschützt installierte App
/// speichern kann.
/// </summary>
public sealed class AppPaths
{
    /// <summary>Erstellt die Pfade unter dem Standard-Anwendungsdatenordner (<c>%LocalAppData%\SiteCheck</c>).</summary>
    public AppPaths()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SiteCheck"))
    {
    }

    /// <summary>Erstellt die Pfade unter einem vorgegebenen Wurzelordner (für Tests).</summary>
    /// <param name="rootDirectory">Der Wurzelordner für alle Dateien.</param>
    public AppPaths(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = rootDirectory;
        HistoryDirectory = Path.Combine(rootDirectory, "history");
    }

    /// <summary>Wurzelordner für alle Dateien.</summary>
    public string RootDirectory { get; }

    /// <summary>Datei mit den Einstellungen.</summary>
    public string SettingsFile => Path.Combine(RootDirectory, "settings.json");

    /// <summary>Datei mit der Liste der eigenen Seiten.</summary>
    public string SitesFile => Path.Combine(RootDirectory, "sites.json");

    /// <summary>Ordner mit je einer Verlaufsdatei pro Seite.</summary>
    public string HistoryDirectory { get; }
}
