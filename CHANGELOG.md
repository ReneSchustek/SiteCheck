# Änderungsprotokoll

Das Format folgt [Keep a Changelog](https://keepachangelog.com/de/1.1.0/), und das Projekt
verwendet [Semantic Versioning](https://semver.org/lang/de/).

## [0.1.0] – 2026-10-26

Erste Vorab-Version.

### Hinzugefügt

- Geschichtete Neufassung aus dem Konsolen-Prototyp: Kern, Abläufe, Daten, Konsole und eine
  WinUI-3-Oberfläche.
- Web-Prüfungen: Weiterleitung auf https, TLS und Zertifikat, Schutz-Kopfzeilen, Cookies,
  Versionsverrat, heikle Dateien, offene Verzeichnis-Auflistung, security.txt.
- Mail-Einrichtung: MX, SPF, DKIM samt Schlüssellänge, DMARC mit Berichtsadresse und Ausrichtung,
  MTA-STS, TLS-RPT, BIMI und die MTA-STS-Policy über HTTPS.
- DNS-Härtung: DNSSEC, CAA und DANE/TLSA.
- Ehrliche Einordnung jedes Befunds als gesichert oder Vermutung („aus Sicht von außen“).
- Bericht als Text zum Kopieren und als gestaltete HTML-Datei zum Speichern, laienverständlich.
- Eigene Seiten speichern und auf einen Schlag prüfen; je Seite eine Ampel aus dem schlimmsten Befund.
- Stresstest der eigenen Seite mit fest verdrahteten Obergrenzen und Pflicht-Bestätigung vor jedem Lauf.
- Konsolen-Zugang (`sitecheck <adresse>`).
