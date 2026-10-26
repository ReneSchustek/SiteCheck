<div align="center">

<img src="src/SiteCheck.App/Assets/logo.png" alt="SiteCheck" width="120" />

# SiteCheck

**Prüft die eigenen Websites von außen auf Sicherheit und Mail-Einrichtung.**

[![CI](https://github.com/ReneSchustek/SiteCheck/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/ReneSchustek/SiteCheck/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/ReneSchustek/SiteCheck?sort=semver)](https://github.com/ReneSchustek/SiteCheck/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

</div>

Ein kleiner Prüfer für die **eigenen** Seiten. Er klopft eine Adresse von außen ab und sagt
in klarer Sprache, was gut eingestellt ist und was fehlt — gedacht auch für Leute, die keine
Admins sind. Die üblichen Prüfungen sind passiv: gewöhnliche Anfragen und DNS-Abfragen, es wird
nichts angegriffen und nichts verschickt. Nur der Stresstest belastet aktiv — und erst, nachdem
bestätigt wurde, dass die Seite dir gehört und selbst gehostet ist.

## Herunterladen

Fertige Pakete für Windows (x64) liegen unter
[Releases](https://github.com/ReneSchustek/SiteCheck/releases/latest): der Installer oder das
portable ZIP, beide self-contained (keine Runtime nötig).

Die Dateien sind nicht mit einem Zertifikat signiert, deshalb warnt Windows SmartScreen beim ersten
Start („Der Computer wurde geschützt" → **Weitere Informationen** → **Trotzdem ausführen**). Zur
Sicherheit steht im Release die **SHA-256-Prüfsumme** jeder Datei; so lässt sich der Download prüfen:

```powershell
Get-FileHash .\SiteCheck-Setup-v0.1.0.exe -Algorithm SHA256
```

## Was er prüft

**Web**
- Leitet http fest auf https um?
- Welche Verschlüsselung (TLS) wird ausgehandelt, wie lange gilt das Zertifikat, passt es zur Adresse?
- Sind die Schutz-Kopfzeilen gesetzt (CSP, HSTS, X-Frame-Options, nosniff, Referrer-Policy)?
- Ist das Sitzungs-Cookie sauber (Secure, HttpOnly, SameSite)?
- Verrät der Server seine Version (Server, X-Powered-By)?
- Sind heikle Dateien offen lesbar (`.git`, `.env`, `composer.json`, `.htaccess` …)?
- Zeigt der Server an gängigen Pfaden eine offene Verzeichnis-Auflistung?
- Gibt es eine `/.well-known/security.txt` (RFC 9116) mit Kontakt für Sicherheitsmeldungen?

**DNS-Härtung**
- Ist die Domain mit DNSSEC signiert?
- Legt ein CAA-Eintrag fest, welche Zertifizierungsstellen ausstellen dürfen?
- Gibt es für den Mailserver TLSA-Einträge (DANE)?

**Mail-Einrichtung** (der häufigste Grund für nicht ankommende Bestätigungsmails)
- MX-Eintrag vorhanden?
- SPF gesetzt, und wie streng endet er (`~all` oder `-all`)?
- DKIM gesetzt, und ist der Schlüssel lang genug?
- DMARC gesetzt: Richtlinie, Berichtsadresse (`rua`) und Anteil (`pct`)?
- MTA-STS (samt der Richtlinie über HTTPS), TLS-RPT und BIMI.
- Passt die Rückwärtsauflösung (PTR) der eigenen Versand-Adressen? Geprüft werden nur einzelne
  Adressen aus dem SPF, keine Netzbereiche; läuft der Versand über einen Anbieter, wird das benannt.
- Hinweis, wenn die Seite auf einer Subdomain läuft, SPF/DKIM aber nur auf der Hauptdomain liegen.

**Ehrlich eingeordnet**
- Jeder Befund steht als gesichert oder als Vermutung „aus Sicht von außen“ da.
- War die Seite nicht erreichbar, heißt es „nicht geprüft“ statt „in Ordnung“. Bekommt der eigene
  Rechner gar keine Verbindung, weist der Bericht darauf hin, dass oft das eigene Netz die Ursache ist
  (ein Virenschutz, der HTTPS aufbricht, oder eine zeitweise IP-Sperre des Hosters).

**Stresstest** (optional, aktiv)
- Hält für kurze Zeit viele gleichzeitige Anfragen offen und misst Antwortzeiten und Fehlerquote.
- Obergrenzen fest verdrahtet (höchstens 20 gleichzeitige Anfragen, höchstens 60 Sekunden).
- Läuft nur nach ausdrücklicher Bestätigung vor **jedem** Lauf, dass die Seite dir gehört.

## Eigene Seiten und Verlauf

Die eigenen Seiten lassen sich speichern und auf einen Schlag prüfen; jede bekommt eine Ampel aus
ihrem schlimmsten Befund. Ein neuer Lauf zeigt, was seit dem vorigen dazugekommen, geblieben oder
behoben ist. Der Verlauf lässt sich leeren.

## Der Bericht

Jedes Ergebnis lässt sich als Text **kopieren** und als gestaltete **HTML-Datei speichern**
(hell und dunkel lesbar). Je Befund steht ein klarer Satz, eine Ampel in Worten und, wo es
hilft, der konkrete Rat samt Beispiel zum Übernehmen.

## Voraussetzungen

- Windows 10 (ab 1809) oder Windows 11
- .NET 10 SDK (für den Bau), Visual Studio 2022/18 mit der Windows-App-SDK-Arbeitslast

## Aufbau

Saubere Schichtung, Abhängigkeiten nur nach innen:

```
SiteCheck.App          WinUI-3-Oberfläche (Ansichten, ViewModels, Composition Root)
SiteCheck.Cli          schmaler Konsolen-Zugang auf derselben Prüflogik
SiteCheck.Application   Abläufe: Prüflauf, Lasttest, Berichtsbau
SiteCheck.Core          Domäne: Befund-Modell, Stufen, Prüfungen, Verträge (ohne Framework)
SiteCheck.Data          konkrete Zugriffe: HTTP, TLS, DNS, Speicher (JSON)
```

## Entwicklung

```bash
# Bauen und prüfen
dotnet build SiteCheck.slnx -c Release
dotnet test tests/SiteCheck.Core.Tests/SiteCheck.Core.Tests.csproj -c Release
dotnet test tests/SiteCheck.Application.Tests/SiteCheck.Application.Tests.csproj -c Release
dotnet test tests/SiteCheck.Data.Tests/SiteCheck.Data.Tests.csproj -c Release
dotnet test tests/SiteCheck.App.Tests/SiteCheck.App.Tests.csproj -c Release
```

## Benutzen

**Oberfläche:** `SiteCheck.App` starten, oben die Adresse eingeben, „Prüfen“.

**Konsole:**

```bash
dotnet run --project src/SiteCheck.Cli -- https://example.com
```

Rückgabewert der Konsole ist `1`, sobald ein kritischer Punkt dabei ist, sonst `0` — praktisch
für einen geplanten Lauf.

## Was er bewusst NICHT macht

Keine aktiven Angriffe außer dem ausdrücklich bestätigten Stresstest gegen die eigene Seite:
keine SQL-Einschleusung, kein Durchprobieren von Passwörtern, kein Fuzzing. Diese Dinge gehören
von Hand und im Code geprüft:

- SQL-Einschleusung (immer vorbereitete Anweisungen / Platzhalter)
- Skript-Einschleusung / XSS (Ausgaben maskieren)
- CSRF-Token in ändernden Formularen
- Sitzungs-ID nach dem Login neu vergeben, Fehlversuche begrenzen
- Passwörter nur als Hash (bcrypt/argon2)
- Zugriffsrechte / fremde Daten (IDOR)
- Datei-Uploads absichern

## Hinweis

Nur für Seiten benutzen, die dir gehören oder für die du die Erlaubnis hast. Viele Anbieter
werten schon viele schnelle Anfragen als Angriff und sperren die Adresse vorübergehend.

## Lizenz

MIT — siehe [LICENSE](LICENSE).
