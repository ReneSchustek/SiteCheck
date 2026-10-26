using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;

namespace SiteCheck.Core.Checks;

/// <summary>
/// Sieht sich die Mail-Einrichtung der Domain an: MX, SPF, DKIM, DMARC sowie — wenn vorhanden —
/// MTA-STS, TLS-RPT und BIMI. Der häufigste Grund für nicht ankommende Bestätigungsmails steckt
/// hier. Nur DNS-Abfragen, kein Versand.
/// <para>
/// Die Prüfung sagt nur, was sie von außen wirklich sieht. Sie behauptet keine Ursache, die sie
/// nur vermutet: Ein fehlender MX ist kein Versand-Blocker, und die Rückwärtsauflösung wird für
/// die aus dem SPF abgeleiteten Versandhosts geprüft, nicht für die Web-Adresse (Lehre aus dem
/// ersten Bericht zur Musik-Seite). Vermutungen tragen <see cref="Confidence.Inferred"/>.
/// </para>
/// </summary>
public sealed class MailSetupCheck(IDnsProbe dns) : ICheck
{
    // Gängige Namen (Wahlschlüssel), unter denen DKIM meist liegt. Keine abschließende Liste.
    private static readonly string[] DkimSelectors = ["default", "selector1", "selector2", "google", "k1", "dkim", "mail", "s1", "s2"];

    // Nicht mehr Versand-IPs rückwärts auflösen als nötig; ein paar genügen für die Aussage.
    private const int MaxSenderIpsToCheck = 5;

    private readonly IDnsProbe _dns = dns;

    /// <inheritdoc/>
    public string Id => "mail.setup";

    /// <inheritdoc/>
    public CheckCategory Category => CheckCategory.Mail;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Finding>> RunAsync(CheckTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (target.IsIpAddress)
        {
            return
            [
                new Finding(
                    Id + ".noip",
                    Category,
                    Severity.Info,
                    "Keine Mail-Prüfung für eine IP-Adresse",
                    "Mail-Einträge (MX, SPF, DMARC) hängen an einem Domainnamen, nicht an einer IP."),
            ];
        }

        string domain = target.MailDomain;
        List<Finding> findings = [];

        if (target.IsSubdomain)
        {
            findings.Add(new Finding(
                Id + ".subdomain",
                Category,
                Severity.Info,
                $"Geprüft wird die Domain {domain} (die Seite läuft auf {target.Host})",
                $"Aus Sicht von außen: Verschickt die Seite mit einer Absenderadresse @{target.Host}, gelten SPF "
                + "und DKIM dieser Subdomain — und die sind oft nicht eingerichtet. Welche Absenderadresse die "
                + "Seite wirklich nutzt, ist von außen nicht feststellbar.",
                new Recommendation($"Als Absenderadresse @{domain} verwenden oder SPF/DKIM auch für die Subdomain einrichten."))
            {
                Confidence = Confidence.Inferred,
            });
        }

        IReadOnlyList<string> exchangers = await _dns.QueryMailExchangersAsync(domain, cancellationToken).ConfigureAwait(false);
        // Ein MX "." (vom Resolver zu "" gekürzt) ist ein Null-MX nach RFC 7505: die Domain nimmt
        // bewusst keine Mail an. Das ist etwas anderes als ein fehlender MX.
        List<string> realExchangers = exchangers.Where(mx => !string.IsNullOrEmpty(mx)).ToList();
        bool isNullMx = realExchangers.Count == 0 && exchangers.Count > 0;
        findings.Add(DescribeMx(domain, realExchangers, isNullMx));

        SpfRecord? spf = await ReadSpfAsync(domain, cancellationToken).ConfigureAwait(false);
        findings.AddRange(DescribeSpf(spf));
        findings.AddRange(await CheckSenderReverseAsync(spf, domain, cancellationToken).ConfigureAwait(false));

        findings.AddRange(await CheckDkimAsync(domain, cancellationToken).ConfigureAwait(false));
        findings.AddRange(await CheckDmarcAsync(domain, cancellationToken).ConfigureAwait(false));
        findings.AddRange(await CheckMaturityRecordsAsync(domain, cancellationToken).ConfigureAwait(false));
        findings.AddRange(await CheckDaneAsync(domain, realExchangers, cancellationToken).ConfigureAwait(false));

        findings.Add(BulkSenderNote());
        findings.Add(VerificationNote());
        findings.Add(ScopeNote());
        return findings;
    }

