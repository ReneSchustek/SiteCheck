# Testabdeckung

Gemessen am 26.10.2026 mit `tools/coverage-snapshot.ps1` (Microsoft Testing Platform,
Cobertura). Gemessen wird in der **Debug**-Konfiguration: Der Release-Build setzt
`DebugType=none` und liefert keine PDBs, ohne die keine Instrumentierung möglich ist.

| Assembly | Zeilenrate | Gate |
|---|---:|---|
| SiteCheck.Core | 97,0 % | ≥ 90 % ✓ |
| SiteCheck.Application | 97,1 % | ≥ 90 % ✓ |
| SiteCheck.Data | 76,8 % | dokumentiert (siehe unten) |

## Warum Data unter 90 % liegt

`SiteCheck.Data` ist die Schicht zum Außen: `HttpProbe`, `TlsProbe`, `DnsProbe`,
`LoadProbe` und `ProbeHttpClients`. Der nicht abgedeckte Rest ist genau der Teil, der eine
echte HTTP-Verbindung, einen echten TLS-Handschlag oder einen laufenden DNS-Resolver braucht:

- `TlsProbe`: der Zertifikats-Rückruf und die Fehlerpfade des Handschlags lassen sich ohne
  echten TLS-Server nicht sinnvoll nachstellen.
- `DnsProbe`: die MX-/TXT-/CAA-/DNSSEC-/TLSA-Abfragen gehen über `DnsClient` an einen Resolver.
- `HttpProbe`: die Verbindungs-Fehlerpfade (Namensauflösung, abgewiesene Verbindung).

Diese Pfade sind über die **Integrationssicht** abgedeckt: Die Konsole (`SiteCheck.Cli`) läuft
regelmäßig gegen echte Seiten (`example.com`, die Musik-Seite) und übt genau diesen Code. Der
messbare, vom Netz unabhängige Teil der Data-Schicht (Speicher, `AppPaths`, `JsonFile`,
`ProbeHttpClients`, der HTTP-Antwort-Aufbau, die IP-Literal-Pfade der `DnsProbe`) ist durch
Unit-Tests abgedeckt.

Echte Integrationstests gegen einen Testserver wären ein eigener Schritt (eigenes
`*.Tests.Integration`-Projekt), falls die Data-Schicht später eine harte Abdeckungszahl braucht.

## Messen

```powershell
./tools/coverage-snapshot.ps1          # Schwelle 90 % für Core und Application
./tools/coverage-snapshot.ps1 -Threshold 95
```
