using SiteCheck.Core.Abstractions;

namespace SiteCheck.Application;

/// <summary>
/// Baut je Prüflauf einen frischen <see cref="CheckRunner"/> über einen eigenen HTTP-Cache. So
/// teilen sich die Prüfungen eines Laufs ihre Seiten-Anfragen, während ein späterer Lauf die
/// Seite erneut abfragt (frischer Cache). Alle Anfragen laufen über die Sperrwache des Prozesses.
/// </summary>
public sealed class CheckRunnerFactory(IHttpProbe http, ITlsProbe tls, IDnsProbe dns, TimeProvider clock, HostRequestGuard guard)
    : ICheckRunnerFactory
{
    private readonly IHttpProbe _http = new GuardedHttpProbe(http, guard);
    private readonly ITlsProbe _tls = new GuardedTlsProbe(tls, guard);
    private readonly IDnsProbe _dns = dns;
    private readonly TimeProvider _clock = clock;

    /// <inheritdoc/>
    public CheckRunner Create(CheckRunOptions options)
    {
        CachingHttpProbe scopedHttp = new(_http);
        return new CheckRunner(CheckCatalog.CreateDefault(scopedHttp, _tls, _dns, _clock, options));
    }
}