    private Finding DescribeMx(string domain, List<string> exchangers, bool isNullMx)
    {
        if (exchangers.Count > 0)
        {
            return new Finding($"{Id}.mx", Category, Severity.Ok, $"MX-Eintrag vorhanden: {string.Join(", ", exchangers)}");
        }

        if (isNullMx)
        {
            return new Finding(
                $"{Id}.mx.null",
                Category,
                Severity.Info,
                $"{domain} hat einen Null-MX (nimmt bewusst keine Mail an)",
                "Ein MX „.“ (RFC 7505) sagt ausdrücklich, dass die Domain keine Mail empfängt. Das ist eine "
                + "bewusste Einstellung, kein Fehler — passend für eine reine Versand- oder Web-Domain.");
        }

        // Technisch regelt MX das Empfangen. In der Praxis ist ein fehlender MX aber ein
        // häufiger Grund für ausbleibende Bestätigungsmails: Bei der Musik-Seite kamen die Mails
        // erst an, nachdem der MX gesetzt war. Darum eine Warnung — aber nicht
        // „Kritisch“, weil von außen nicht beweisbar ist, dass es im Einzelfall die Ursache ist.
        return new Finding(
            $"{Id}.mx.missing",
            Category,
            Severity.Warning,
            $"{domain} hat keinen MX-Eintrag",
            "Ohne MX kann die Domain keine Mail empfangen, und viele Empfänger misstrauen einer Domain ganz "
            + "ohne MX — ein häufiger Grund dafür, dass Bestätigungsmails nicht ankommen. Erfahrungsgemäß hilft "
            + "es spürbar, den MX zu setzen.",
            new Recommendation("Beim Mail-Anbieter den MX-Eintrag der Domain setzen."));
    }

    private async Task<SpfRecord?> ReadSpfAsync(string domain, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> records = await _dns.QueryTextRecordsAsync(domain, cancellationToken).ConfigureAwait(false);
        string? raw = records.FirstOrDefault(record => record.StartsWith("v=spf1", StringComparison.OrdinalIgnoreCase));
        return raw is null ? null : SpfRecord.Parse(raw, domain);
    }

    private IEnumerable<Finding> DescribeSpf(SpfRecord? spf)
    {
        if (spf is null)
        {
            yield return new Finding(
                $"{Id}.spf.missing",
                Category,
                Severity.Warning,
                "Kein SPF-Eintrag",
                "Ohne SPF kann der Empfänger nicht prüfen, ob der Server überhaupt für die Domain senden darf.",
                new Recommendation("Einen SPF-Eintrag als TXT-Record setzen.", "v=spf1 include:_spf.deinanbieter.de -all"));
            yield break;
        }

        yield return spf.AllQualifier switch
        {
            SpfAllQualifier.Pass => new Finding(
                $"{Id}.spf.open",
                Category,
                Severity.Critical,
                "SPF erlaubt jedem das Senden (+all)",
                spf.Raw,
                new Recommendation("Das Ende des SPF-Eintrags auf -all setzen.", "v=spf1 … -all")),
            SpfAllQualifier.Fail => new Finding($"{Id}.spf", Category, Severity.Ok, "SPF-Eintrag vorhanden und hart abschließend (-all)", spf.Raw),
            SpfAllQualifier.SoftFail => new Finding(
                $"{Id}.spf.soft",
                Category,
                Severity.Info,
                "SPF endet auf ~all (weich)",
                spf.Raw,
                new Recommendation("Für den Betrieb auf -all anheben, sobald alle Versandwege erfasst sind.")),
            _ => new Finding(
                $"{Id}.spf.noall",
                Category,
                Severity.Warning,
                "SPF ohne klaren Abschluss (-all fehlt)",
                spf.Raw,
                new Recommendation("Den Eintrag mit -all abschließen, damit nicht erfasste Absender abgewiesen werden.")),
        };

        // RFC 7208 begrenzt die DNS-Abfragen eines SPF-Laufs auf zehn; darüber bricht die
        // Auswertung beim Empfänger mit PermError ab, und SPF gilt dann als nicht bestanden.
        if (spf.DnsLookupMechanismCount > 10)
        {
            yield return new Finding(
                $"{Id}.spf.lookups",
                Category,
                Severity.Warning,
                $"SPF löst zu viele DNS-Abfragen aus ({spf.DnsLookupMechanismCount}, erlaubt sind 10)",
                "Über der Grenze bewerten Empfänger SPF als Fehler (PermError).",
                new Recommendation("Die include-Kette verschlanken oder zusammenfassen (SPF-Flattening)."));
        }
    }

