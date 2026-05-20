---
title: SNMP Monitoring
description: ReadyStackGo-Deployments per integriertem SNMP-Agent abfragen
---

ReadyStackGo bringt einen read-only SNMP-Agent mit, sodass externe
Monitoring-Systeme (Nagios, Zabbix, PRTG, LibreNMS, Icinga, ...) Health und
Status von ProductDeployments, einzelnen Stacks, Container-Services und der
RSGO-Instanz selbst per Standard-SNMP-Polling (GET / GETNEXT / WALK) abfragen
können.

> **Status (v0.64):** read-only, SNMPv2c, mit einer Platzhalter-Private
> Enterprise Number (`1.3.6.1.4.1.99999.1`). Der IANA-Antrag läuft; der OID
> wird vor v1.0 auf die zugewiesene PEN umgestellt. SNMPv3-User-Credentials
> können konfiguriert werden, aber RSGO antwortet in dieser Milestone nur
> via v2c; vollständige v3-Responses kommen in v0.65.

## Agent aktivieren

Der Agent ist per Default deaktiviert. Aktivierung via `appsettings.json`
(oder Environment-Variablen in deiner `docker-compose.override.yml`):

```json
{
  "Snmp": {
    "Enabled": true,
    "Port": 1161,
    "ListenAddress": "0.0.0.0",
    "RootOid": "1.3.6.1.4.1.99999.1",
    "Community": "dein-community-string",
    "V3Users": []
  }
}
```

Container nach Änderung neu starten — der Agent liest die Settings einmal
beim Start.

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
snmpwalk -v2c -c dein-community-string rsgo-host:1161 1.3.6.1.4.1.99999.1

# Status eines konkreten ProductDeployments abfragen
# (envIdx und prodIdx aus Settings → SNMP Monitoring)
snmpget -v2c -c dein-community-string rsgo-host:1161 \
   1.3.6.1.4.1.99999.1.3.1.6.<envIdx>.<prodIdx>
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

## Einschränkungen (v0.64)

- **Read-only.** Kein SET, keine Traps. Beides kommt in späteren Phasen.
- **Nur SNMPv2c-Responses.** v3-Credentials dekodieren eingehende Requests,
  Antworten gehen aber weiterhin via v2c; volles v3 in v0.65.
- **Settings nur via Config.** Die WebUI zeigt die aktuelle Konfiguration,
  editiert sie aber noch nicht. Änderungen erfordern Container-Restart.
