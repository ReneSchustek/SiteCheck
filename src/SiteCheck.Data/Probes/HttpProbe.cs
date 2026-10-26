using System.Net;
using System.Text;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models.Probes;

namespace SiteCheck.Data.Probes;

/// <summary>
/// Holt Kopfzeilen, Status, Cookies und den Anfang des Inhalts über gewöhnliche GET-Anfragen.
/// Rein beobachtend: Es wird nichts geschrieben und nichts angegriffen.
/// </summary>
public sealed class HttpProbe : IHttpProbe
{
    // Nur der Anfang des Inhalts wird gelesen. Das reicht, um eine offene .env von einer
    // Fehlerseite zu unterscheiden, und hält große Downloads (backup.zip) klein.
    private const int MaxBodyBytes = 8 * 1024;

    private readonly HttpClient _followingClient;
    private readonly HttpClient _nonFollowingClient;

    /// <summary>
    /// Erstellt die Sonde mit zwei Clients: einer folgt Umleitungen, der andere nicht. Beide
    /// führen keine Cookies mit, damit die <c>Set-Cookie</c>-Zeile der ersten Antwort sichtbar bleibt.
    /// </summary>
    /// <param name="followingClient">Client mit <c>AllowAutoRedirect = true</c>.</param>
    /// <param name="nonFollowingClient">Client mit <c>AllowAutoRedirect = false</c>.</param>
    public HttpProbe(HttpClient followingClient, HttpClient nonFollowingClient)
    {
        _followingClient = followingClient;
        _nonFollowingClient = nonFollowingClient;
    }

    /// <inheritdoc/>
    public Task<HttpProbeResponse?> FetchAsync(Uri address, CancellationToken cancellationToken) =>
        SendAsync(_followingClient, address, cancellationToken);

    /// <inheritdoc/>
    public Task<HttpProbeResponse?> FetchWithoutRedirectAsync(Uri address, CancellationToken cancellationToken) =>
        SendAsync(_nonFollowingClient, address, cancellationToken);

    private static async Task<HttpProbeResponse?> SendAsync(HttpClient client, Uri address, CancellationToken cancellationToken)
    {
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, address);
            using HttpResponseMessage response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            return await ToProbeResponseAsync(response, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Nur ein echter Abbruch durch den Aufrufer wird durchgereicht.
            throw;
        }
        catch (OperationCanceledException)
        {
            // Der HttpClient-Timeout wirft ebenfalls eine (Task)OperationCanceledException, obwohl
            // unser Token gar nicht abgebrochen wurde. Das ist „keine Antwort“, kein Abbruch.
            return null;
        }
        catch (HttpRequestException)
        {
            // Nicht erreichbar, Namensauflösung fehlgeschlagen, Verbindung abgewiesen: Für die
            // Prüfungen ist das schlicht „keine Antwort“, kein Programmfehler.
            return null;
        }
        catch (InvalidOperationException)
        {
            // Ungültige Adresse für diesen Client (etwa fehlendes Schema): wie keine Antwort behandeln.
            return null;
        }
    }

    private static async Task<HttpProbeResponse> ToProbeResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
        CollectHeaders(response.Headers, headers);
        CollectHeaders(response.Content.Headers, headers);

        IReadOnlyList<string> setCookies = response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? cookies)
            ? [.. cookies]
            : [];

        string bodyPrefix = await ReadBodyPrefixAsync(response.Content, cancellationToken).ConfigureAwait(false);

        return new HttpProbeResponse(
            (int)response.StatusCode,
            response.Headers.Location,
            headers,
            setCookies,
            bodyPrefix);
    }

    private static void CollectHeaders(System.Net.Http.Headers.HttpHeaders source, Dictionary<string, string> target)
    {
        foreach (KeyValuePair<string, IEnumerable<string>> header in source)
        {
            // Set-Cookie bleibt den Einzelzeilen vorbehalten; mit Komma verbunden wären die
            // Cookies nicht mehr auseinanderzuhalten (Ablaufdaten enthalten selbst Kommas).
            if (string.Equals(header.Key, "Set-Cookie", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            target[header.Key] = string.Join(", ", header.Value);
        }
    }

    private static async Task<string> ReadBodyPrefixAsync(HttpContent content, CancellationToken cancellationToken)
    {
        try
        {
            using Stream stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            byte[] buffer = new byte[MaxBodyBytes];
            int read = 0;
            while (read < buffer.Length)
            {
                int chunk = await stream
                    .ReadAsync(buffer.AsMemory(read, buffer.Length - read), cancellationToken)
                    .ConfigureAwait(false);
                if (chunk == 0)
                {
                    break;
                }

                read += chunk;
            }

            // Latin-1 bildet jedes Byte auf genau ein Zeichen ab; für die Signatursuche
            // ("<?php", "PK", "ref:") zählen die Bytes, nicht eine korrekte Textdekodierung.
            return Encoding.Latin1.GetString(buffer, 0, read);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException)
        {
            // Inhalt ließ sich nicht lesen (Lesefehler oder Timeout): Kopfzeilen und Status
            // reichen den meisten Prüfungen; der Rumpf bleibt leer.
            return string.Empty;
        }
    }
}
