namespace SiteCheck.Core.Models.Probes;

/// <summary>Ausgehandelte TLS-Fassung, auf die Unterscheidung reduziert, auf die es ankommt.</summary>
public enum TlsVersion
{
    /// <summary>Kein Handschlag zustande gekommen, etwa weil das Zertifikat abgelehnt wurde.</summary>
    Unknown = 0,

    /// <summary>TLS 1.0, 1.1 oder älter. Seit RFC 8996 (2021) nicht mehr zulässig.</summary>
    Outdated = 1,

    /// <summary>TLS 1.2.</summary>
    Tls12 = 2,

    /// <summary>TLS 1.3.</summary>
    Tls13 = 3,
}