    private async Task<IReadOnlyList<Finding>> CheckSenderReverseAsync(SpfRecord? spf, string domain, CancellationToken cancellationToken)
    {
        // Ohne SPF ist von außen nicht bekannt, welche Server für die Domain senden. Dann wird
        // bewusst keine Rückwärtsauflösung behauptet.
        if (spf is null)
        {
            return
            [
                new Finding(
                    $"{Id}.ptr.unknown",
                    Category,
                    Severity.Info,
                    "Versandhosts von außen nicht bestimmbar",
                    "Ohne SPF lässt sich nicht sagen, welche Server für die Domain senden. Die Rückwärtsauflösung "
                    + "(PTR) ist deshalb nicht prüfbar.")
                {
                    Confidence = Confidence.Inferred,
                },
            ];
        }

        List<Finding> findings = [];

        // Läuft der Versand über einen Anbieter (include:), ist dessen Einrichtung maßgeblich,
        // nicht die der eigenen Hosts. Das wird benannt, nicht als Mangel gewertet.
        if (spf.IncludedDomains.Count > 0)
        {
            findings.Add(new Finding(
                $"{Id}.ptr.relay",
                Category,
                Severity.Info,
                $"Versand läuft (laut SPF) über: {string.Join(", ", spf.IncludedDomains)}",
                "Bei diesen Anbietern liegt die maßgebliche Einrichtung samt Rückwärtsauflösung. Von außen ist "
                + "sie hier nicht prüfbar.")
            {
                Confidence = Confidence.Inferred,
            });
        }

        // Eigene Versand-IPs: aus ip4-Literalen und aufgelösten a/mx-Hosts des SPF.
        List<string> senderIps = [.. spf.DirectIps];
        foreach (string host in spf.DirectHosts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            senderIps.AddRange(await _dns.ResolveIpv4Async(host, cancellationToken).ConfigureAwait(false));
        }

        foreach (string ip in senderIps.Distinct().Take(MaxSenderIpsToCheck))
        {
            cancellationToken.ThrowIfCancellationRequested();
            findings.Add(await DescribeReverseAsync(ip, domain, cancellationToken).ConfigureAwait(false));
        }

        return findings;
    }

    private async Task<Finding> DescribeReverseAsync(string ip, string domain, CancellationToken cancellationToken)
    {
        string? pointer = await _dns.QueryPointerAsync(ip, cancellationToken).ConfigureAwait(false);
        if (pointer is null)
        {
            return new Finding(
                $"{Id}.ptr.missing.{ip}",
                Category,
                Severity.Info,
                $"Versand-IP {ip} hat keine Rückwärtsauflösung (PTR)",
                "Fehlt der PTR, lehnen manche Empfänger die Mail ab.",
                new Recommendation("Beim Anbieter für diese IP einen PTR-Eintrag auf einen Namen der Domain setzen lassen."))
            {
                Confidence = Confidence.Inferred,
            };
        }

        if (pointer.Contains(domain, StringComparison.OrdinalIgnoreCase))
        {
            return new Finding($"{Id}.ptr.{ip}", Category, Severity.Ok, $"Versand-IP {ip} löst passend auf ({pointer})");
        }

        // Forward-Bestätigung: Zeigt der PTR-Name vorwärts wieder auf dieselbe IP (FCrDNS), ist
        // die Einrichtung sauber — bei Shared Hosting der Normalfall (z. B. s183.goserver.host).
        // Das ist kein Mangel und wird nicht als „fremder Name“ gewarnt.
        IReadOnlyList<string> forward = await _dns.ResolveIpv4Async(pointer, cancellationToken).ConfigureAwait(false);
        if (forward.Contains(ip, StringComparer.Ordinal))
        {
            return new Finding(
                $"{Id}.ptr.fcrdns.{ip}",
                Category,
                Severity.Ok,
                $"Versand-IP {ip} hat eine saubere Rückwärtsauflösung ({pointer})",
                "Der Name zeigt vorwärts wieder auf dieselbe IP (forward-bestätigt). Dass er nicht die Domain "
                + "enthält, ist bei gemeinsamem Hosting normal und kein Mangel.");
        }

        return new Finding(
            $"{Id}.ptr.mismatch.{ip}",
            Category,
            Severity.Info,
            $"Versand-IP {ip} löst auf einen fremden Namen auf",
            $"Aus Sicht von außen: {ip} zeigt rückwärts auf {pointer}, nicht auf {domain}, und der Name ist nicht "
            + "forward-bestätigt. Ein allgemeiner Anbieter-Name kann ein Spam-Signal sein; bei einem gemeinsamen "
            + "Versanddienst ist das aber oft normal.",
            new Recommendation("Wenn es ein eigener Versandserver ist, den PTR auf einen Namen der Domain setzen lassen."))
        {
            Confidence = Confidence.Inferred,
        };
    }

