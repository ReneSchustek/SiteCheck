using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace SiteCheck.Data.Storage;

/// <summary>
/// Liest und schreibt eine JSON-Datei. Geschrieben wird erst in eine Nebendatei und dann per
/// Umbenennen an die Stelle gesetzt: So steht nie eine halb geschriebene Datei da, wenn der
/// Schreibvorgang mittendrin abbricht.
/// </summary>
internal static class JsonFile
{
    /// <summary>
    /// Liest und deserialisiert eine Datei. Fehlt sie oder ist sie beschädigt, wird der
    /// Vorgabewert zurückgegeben; eine kaputte Datei darf das Programm nicht am Start hindern.
    /// </summary>
    /// <typeparam name="T">Der Zieltyp.</typeparam>
    /// <param name="path">Pfad der Datei.</param>
    /// <param name="typeInfo">Die quellgenerierte Typinformation.</param>
    /// <param name="fallback">Rückfallwert bei fehlender oder beschädigter Datei.</param>
    /// <param name="cancellationToken">Bricht den Zugriff ab.</param>
    /// <returns>Der gelesene Wert oder der Rückfallwert.</returns>
    public static async Task<T> ReadAsync<T>(string path, JsonTypeInfo<T> typeInfo, T fallback, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return fallback;
        }

        try
        {
            // Bewusst synchrones using statt await using: Der FileStream bietet keine
            // ConfigureAwait-fähige asynchrone Freigabe, und das synchrone Schließen einer
            // kleinen Konfigurationsdatei ist unkritisch. So bleibt CA2007 erfüllt.
            using FileStream stream = File.OpenRead(path);
            T? value = await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken).ConfigureAwait(false);
            return value ?? fallback;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Beschädigt oder nicht lesbar: mit dem Vorgabewert weiterarbeiten, nicht abstürzen.
            return fallback;
        }
    }

    /// <summary>Serialisiert einen Wert und schreibt ihn atomar an die angegebene Stelle.</summary>
    /// <typeparam name="T">Der Werttyp.</typeparam>
    /// <param name="path">Pfad der Zieldatei.</param>
    /// <param name="value">Der zu schreibende Wert.</param>
    /// <param name="typeInfo">Die quellgenerierte Typinformation.</param>
    /// <param name="cancellationToken">Bricht den Zugriff ab.</param>
    public static async Task WriteAsync<T>(string path, T value, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        // Eindeutige Nebendatei je Schreibvorgang, damit zwei gleichzeitige Schreibvorgänge
        // sich nicht dieselbe temporäre Datei teilen.
        string tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, value, typeInfo, cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
