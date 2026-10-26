namespace SiteCheck.Core.Models;

/// <summary>Was der <c>all</c>-Mechanismus am Ende eines SPF-Eintrags sagt.</summary>
public enum SpfAllQualifier
{
    /// <summary>Kein <c>all</c> vorhanden.</summary>
    None = 0,

    /// <summary><c>-all</c>: hartes Ablehnen. Nur die genannten Absender dürfen senden.</summary>
    Fail = 1,

    /// <summary><c>~all</c>: weiches Ablehnen (SoftFail). Üblich beim Einführen.</summary>
    SoftFail = 2,

    /// <summary><c>?all</c>: neutral, bewertet nichts.</summary>
    Neutral = 3,

    /// <summary><c>+all</c>: erlaubt jedem das Senden — hebt den Schutz auf.</summary>
    Pass = 4,
}

/// <summary>
/// Ein eingelesener SPF-Eintrag. Reicht für die Außensicht: die erlaubten Absender (als Hosts,
/// IPs und <c>include</c>-Anbieter) und der Abschluss-Qualifier. SPF wird nicht vollständig
/// rekursiv aufgelöst — das täte ein echter Empfänger, der Außen-Check nennt die Anbieter beim Namen.
/// </summary>
/// <param name="Raw">Der unveränderte TXT-Wert.</param>
/// <param name="AllQualifier">Der Abschluss-Mechanismus.</param>
/// <param name="IncludedDomains">Die <c>include:</c>-Anbieter (dort liegt die maßgebliche Einrichtung).</param>
/// <param name="DirectHosts">Hosts aus <c>a</c>/<c>mx</c>-Mechanismen, die zu IPs aufzulösen sind.</param>
/// <param name="DirectIps">Einzelne IPv4-Hosts aus <c>ip4</c> (ohne Maske oder <c>/32</c>); Bereiche und IPv6 bleiben außen vor.</param>
/// <param name="DnsLookupMechanismCount">Zahl der Mechanismen, die DNS-Abfragen auslösen (RFC-7208-Grenze: 10).</param>
public sealed record SpfRecord(
    string Raw,
    SpfAllQualifier AllQualifier,
    IReadOnlyList<string> IncludedDomains,
    IReadOnlyList<string> DirectHosts,
    IReadOnlyList<string> DirectIps,
    int DnsLookupMechanismCount)
{
    /// <summary>Liest einen SPF-TXT-Wert ein.</summary>
    /// <param name="raw">Der TXT-Wert, beginnend mit <c>v=spf1</c>.</param>
    /// <param name="domain">Die Domain; sie ist das Ziel eines bloßen <c>a</c>/<c>mx</c> ohne Namen.</param>
    /// <returns>Der eingelesene Eintrag.</returns>
    public static SpfRecord Parse(string raw, string domain)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        SpfAllQualifier all = SpfAllQualifier.None;
        List<string> includes = [];
        List<string> hosts = [];
        List<string> ips = [];
        int dnsLookups = 0;

        foreach (string token in raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.StartsWith("v=spf1", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Der führende Qualifier (+ - ~ ?) gehört zur Bewertung, nicht zum Namen.
            char qualifier = token[0] is '+' or '-' or '~' or '?' ? token[0] : '+';
            string body = token[0] is '+' or '-' or '~' or '?' ? token[1..] : token;

            if (body.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                all = qualifier switch
                {
                    '-' => SpfAllQualifier.Fail,
                    '~' => SpfAllQualifier.SoftFail,
                    '?' => SpfAllQualifier.Neutral,
                    _ => SpfAllQualifier.Pass,
                };
            }
            else if (body.StartsWith("include:", StringComparison.OrdinalIgnoreCase))
            {
                includes.Add(body["include:".Length..]);
                dnsLookups++;
            }
            else if (body.StartsWith("ip4:", StringComparison.OrdinalIgnoreCase))
            {
                // Nur Einzel-Hosts aufnehmen. Ein Bereich wie ip4:37.17.224.0/21 nennt keine
                // einzelne Maschine; seine Netzadresse hat nie einen PTR, eine Rückwärtsauflösung
                // darauf wäre bedeutungslos. /32 ist ein einzelner Host.
                string value = body[4..];
                string[] parts = value.Split('/');
                if (parts.Length == 1 || parts[1] == "32")
                {
                    ips.Add(parts[0]);
                }
            }
            // ip6 wird für die (IPv4-)Rückwärtsauflösung nicht herangezogen.
            else if (body.Equals("a", StringComparison.OrdinalIgnoreCase) || body.Equals("mx", StringComparison.OrdinalIgnoreCase))
            {
                hosts.Add(domain);
                dnsLookups++;
            }
            else if (body.StartsWith("a:", StringComparison.OrdinalIgnoreCase) || body.StartsWith("mx:", StringComparison.OrdinalIgnoreCase))
            {
                hosts.Add(body[(body.IndexOf(':', StringComparison.Ordinal) + 1)..].Split('/')[0]);
                dnsLookups++;
            }
            else if (body.StartsWith("redirect=", StringComparison.OrdinalIgnoreCase)
                || body.StartsWith("exists:", StringComparison.OrdinalIgnoreCase)
                || body.StartsWith("ptr", StringComparison.OrdinalIgnoreCase))
            {
                // Zählt zur DNS-Lookup-Grenze, nennt aber keinen direkt prüfbaren Host.
                dnsLookups++;
            }
        }

        return new SpfRecord(raw, all, includes, hosts, ips, dnsLookups);
    }
}