    private async Task<IReadOnlyList<Finding>> CheckDkimAsync(string domain, CancellationToken cancellationToken)
    {
        foreach (string selector in DkimSelectors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<string> records = await _dns
                .QueryTextRecordsAsync($"{selector}._domainkey.{domain}", cancellationToken)
                .ConfigureAwait(false);

            string? dkim = records.FirstOrDefault(record => record.Contains("v=DKIM1", StringComparison.OrdinalIgnoreCase));
            if (dkim is not null)
            {
                return [new Finding($"{Id}.dkim", Category, Severity.Ok, $"DKIM gefunden (Wahlschlüssel „{selector}“)"), DescribeDkimStrength(dkim)];
            }
        }

        // Der tatsächlich benutzte Wahlschlüssel steht in jeder gesendeten Mail, nicht im DNS unter
        // einem festen Namen. Darum ist das Nichtfinden eine Vermutung, kein Beweis für fehlendes DKIM.
        return
        [
            new Finding(
                $"{Id}.dkim.missing",
                Category,
                Severity.Warning,
                "Kein DKIM unter den üblichen Namen gefunden",
                "Der tatsächlich benutzte Wahlschlüssel ist von außen nicht sicher feststellbar — er kann unter einem "
                + "anderen Namen liegen. Der verlässliche Nachweis ist eine Testmail (siehe unten).",
                new Recommendation("DKIM beim Mail-Anbieter aktivieren; den genutzten Wahlschlüssel über eine Testmail bestätigen."))
            {
                Confidence = Confidence.Inferred,
            },
        ];
    }

    private Finding DescribeDkimStrength(string dkimRecord)
    {
        int? bits = DkimKey.RsaKeySizeBits(dkimRecord);
        if (bits is null)
        {
            // Kein lesbarer RSA-Schlüssel — etwa Ed25519 oder ein widerrufener Schlüssel. Kein Fehler.
            return new Finding(
                $"{Id}.dkim.key",
                Category,
                Severity.Info,
                "DKIM-Schlüssellänge nicht bestimmbar",
                "Der veröffentlichte Schlüssel ist kein lesbarer RSA-Schlüssel (z. B. Ed25519 oder widerrufen).");
        }

        return bits switch
        {
            < 1024 => new Finding(
                $"{Id}.dkim.key",
                Category,
                Severity.Warning,
                $"DKIM-Schlüssel ist zu kurz ({bits} Bit)",
                "Unter 1024 Bit gilt der Schlüssel als unsicher.",
                new Recommendation("Einen neuen DKIM-Schlüssel mit mindestens 2048 Bit erzeugen.")),
            < 2048 => new Finding(
                $"{Id}.dkim.key",
                Category,
                Severity.Info,
                $"DKIM-Schlüssel ist 1024 Bit",
                "1024 Bit ist das Mindestmaß; 2048 Bit sind heute empfohlen.",
                new Recommendation("Bei Gelegenheit auf einen 2048-Bit-Schlüssel wechseln.")),
            _ => new Finding($"{Id}.dkim.key", Category, Severity.Ok, $"DKIM-Schlüssel ausreichend lang ({bits} Bit)"),
        };
    }

