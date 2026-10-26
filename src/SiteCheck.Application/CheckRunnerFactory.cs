using SiteCheck.Core.Abstractions;

namespace SiteCheck.Application;

/// <summary>
/// Baut je Prüflauf einen frischen <see cref="CheckRunner"/> über einen eigenen HTTP-Cache. So
/// teilen sich die Prüfungen eines Laufs ihre Seiten-Anfragen, während ein späterer Lauf die
/// Seite erneut abfragt (frischer Cache).
/// </summary>
public sealed class CheckRunnerFactory(IHttpProbe http, ITlsProbe tls, IDnsProbe dns, TimeProvider clock)
    : ICheckRunnerFactory
{
    private readonly IHttpProbe _http = http;
    private readonly ITlsProbe _tls = tls;
    private readonly IDnsProbe _dns = dns;
    private readonly TimeProvider _clock = clock;

    /// <inheritdoc/>
    public CheckRunner Create()
    {
        CachingHttpProbe scopedHttp = new(_http);
        return new CheckRunner(CheckCatalog.CreateDefault(scopedHttp, _tls, _dns, _clock));
    }
}
