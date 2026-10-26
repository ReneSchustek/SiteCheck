namespace SiteCheck.Core.Models.Probes;

/// <summary>
/// Was eine einzelne HTTP-Anfrage zurückbrachte, auf das reduziert, was die Prüfungen brauchen.
/// </summary>
/// <param name="StatusCode">HTTP-Statuscode der (letzten) Antwort.</param>
/// <param name="Location">Ziel einer Umleitung, sofern die Antwort eine nennt.</param>
/// <param name="Headers">Kopfzeilen ohne Groß-/Kleinschreibung im Namen; mehrfache Werte mit Komma verbunden.</param>
/// <param name="SetCookieHeaders">Jede <c>Set-Cookie</c>-Zeile einzeln. Sie lassen sich nicht mit Komma verbinden, weil Ablaufdaten selbst Kommas enthalten.</param>
/// <param name="BodyPrefix">
/// Der Anfang des Inhalts, höchstens wenige Kilobyte, Byte für Byte als Latin-1 gelesen. Reicht,
/// um eine offene <c>.env</c> von einer Fehlerseite zu unterscheiden, ohne große Dateien zu laden.
/// </param>
public sealed record HttpProbeResponse(
    int StatusCode,
    Uri? Location,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyList<string> SetCookieHeaders,
    string BodyPrefix)
{
    /// <summary>Liefert den Wert einer Kopfzeile oder <see langword="null"/>, wenn sie fehlt.</summary>
    /// <param name="name">Name der Kopfzeile.</param>
    /// <returns>Der Wert oder <see langword="null"/>.</returns>
    public string? GetHeader(string name) => Headers.TryGetValue(name, out string? value) ? value : null;
}
