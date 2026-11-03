# Änderungsprotokoll

Das Format folgt [Keep a Changelog](https://keepachangelog.com/de/1.1.0/), und das Projekt
verwendet [Semantic Versioning](https://semver.org/lang/de/).

## [Unreleased]

### Behoben

- SiteCheck konnte den eigenen Anschluss beim Hoster sperren lassen. Es fragt jetzt höchstens
  40-mal je Minute und 150-mal je Domain und Stunde ab, auch über Neustarts und zwischen Oberfläche
  und Konsole hinweg.
- Weist ein Server ab oder antwortet wiederholt nicht, hält der Lauf sofort an und versucht es nicht
  erneut. Die Domain ruht dann eine Stunde; was nicht lief, steht als „nicht geprüft“ im Bericht.
- Der Stresstest endet bei der ersten Abweisung, und die Domain ruht danach mindestens eine Viertelstunde.
- Was nicht geprüft wurde, erscheint im Vergleich zum vorigen Lauf nicht mehr als „behoben“.

### Geändert

- Heikle Dateien und Verzeichnis-Auflistung werden nur noch nach Bestätigung für den einzelnen Lauf
  abgefragt (Konsole: `--sensitive-paths`), weil sie für Hoster wie ein Angriff aussehen.
- Vor der ersten Prüfung einer Domain erscheint ein Hinweis auf die Gefahr einer Sperre.
- Der Sammellauf endet, sobald eine Seite abweist, weil die übrigen oft auf demselben Server liegen.
- Die Konsole meldet einen angehaltenen Lauf mit dem Rückgabewert `3`.

## [0.1.0] – 2026-10-26

Erste Vorab-Version.

### Hinzugefügt

- Geschichtete Neufassung aus dem Konsolen-Prototyp: Kern, Abläufe, Daten, Konsole und eine
  WinUI-3-Oberfläche.
- Web-Prüfungen: Weiterleitung auf https, TLS und Zertifikat, Schutz-Kopfzeilen, Cookies,
  Versionsverrat, heikle Dateien, offene Verzeichnis-Auflistung, security.txt.
- Mail-Einrichtung: MX, SPF, DKIM samt Schlüssellänge, DMARC mit Richtlinie, Berichtsadresse und Anteil,
  MTA-STS, TLS-RPT, BIMI und die MTA-STS-Policy über HTTPS.
- DNS-Härtung: DNSSEC, CAA und DANE/TLSA.
- Ehrliche Einordnung jedes Befunds als gesichert oder Vermutung („aus Sicht von außen“).
- Bericht als Text zum Kopieren und als gestaltete HTML-Datei zum Speichern, laienverständlich.
- Eigene Seiten speichern und auf einen Schlag prüfen; je Seite eine Ampel aus dem schlimmsten Befund.
- Stresstest der eigenen Seite mit fest verdrahteten Obergrenzen und Pflicht-Bestätigung vor jedem Lauf.
- Konsolen-Zugang (`sitecheck <adresse>`).
