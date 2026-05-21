---
title: SNMP Monitoring
description: ReadyStackGo-Deployments per integriertem SNMP-Agent abfragen
---

ReadyStackGo bringt einen read-only SNMP-Agent mit, sodass externe
Monitoring-Systeme (Nagios, Zabbix, PRTG, LibreNMS, Icinga, ...) Health und
Status von ProductDeployments, einzelnen Stacks, Container-Services und der
RSGO-Instanz selbst per Standard-SNMP-Polling (GET / GETNEXT / WALK) abfragen
können.

> **Status (v0.65):** SNMPv2c read-only Polling läuft vollständig und ist
> direkt in der WebUI konfigurierbar — ohne Container-Restart.
> SNMPv3-User-Credentials können angelegt werden; v3-Pakete werden korrekt
> dekodiert, RSGO antwortet aktuell aber via v2c. Vollständige
> v3-Responses kommen in einer Folgephase. SNMP-Traps sind scaffolded
> (Receiver-Liste editierbar), die eigentliche Trap-Emission folgt
> separat. Der OID-Root ist der Platzhalter `1.3.6.1.4.1.65846.1`;
> der IANA-PEN-Antrag läuft und wird vor v1.0 umgestellt.

## Agent aktivieren

Der Agent ist per Default deaktiviert. Aktivierung in der WebUI:

1. **Settings → SNMP Monitoring** öffnen
2. **Enabled** einschalten
3. v2c-Community-String setzen (oder leer lassen und SNMPv3-User anlegen)
4. Speichern

Die Änderungen sind sofort aktiv — der Listener rebindet ohne
Container-Restart. Die Konfiguration liegt in der RSGO-Datenbank und
überlebt Upgrades.

### Port-Mapping

Der Container hört intern auf **UDP/1161** (non-privileged, sodass der
Container als Non-Root-User laufen kann — OpenShift restricted SCC, K8s
`runAsNonRoot`, rootless Docker etc.). Die Default-`docker-compose.yml`
mappt den Port 1:1:

```yaml
ports:
  - "1161:1161/udp"
```

Wer den klassischen SNMP-Port `161` von außen erreichbar machen will, legt
eine `docker-compose.override.yml` an:

```yaml
services:
  readystackgo:
    ports:
      - "161:1161/udp"
```

Der Docker-Daemon bindet host:161 (der hat root), der Container-Prozess
bleibt non-privileged.

## OIDs für deine Umgebung herausfinden

Jedes Environment / Produkt / Stack / Service hat einen stabilen
hash-derived Index. Du kannst die OIDs entweder per `snmpwalk` entdecken
oder einfach in der WebUI unter **Settings → SNMP Monitoring** nachsehen.
Die Seite listet jede konkrete OID für deine Instanz mit Copy-Buttons.

Die Seite verlinkt auch den MIB-Download (`/api/snmp/mib`) — importiere die
Datei in dein Monitoring-Tool, sodass die numerischen OIDs auf symbolische
Namen wie `RSGO-MIB::rsgoProductStatus` aufgelöst werden.

## Beispiel: snmpwalk

```bash
# Den kompletten RSGO-Subtree walken
snmpwalk -v2c -c dein-community-string rsgo-host:1161 1.3.6.1.4.1.65846.1

# Status eines konkreten ProductDeployments abfragen
# (envIdx und prodIdx aus Settings → SNMP Monitoring)
snmpget -v2c -c dein-community-string rsgo-host:1161 \
   1.3.6.1.4.1.65846.1.3.1.6.<envIdx>.<prodIdx>
```

## OID-Struktur

| OID | Inhalt |
| --- | --- |
| `<root>.1.*.0` | `rsgoSystem`-Skalare — Version, Uptime, Anzahl Environments, Anzahl Sources, DB-Health, Build-Timestamp |
| `<root>.2.1.<col>.<envIdx>` | `rsgoEnvironmentTable` — ID, Name, Typ |
| `<root>.3.1.<col>.<envIdx>.<prodIdx>` | `rsgoProductTable` — ID, Name, Version, Status, Total/Running/Failed Stacks, LastDeployedAt, ErrorMessage |
| `<root>.4.1.<col>.<envIdx>.<prodIdx>.<stackIdx>` | `rsgoStackTable` — Name, Status, Service-Count, Order, Error |
| `<root>.5.1.<col>.<envIdx>.<prodIdx>.<stackIdx>.<svcIdx>` | `rsgoServiceTable` — Name, Container-Name, Running, Health-Status, Restart-Count, Last-Health-Check |

Die exakten Spaltennummern und Enum-Mappings stehen in der MIB-Datei.

## MIB-Datei herunterladen

Solange der Agent läuft, ist die MIB-Datei unter `/api/snmp/mib`
erreichbar (auch über den Download-Button auf
**Settings → SNMP Monitoring** verlinkt). Importiere sie in den MIB-Browser
deines Monitoring-Tools, damit symbolische Namen wie
`RSGO-MIB::rsgoProductStatus` funktionieren.

## Index-Stabilität

Die Indizes in den OIDs sind deterministische 31-bit positive
`Int32`-Hashes der zugrundeliegenden Domain-IDs (Environment-GUID,
ProductGroupId, ...). Sie bleiben über Container-Neustarts und
Add/Delete-Operationen auf anderen Entitäten stabil — wenn du eine OID
einmal in deiner Monitoring-Config speicherst, zeigt sie auch danach noch
auf dasselbe logische Objekt.

## SNMP Traps (Push-Benachrichtigungen)

RSGO sendet v2c-Traps an alle Empfänger aus dem Feld **Trap receivers**
auf der Settings-Seite (Komma- oder Zeilenumbruch-getrennt, `host[:port]`,
Default-Port `162`), sobald ein relevantes Domain-Event eintritt:

| Trap | Domain-Event | Inhalt |
| --- | --- | --- |
| `rsgoProductDeploymentFailedTrap` | Ein ProductDeployment ist final auf `Failed`. | Produktname, Fehlermeldung |
| `rsgoProductDeploymentAutoFinalizedTrap` | Eine stuck-Deployment wurde von RSGO automatisch finalisiert. | Produktname, Folge-Status, Begründung |
| `rsgoProductMaintenanceModeChangedTrap` | Maintenance-Modus an/aus für ein Produkt. | Neuer Operation-Mode, Begründung |

Traps werden nur gesendet, wenn der SNMP-Agent aktiviert ist und ein
v2c-Community-String konfiguriert ist (dieser dient auch als Trap-Community).

## Einschränkungen (v0.65)

- **Read-only-Polling.** Kein SET. (Traps für ProductDeploymentFailed,
  ProductDeploymentAutoFinalized und ProductMaintenanceModeChanged werden
  bei Eintreten der Domain-Events emittiert — siehe Abschnitt darüber.)
