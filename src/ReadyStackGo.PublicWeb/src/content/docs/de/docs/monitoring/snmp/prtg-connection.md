---
title: PRTG Auto-Register — wiederverwendbare PrtgConnection
description: Eine PRTG-Connection einmalig in RSGO einrichten, dann ProductDeployments daran linken — RSGO trägt sich beim Deploy selbst als PRTG-Device ein und räumt beim Remove wieder auf.
---

Mit einer **PrtgConnection** kann RSGO sich **selbst** in PRTG eintragen, wenn ein ProductDeployment hochkommt, und sich wieder austragen, wenn es entfernt wird. Im Gegensatz zu [Variant 4 (HTTP Data Advanced Sensor)](/de/docs/monitoring/snmp/prtg-http-sensor) und [Variant 1 (Device Template Bundle)](/de/docs/monitoring/snmp/prtg), die PRTG-seitig manuell konfiguriert werden, ist diese Variante **bidirektional**: RSGO kennt PRTG und ruft dessen API auf.

:::tip[Wann ist das die richtige Wahl?]
- Du hast viele Deployments und willst nicht für jedes manuell einen PRTG-Sensor anlegen.
- PRTG ist eine bestehende, zentrale Plattform und du willst alle RSGO-Produkte dort als Devices sehen.
- Du willst, dass beim Removal eines Produkts der PRTG-Eintrag automatisch verschwindet (kein Sensor-Friedhof).
:::

## Wie es funktioniert

1. Du legst **einmal** eine PrtgConnection in `/settings/prtg-connections` an: URL + PRTG-API-Token + optional eine **Template-Device-ID** in PRTG, die RSGO pro Deployment dupliziert.
2. Beim Deploy linkst du das ProductDeployment an die Connection (über die API oder die Detail-Seite — siehe "Linken" unten).
3. Sobald das ProductDeployment in den Status **Running** wechselt, ruft RSGO die PRTG-API auf:
   - `duplicateobject.htm` → dupliziert das Template-Device, vergibt einen Namen `RSGO: <product> (<version>)`, setzt den Host auf die RSGO-URL
   - `pause.htm?action=1` → unpaused das neue Device, sodass PRTG zu pollen anfängt
   - Die zurückgegebene PRTG-Device-ID wird auf dem ProductDeployment gespeichert.
4. Beim **Removed**- oder **Superseded**-Event löscht RSGO das Device per `deleteobject.htm`.

Alle Aufrufe sind **best-effort**: ein PRTG-Ausfall blockiert den RSGO-Deploy nicht, nur die Sync-Operation wird in den RSGO-Logs als Warning markiert.

## Connection in RSGO anlegen

### Schritt 1: Template-Device in PRTG vorbereiten

Lege in PRTG einmalig ein **Template-Device** an, das alle Sensoren enthält, die RSGO pro Deployment haben soll. Sensoren auf Pause stellen, damit das Template selbst nichts pollt. Notiere dir die **Object ID** des Devices (sichtbar in der URL beim Bearbeiten: `device.htm?id=4221` → ID = `4221`).

Optional: das [Variant 1 Device-Template-Bundle](/de/docs/monitoring/snmp/prtg) als Ausgangspunkt nutzen — es liefert das Sensoren-Set, das RSGO erwartet.

### Schritt 2: PRTG-API-Token erzeugen

In PRTG: **Setup → System Administration → User Accounts** → eigener User → Passhash sichtbar (oder, ab PRTG 23.x, echter API-Token).

### Schritt 3: Connection in RSGO speichern

Settings hat eine eigene Kachel **PRTG Connections** für den Einstieg:

![Settings-Index mit der PRTG-Connections-Kachel zwischen den anderen Konfigurations-Tiles](/images/docs/prtg-v3-settings-tile.png)

Beim ersten Aufruf ist die Liste noch leer. **+ Add connection** öffnet das Formular:

![Add-PRTG-Connection-Formular mit Name, URL, API token, Template Device ID und Verify-TLS-Checkbox](/images/docs/prtg-v3-add-form.png)

| Feld | Wert |
|------|------|
| **Name** | `prod-prtg` (Frei wählbar, erscheint im Deploy-Wizard) |
| **URL** | `https://prtg.example.local` |
| **API token / passhash** | Aus Schritt 2 |
| **Template Device ID** | `4221` aus Schritt 1 — leer lassen schaltet Auto-Register aus |
| **Verify TLS certificate** | Aushaken, wenn PRTG ein Self-Signed Cert hat (sehr häufig) |

