using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models.Stress;

namespace SiteCheck.Application.Tests.Fakes;

/// <summary>
/// Attrappe für <see cref="ILoadProbe"/>, die jede Anfrage sofort mit festem Ergebnis
/// beantwortet und mitzählt. Achtet auf den Abbruch über das Token.
/// </summary>
internal sealed class FakeLoadProbe(int statusCode = 200, double elapsedMs = 5) : ILoadProbe
{
    private int _calls;

    /// <summary>Wie viele Anfragen abgeschickt wurden.</summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <inheritdoc/>
    public Task<RequestOutcome> SendAsync(Uri address, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = Interlocked.Increment(ref _calls);
        return Task.FromResult(new RequestOutcome(elapsedMs, statusCode));
    }
}
