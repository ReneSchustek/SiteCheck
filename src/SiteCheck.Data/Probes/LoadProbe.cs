using System.Diagnostics;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models.Stress;

namespace SiteCheck.Data.Probes;

/// <summary>
/// Schickt eine einzelne GET-Anfrage und misst die Dauer. Fehler und Zeitüberschreitungen
/// werden nicht geworfen, sondern als Ergebnis ohne Statuscode zurückgegeben; der Lasttest
/// soll gerade sehen, wie viele Anfragen unter Last scheitern.
/// </summary>
public sealed class LoadProbe(HttpClient client) : ILoadProbe
{
    private readonly HttpClient _client = client;

    /// <inheritdoc/>
    public async Task<RequestOutcome> SendAsync(Uri address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);

        long startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, address);
            using HttpResponseMessage response = await _client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            return new RequestOutcome(Elapsed(startTimestamp), (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Abbruch durch Nutzer oder Zeitlimit: nach oben, zählt nicht als Fehlschlag.
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            // Verbindungsfehler oder Zeitüberschreitung der einzelnen Anfrage: als Fehlschlag
            // ohne Statuscode werten. Genau das will der Lasttest sichtbar machen.
            return new RequestOutcome(Elapsed(startTimestamp), null);
        }
    }

    private static double Elapsed(long startTimestamp) =>
        Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
}