Auf **Create** klicken. Der Token wird in der RSGO-DB verschlüsselt — du siehst ihn danach **nie wieder** in der UI. Anschließend zeigt die Liste den neuen Eintrag:

![PRTG-Connections-Liste mit dem neu erstellten Eintrag, Spalten für URL, Template-Device und Last-used](/images/docs/prtg-v3-connections-list-populated.png)

## Ein ProductDeployment an die Connection linken

Auf der Deployment-Detail-Seite findest du eine **PRTG monitoring** Card mit zwei Tabs:

- **Saved connection** — Connection-Dropdown (für diese Variante)
- **Inline (ad-hoc)** — direkt URL+Token eintragen (siehe [V2](/de/docs/monitoring/snmp/prtg-inline))

![Saved-Connection-Tab der PRTG-monitoring-Card auf der Deployment-Detail-Seite mit Dropdown zum Auswählen der Connection](/images/docs/prtg-detail-saved-tab.png)

Alternativ per REST-API:

```bash
curl -X PUT https://rsgo.local/api/deployments/<deployment-id>/prtg-connection \
  -H "X-Api-Key: rsgo_..." \
  -H "Content-Type: application/json" \
  -d '{"id":"<deployment-id>","prtgConnectionId":"<connection-id>"}'
```

`prtgConnectionId: null` setzt den Link wieder zurück.

Beim nächsten Lifecycle-Event (`Running` → Register, `Removed`/`Superseded` → Deregister) macht RSGO den PRTG-API-Call automatisch.

## Sicherheit

- **API-Token verschlüsselt**: gespeichert mit demselben `ICredentialEncryptionService`, der auch für Docker-Registry-Credentials und SNMPv3-Passphrases zuständig ist.
- **Token-Leak-Impact**: Read+Write-Zugriff auf PRTG. Behandle die Connection wie eine Registry-Credential.
- **TLS**: `Verify TLS` standardmäßig aktiv. Bei Self-Signed-Certs explizit aushaken — RSGO logged das per Connection.
- **Berechtigungen in PRTG**: Der PRTG-User braucht nur Lese- und Schreibrechte auf der Gruppe, in der die RSGO-Devices liegen. Kein PRTG-Admin nötig.

## Welche PRTG-API-Endpoints werden aufgerufen?

| Trigger | RSGO ruft | Zweck |
|---------|-----------|-------|
| ProductDeployment → `Running` | `POST /api/duplicateobject.htm?id=<template>&name=<n>&host=<h>` | Device aus Template anlegen |
| (gleich danach) | `GET /api/pause.htm?id=<new-device-id>&action=1` | Device unpausen, Polling startet |
| ProductDeployment → `Removed` oder `Superseded` | `POST /api/deleteobject.htm?id=<device-id>&approve=1` | Device löschen |
| (Healthcheck der Connection) | `GET /api/getstatus.json` | Liveness + Token-Validierung |

Vollständige PRTG-API-Doku: https://www.paessler.com/manuals/prtg/application_programming_interface_api_definition

## Verhältnis zu den anderen Varianten

| | Variant 4 (HTTP Sensor) | Variant 1 (Bundle) | **Variant 3 (Connection)** |
|---|---|---|---|
| Setup-Aufwand | 5 min in PRTG | 10–15 min in PRTG (Admin) | 5 min in PRTG + RSGO-Connection |
| Auto-Register neuer Deployments | — | ✓ (Auto-Discovery, alle 60 min) | **✓ sofort beim Deploy** |
| Auto-Deregister bei Remove | — | — | **✓** |
| RSGO ruft PRTG-API auf | — | — | **✓** |
| Sensor-Granularität | aggregierte Channels | Per-Stack/Per-Service | aus Template-Device |

Variant 3 macht primär dann Sinn, wenn dir das Auto-Cleanup wichtig ist oder du in einem CI/CD-Setup viele Deployments hochfahren/abreißen lässt.

## Verwandte Seiten

- [SNMP einrichten in ReadyStackGo](/de/docs/monitoring/snmp) — Voraussetzung
- [Variant 1 — Device Template Bundle](/de/docs/monitoring/snmp/prtg) — manuelles Setup, tiefe Granularität
- [Variant 4 — HTTP Data Advanced Sensor](/de/docs/monitoring/snmp/prtg-http-sensor) — schnellste Variante, manuelles Setup
