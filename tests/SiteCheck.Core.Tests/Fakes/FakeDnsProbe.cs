using SiteCheck.Core.Abstractions;

namespace SiteCheck.Core.Tests.Fakes;

/// <summary>Attrappe für <see cref="IDnsProbe"/> mit festen DNS-Antworten je Name.</summary>
internal sealed class FakeDnsProbe : IDnsProbe
{
    private readonly Dictionary<string, IReadOnlyList<string>> _mx = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<string>> _txt = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<string>> _ipv4 = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _pointers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _dnssec = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<string>> _caa = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _tlsa = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Hinterlegt die MX-Austauscher einer Domain.</summary>
    public FakeDnsProbe SetMx(string domain, params string[] exchangers)
    {
        _mx[domain] = exchangers;
        return this;
    }

    /// <summary>Hinterlegt die TXT-Einträge eines Namens.</summary>
    public FakeDnsProbe SetTxt(string name, params string[] records)
    {
        _txt[name] = records;
        return this;
    }

    /// <summary>Hinterlegt die IPv4-Adressen eines Hosts.</summary>
    public FakeDnsProbe SetIpv4(string host, params string[] addresses)
    {
        _ipv4[host] = addresses;
        return this;
    }

    /// <summary>Hinterlegt den PTR-Namen einer IP-Adresse.</summary>
    public FakeDnsProbe SetPointer(string ipAddress, string pointer)
    {
        _pointers[ipAddress] = pointer;
        return this;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> QueryMailExchangersAsync(string domain, CancellationToken cancellationToken) =>
        Task.FromResult(_mx.GetValueOrDefault(domain, []));

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> QueryTextRecordsAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(_txt.GetValueOrDefault(name, []));

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> ResolveIpv4Async(string host, CancellationToken cancellationToken) =>
        Task.FromResult(_ipv4.GetValueOrDefault(host, []));

    /// <summary>Markiert eine Domain als DNSSEC-signiert.</summary>
    public FakeDnsProbe SetDnssec(string domain)
    {
        _ = _dnssec.Add(domain);
        return this;
    }

    /// <summary>Hinterlegt CAA-Einträge einer Domain.</summary>
    public FakeDnsProbe SetCaa(string domain, params string[] records)
    {
        _caa[domain] = records;
        return this;
    }

    /// <summary>Markiert einen Namen als TLSA-tragend.</summary>
    public FakeDnsProbe SetTlsa(string name)
    {
        _ = _tlsa.Add(name);
        return this;
    }

    /// <inheritdoc/>
    public Task<string?> QueryPointerAsync(string ipAddress, CancellationToken cancellationToken) =>
        Task.FromResult(_pointers.GetValueOrDefault(ipAddress));

    /// <inheritdoc/>
    public Task<bool> IsDnssecSignedAsync(string domain, CancellationToken cancellationToken) =>
        Task.FromResult(_dnssec.Contains(domain));

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> QueryCaaAsync(string domain, CancellationToken cancellationToken) =>
        Task.FromResult(_caa.GetValueOrDefault(domain, []));

    /// <inheritdoc/>
    public Task<bool> HasTlsaAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(_tlsa.Contains(name));
}
