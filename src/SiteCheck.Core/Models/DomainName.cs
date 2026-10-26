namespace SiteCheck.Core.Models;

/// <summary>
/// Leitet aus einem Rechnernamen die Domain ab, unter der Mail-Einträge (MX, SPF, DMARC)
/// gesucht werden: aus <c>music.vreka-music.com</c> wird <c>vreka-music.com</c>.
/// <para>
/// Die vollständige Public Suffix List wäre genauer, bräuchte aber ein Paket oder eine
/// gepflegte Datei mit rund 10.000 Einträgen. Für eigene Seiten genügen die letzten zwei
/// Teile plus eine kurze Liste zweistufiger Länderendungen, unter denen Kunden tatsächlich
/// registrieren. Fehlt eine Endung, wird eine Ebene zu wenig abgeschnitten; die Prüfung
/// fragt dann die Endung selbst ab und meldet fehlende Einträge, statt falsche zu bestätigen.
/// </para>
/// </summary>
public static class DomainName
{
    private static readonly HashSet<string> TwoLevelSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "co.uk", "org.uk", "ac.uk", "gov.uk", "me.uk",
        "co.at", "or.at",
        "com.au", "net.au", "org.au",
        "co.nz", "co.za", "co.jp", "com.br", "com.tr", "com.cn",
    };

    /// <summary>Liefert die registrierbare Domain zu einem Rechnernamen.</summary>
    /// <param name="host">Rechnername ohne Schema und Port, etwa <c>www.beispiel.de</c>.</param>
    /// <returns>Die Domain, etwa <c>beispiel.de</c>; ein Name mit höchstens zwei Teilen kommt unverändert zurück.</returns>
    public static string GetRegistrableDomain(string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        string[] labels = host.TrimEnd('.').Split('.');
        if (labels.Length <= 2)
        {
            return string.Join('.', labels);
        }

        string lastTwo = string.Join('.', labels[^2..]);
        int keep = TwoLevelSuffixes.Contains(lastTwo) ? 3 : 2;
        return string.Join('.', labels[^keep..]);
    }
}
