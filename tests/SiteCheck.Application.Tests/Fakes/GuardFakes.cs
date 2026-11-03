using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models.Guard;
using SiteCheck.Core.Models.Probes;

namespace SiteCheck.Application.Tests.Fakes;

/// <summary>Speicher im Arbeitsspeicher; eine zweite Wache über demselben Speicher spielt den Neustart.</summary>
internal sealed class InMemoryGuardStore : IRequestGuardStore
{
    private Dictionary<string, HostGuardEntry> _hosts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Wirft beim Zugriff, wie eine gesperrte Datei.</summary>
    public bool Fails { get; set; }

    public Task<IReadOnlyDictionary<string, HostGuardEntry>> LoadAsync(CancellationToken cancellationToken)
    {
        ThrowIfFailing();
        return Task.FromResult<IReadOnlyDictionary<string, HostGuardEntry>>(_hosts);
    }

    public Task SaveAsync(IReadOnlyDictionary<string, HostGuardEntry> hosts, CancellationToken cancellationToken)
    {
        ThrowIfFailing();
        _hosts = new Dictionary<string, HostGuardEntry>(hosts, StringComparer.OrdinalIgnoreCase);
        return Task.CompletedTask;
    }

    private void ThrowIfFailing()
    {
        if (Fails)
        {
            throw new IOException("Datei gesperrt.");
        }
    }
}

/// <summary>DNS, das jeden Namen auflöst oder, mit <c>resolves: false</c>, keinen.</summary>
internal sealed class StubDnsProbe(bool resolves = true) : IDnsProbe
{
    public Task<IReadOnlyList<string>> QueryMailExchangersAsync(string domain, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);

    public Task<IReadOnlyList<string>> QueryTextRecordsAsync(string name, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);

    public Task<IReadOnlyList<string>> ResolveIpv4Async(string host, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(resolves ? ["192.0.2.1"] : []);

    public Task<string?> QueryPointerAsync(string ipAddress, CancellationToken cancellationToken) => Task.FromResult<string?>(null);

    public Task<bool> IsDnssecSignedAsync(string domain, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task<IReadOnlyList<string>> QueryCaaAsync(string domain, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);

    public Task<bool> HasTlsaAsync(string name, CancellationToken cancellationToken) => Task.FromResult(false);
}

/// <summary>HTTP-Sonde, die jede Anfrage mitschreibt und nach einer Regel je Adresse antwortet.</summary>
internal sealed class RecordingHttpProbe(Func<Uri, int?> statusFor) : IHttpProbe
{
    public List<Uri> Requests { get; } = [];

    public Task<HttpProbeResponse?> FetchAsync(Uri address, CancellationToken cancellationToken) => Answer(address);

    public Task<HttpProbeResponse?> FetchWithoutRedirectAsync(Uri address, CancellationToken cancellationToken) => Answer(address);

    private Task<HttpProbeResponse?> Answer(Uri address)
    {
        Requests.Add(address);
        return Task.FromResult(statusFor(address) is int status
            ? new HttpProbeResponse(status, null, new Dictionary<string, string>(), [], string.Empty)
            : null);
    }
}

/// <summary>TLS-Sonde mit fester Antwort, die ihre Aufrufe zählt.</summary>
internal sealed class CountingTlsProbe(bool answers = true) : ITlsProbe
{
    public int Calls { get; private set; }

    public Task<TlsHandshakeResult?> HandshakeAsync(string host, int port, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(answers
            ? new TlsHandshakeResult(TlsVersion.Tls13, null, null, true, true)
            : null);
    }
}