    private async Task<IReadOnlyList<Finding>> CheckDmarcAsync(string domain, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> records = await _dns.QueryTextRecordsAsync($"_dmarc.{domain}", cancellationToken).ConfigureAwait(false);
        string? dmarc = records.FirstOrDefault(record => record.StartsWith("v=DMARC1", StringComparison.OrdinalIgnoreCase));

        if (dmarc is null)
        {
            return
            [
                new Finding(
                    $"{Id}.dmarc.missing",
                    Category,
                    Severity.Warning,
                    "Kein DMARC-Eintrag",
                    "DMARC sagt dem Empfänger, was mit Mails geschehen soll, die SPF/DKIM nicht bestehen. Gmail und "
                    + "Yahoo verlangen seit 2024 von Massenversendern einen DMARC-Eintrag.",
                    new Recommendation("Einen DMARC-Eintrag als TXT unter _dmarc setzen.", "v=DMARC1; p=quarantine; rua=mailto:dmarc@deinedomain.de")),
            ];
        }

        Dictionary<string, string> tags = dmarc
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(pair => pair.Length == 2)
            .GroupBy(pair => pair[0].Trim().ToUpperInvariant())
            .ToDictionary(group => group.Key, group => group.First()[1].Trim(), StringComparer.Ordinal);

        string policy = tags.GetValueOrDefault("P", "?");
        List<Finding> findings =
        [
            policy.Contains("none", StringComparison.OrdinalIgnoreCase)
                ? new Finding(
                    $"{Id}.dmarc.none",
                    Category,
                    Severity.Info,
                    "DMARC vorhanden, aber nur beobachtend (p=none)",
                    "p=none meldet nur, ohne zu schützen.",
                    new Recommendation("Nach einer Beobachtungszeit auf p=quarantine oder p=reject anheben."))
                : new Finding($"{Id}.dmarc", Category, Severity.Ok, $"DMARC vorhanden (p={policy})", dmarc),
        ];

        // Ohne Berichtsadresse (rua) bekommt man keine Auswertung, wer im eigenen Namen sendet.
        if (!tags.ContainsKey("RUA"))
        {
            findings.Add(new Finding(
                $"{Id}.dmarc.norua",
                Category,
                Severity.Info,
                "DMARC ohne Berichtsadresse (rua)",
                "Ohne rua bekommst du keine Berichte darüber, wer im Namen der Domain sendet und was durchfällt.",
                new Recommendation("Eine rua-Adresse ergänzen.", "rua=mailto:dmarc@deinedomain.de")));
        }

        // pct < 100 wendet die Politik nur auf einen Teil des Verkehrs an.
        if (tags.TryGetValue("PCT", out string? pct) && int.TryParse(pct, out int percent) && percent < 100)
        {
            findings.Add(new Finding(
                $"{Id}.dmarc.pct",
                Category,
                Severity.Info,
                $"DMARC wirkt nur auf {percent} % des Verkehrs (pct={percent})",
                "Der Rest wird von der Politik nicht erfasst. Für vollen Schutz pct weglassen (= 100)."));
        }

        return findings;
    }

    private async Task<IReadOnlyList<Finding>> CheckDaneAsync(string domain, List<string> exchangers, CancellationToken cancellationToken)
    {
        // DANE/TLSA ergibt nur Sinn mit DNSSEC — ohne signierte Zone ließe sich ein TLSA-Eintrag
        // fälschen. Darum erst DNSSEC prüfen, dann TLSA am primären Mailserver.
        bool signed = await _dns.IsDnssecSignedAsync(domain, cancellationToken).ConfigureAwait(false);
        if (!signed || exchangers.Count == 0)
        {
            return [];
        }

        string primaryMx = exchangers[0];
        bool hasTlsa = await _dns.HasTlsaAsync($"_25._tcp.{primaryMx}", cancellationToken).ConfigureAwait(false);
        return
        [
            hasTlsa
                ? new Finding($"{Id}.dane", Category, Severity.Ok, $"DANE/TLSA am Mailserver vorhanden ({primaryMx})")
                : new Finding(
                    $"{Id}.dane.absent",
                    Category,
                    Severity.Info,
                    "DANE/TLSA nicht eingerichtet (bei vorhandenem DNSSEC möglich)",
                    "Mit DNSSEC lässt sich über TLSA zusätzlich der verschlüsselte Mail-Transport absichern. Kein Muss.",
                    new Recommendation($"TLSA-Eintrag für _25._tcp.{primaryMx} veröffentlichen.")),
        ];
    }

