namespace SiteCheck.Core.Models.Probes;

/// <summary>Ergebnis eines TLS-Handschlags mit der Seite.</summary>
/// <param name="Version">Ausgehandelte Fassung; <see cref="TlsVersion.Unknown"/>, wenn das Zertifikat abgelehnt wurde.</param>
/// <param name="CertificateNotAfter">Ende der Gültigkeit des Serverzertifikats, falls eines kam.</param>
/// <param name="CertificateIssuer">Aussteller des Serverzertifikats, falls eines kam.</param>
/// <param name="IsChainTrusted">
/// Ob die Zertifikatskette zu einer anerkannten Stelle führt. Ein bloß abgelaufenes Zertifikat
/// zählt hier als vertrauenswürdig, denn den Ablauf meldet die Prüfung eigens und deutlicher.
/// </param>
/// <param name="IsNameMatch">Ob das Zertifikat für den geprüften Rechnernamen ausgestellt ist.</param>
public sealed record TlsHandshakeResult(
    TlsVersion Version,
    DateTimeOffset? CertificateNotAfter,
    string? CertificateIssuer,
    bool IsChainTrusted,
    bool IsNameMatch);
