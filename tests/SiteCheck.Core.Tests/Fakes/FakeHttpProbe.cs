using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models.Probes;

namespace SiteCheck.Core.Tests.Fakes;

/// <summary>
/// Attrappe für <see cref="IHttpProbe"/> mit festen Antworten je Adresse. Ohne hinterlegte
/// Antwort gilt die Adresse als nicht erreichbar (<see langword="null"/>), wie beim echten Client.
/// </summary>
internal sealed class FakeHttpProbe : IHttpProbe
{
    private readonly Dictionary<string, HttpProbeResponse> _following = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HttpProbeResponse> _nonFollowing = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Hinterlegt die Antwort auf eine Anfrage mit Folgen der Umleitung.</summary>
    public void SetResponse(string address, HttpProbeResponse response) => _following[address] = response;

    /// <summary>Hinterlegt die Antwort auf eine Anfrage ohne Folgen der Umleitung.</summary>
    public void SetNonRedirectResponse(string address, HttpProbeResponse response) => _nonFollowing[address] = response;

    /// <inheritdoc/>
    public Task<HttpProbeResponse?> FetchAsync(Uri address, CancellationToken cancellationToken) =>
        Task.FromResult(_following.GetValueOrDefault(address.AbsoluteUri));

    /// <inheritdoc/>
    public Task<HttpProbeResponse?> FetchWithoutRedirectAsync(Uri address, CancellationToken cancellationToken) =>
        Task.FromResult(_nonFollowing.GetValueOrDefault(address.AbsoluteUri));

    /// <summary>Baut eine Antwort mit Statuscode und beliebigen Kopfzeilen.</summary>
    public static HttpProbeResponse Response(
        int statusCode,
        Uri? location = null,
        IReadOnlyDictionary<string, string>? headers = null,
        IReadOnlyList<string>? setCookies = null,
        string body = "")
    {
        Dictionary<string, string> headerMap = new(StringComparer.OrdinalIgnoreCase);
        if (headers is not null)
        {
            foreach (KeyValuePair<string, string> header in headers)
            {
                headerMap[header.Key] = header.Value;
            }
        }

        return new HttpProbeResponse(statusCode, location, headerMap, setCookies ?? [], body);
    }
}