    private Finding BulkSenderNote() => new(
        $"{Id}.bulk",
        Category,
        Severity.Info,
        "Für Massenversand (Newsletter an Gmail/Yahoo) gelten Extra-Regeln",
        "Ab etwa 5.000 Mails am Tag verlangen Gmail und Yahoo seit 2024: SPF, DKIM und DMARC (prüft dieses "
        + "Werkzeug), eine Ein-Klick-Abmeldung (List-Unsubscribe im Mail-Programm) und eine Spam-Rate unter 0,1 % "
        + "(hart: 0,3 %) in den Google Postmaster Tools. Die letzten beiden Punkte sieht ein Blick von außen nicht "
        + "— sie gehören im Versandprogramm und in den Postmaster Tools nachgesehen.")
    {
        Confidence = Confidence.Inferred,
    };

    private async Task<IReadOnlyList<Finding>> CheckMaturityRecordsAsync(string domain, CancellationToken cancellationToken)
    {
        // MTA-STS, TLS-RPT und BIMI sind Reifegrad-Zeichen. Fehlen ist kein Fehler; vorhanden ist
        // ein Pluspunkt. Alle drei stecken in TXT-Einträgen, also ohne zusätzlichen Netzzugriff prüfbar.
        List<Finding> findings = [];

        findings.Add(await MaturityRecordAsync(
            $"_mta-sts.{domain}", "v=STSv1", "MTA-STS", "erzwingt verschlüsselten Mail-Transport zur Domain",
            $"{Id}.mtasts", cancellationToken).ConfigureAwait(false));
        findings.Add(await MaturityRecordAsync(
            $"_smtp._tls.{domain}", "v=TLSRPTv1", "TLS-RPT", "lässt sich über Transport-Probleme berichten",
            $"{Id}.tlsrpt", cancellationToken).ConfigureAwait(false));
        findings.Add(await MaturityRecordAsync(
            $"default._bimi.{domain}", "v=BIMI1", "BIMI", "zeigt das Marken-Logo in unterstützenden Mail-Programmen",
            $"{Id}.bimi", cancellationToken).ConfigureAwait(false));

        return findings;
    }

    private async Task<Finding> MaturityRecordAsync(
        string name,
        string marker,
        string label,
        string purpose,
        string key,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> records = await _dns.QueryTextRecordsAsync(name, cancellationToken).ConfigureAwait(false);
        bool present = records.Any(record => record.Contains(marker, StringComparison.OrdinalIgnoreCase));

        return present
            ? new Finding($"{key}.ok", Category, Severity.Ok, $"{label} eingerichtet")
            : new Finding(
                $"{key}.absent",
                Category,
                Severity.Info,
                $"{label} nicht eingerichtet",
                $"{label} {purpose}. Das ist kein Fehler, sondern ein Reifegrad-Zeichen für fortgeschrittene Absender.");
    }

    private Finding VerificationNote() => new(
        $"{Id}.verify",
        Category,
        Severity.Info,
        "Verlässlich prüfen lässt sich die Mail nur mit einer Testmail",
        "Schick eine Testmail an eine Gmail- und eine iCloud-Adresse und öffne dort „Original anzeigen“. In der "
        + "Zeile Authentication-Results muss spf=pass, dkim=pass und dmarc=pass stehen. iCloud ist strenger als "
        + "Gmail, deshalb beide.");

    private Finding ScopeNote() => new(
        $"{Id}.scope",
        Category,
        Severity.Info,
        "Was ein Blick von außen nicht sieht",
        "Diese Prüfung liest nur öffentliche DNS-Einträge. Den tatsächlichen Versandweg, den verwendeten "
        + "DKIM-Schlüssel und alles hinter einer Anmeldung sieht sie nicht — dafür ist die Testmail da.");
}
