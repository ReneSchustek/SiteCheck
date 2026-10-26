using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models.Probes;

namespace SiteCheck.Core.Tests.Fakes;

/// <summary>Attrappe für <see cref="ITlsProbe"/> mit einem festen Handschlag-Ergebnis.</summary>
internal sealed class FakeTlsProbe(TlsHandshakeResult? result) : ITlsProbe
{
    private readonly TlsHandshakeResult? _result = result;

    /// <inheritdoc/>
    public Task<TlsHandshakeResult?> HandshakeAsync(string host, int port, CancellationToken cancellationToken) =>
        Task.FromResult(_result);
}
