using System.Diagnostics.CodeAnalysis;

namespace SiteCheck.Core.Models;

/// <summary>
/// Eine geprüfte, vereinheitlichte Adresse. Entsteht nur über <see cref="TryParse"/>, damit
/// keine Prüfung mit einer halben Eingabe loslaufen kann.
/// <para>
/// Geprüft wird immer die https-Fassung. Wer <c>http://</c> oder gar kein Schema eingibt,
/// meint dieselbe Seite; ob http sauber umleitet, prüft die Umleitungsprüfung eigens.
/// </para>
/// </summary>
public sealed record CheckTarget
{
    private CheckTarget(Uri address)
    {
        Address = address;
    }

    /// <summary>Die https-Adresse ohne Abfrageteil und Sprungmarke.</summary>
    public Uri Address { get; }

    /// <summary>Rechnername in ASCII-Schreibweise, so wie DNS und TLS ihn brauchen (Umlaut-Domains als <c>xn--</c>).</summary>
    public string Host => Address.IdnHost;

    /// <summary>Ob statt eines Namens eine IP-Adresse eingegeben wurde. Dann gibt es keine Mail-Domain.</summary>
    public bool IsIpAddress => Address.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6;

    /// <summary>Die Domain, deren Mail-Einträge geprüft werden; bei einer IP-Adresse leer.</summary>
    public string MailDomain => IsIpAddress ? string.Empty : DomainName.GetRegistrableDomain(Host);

    /// <summary>Ob die Seite auf einer Subdomain läuft (<c>music.beispiel.de</c> statt <c>beispiel.de</c>).</summary>
    public bool IsSubdomain => !IsIpAddress && !string.Equals(Host, MailDomain, StringComparison.OrdinalIgnoreCase);

    /// <summary>Die Adresse als Zeichenkette, so wie sie gespeichert und angezeigt wird.</summary>
    public override string ToString() => Address.AbsoluteUri;

    /// <summary>Prüft eine Eingabe und macht daraus ein Prüfziel.</summary>
    /// <param name="input">Was eingegeben wurde, etwa <c>beispiel.de</c> oder <c>https://www.beispiel.de/start</c>.</param>
    /// <param name="target">Das Prüfziel, wenn die Eingabe taugt.</param>
    /// <param name="error">Der Grund, wenn sie nicht taugt; sonst <see cref="TargetParseError.None"/>.</param>
    /// <returns><see langword="true"/>, wenn ein Prüfziel entstanden ist.</returns>
    public static bool TryParse(string? input, [NotNullWhen(true)] out CheckTarget? target, out TargetParseError error)
    {
        target = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            error = TargetParseError.Empty;
            return false;
        }

        string trimmed = input.Trim();

        // Ohne Schema hielte Uri "beispiel.de" für einen relativen Pfad.
        string withScheme = trimmed.Contains("://", StringComparison.Ordinal) ? trimmed : "https://" + trimmed;
        if (!Uri.TryCreate(withScheme, UriKind.Absolute, out Uri? parsed))
        {
            error = TargetParseError.InvalidAddress;
            return false;
        }

        error = Validate(parsed);
        if (error != TargetParseError.None)
        {
            return false;
        }

        target = new CheckTarget(Normalize(parsed));
        return true;
    }

    private static TargetParseError Validate(Uri parsed)
    {
        if (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp)
        {
            return TargetParseError.UnsupportedScheme;
        }

        if (!string.IsNullOrEmpty(parsed.UserInfo))
        {
            return TargetParseError.ContainsCredentials;
        }

        // Ein Name ohne Punkt ("intranet", "localhost") ist keine öffentliche Seite, die man
        // von außen abklopfen könnte; eine IP-Adresse dagegen schon.
        bool isIp = parsed.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6;
        bool hasDot = parsed.Host.Trim('.').Contains('.', StringComparison.Ordinal);
        return isIp || hasDot ? TargetParseError.None : TargetParseError.InvalidAddress;
    }

    private static Uri Normalize(Uri parsed)
    {
        // Ein eigener Port bleibt nur erhalten, wenn er zu https gehört. Port 80 einer
        // http-Eingabe auf https zu übertragen, liefe ins Leere.
        bool keepPort = parsed.Scheme == Uri.UriSchemeHttps && !parsed.IsDefaultPort;
        UriBuilder builder = new(Uri.UriSchemeHttps, parsed.Host, keepPort ? parsed.Port : -1, parsed.AbsolutePath);
        return builder.Uri;
    }
}
