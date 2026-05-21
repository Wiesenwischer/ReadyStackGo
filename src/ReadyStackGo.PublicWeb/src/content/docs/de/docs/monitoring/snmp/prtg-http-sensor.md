---
title: SNMP — PRTG HTTP Sensor (5-Min-Setup)
description: Ein einziger PRTG-Sensor pollt einen RSGO-Endpunkt und bekommt aggregierte Status-Channels — ohne Template-Install, ohne Probe-Restart, ohne MIB-Import.
---

ReadyStackGo bietet einen Endpunkt im **PRTG "HTTP Data Advanced"-JSON-Schema**: Du fügst in PRTG einen einzelnen Sensor hinzu, pastest eine URL, und PRTG pollt direkt von RSGO. Kein Programm-Files-Geschreibe, kein Probe-Restart, kein MIB-Import.

:::tip[Wann nimmst du diese Variante?]
- Wenn du PRTG nur "mal kurz" auf RSGO loslassen willst — z. B. um auf einem Status-TV ein "alles grün/rot"-Widget zu haben.
- Wenn du keine Admin-Rechte auf dem PRTG-Server hast (kannst aber Sensoren in der Web-UI anlegen).
- Wenn dir die [Variant 1 (Device Template Bundle)](/de/docs/monitoring/snmp/prtg) zu viel Setup ist.

Für tiefere Granularität (ein Sensor pro Stack, pro Service) bleibt Variant 1 die richtige Wahl. Beide Varianten können nebeneinander laufen.
:::

## Was du bekommst

Ein PRTG-Sensor mit ~13 Channels (alle global aggregiert über alle Umgebungen):

| Channel | Bedeutung | Sensor-Status bei Wert > 0 |
|---------|-----------|----------------------------|
| **Products total** | Aktive ProductDeployments (ohne Removed/Superseded) | — |
| **Products healthy** | Status = Running | — |
| **Products degraded** | Status = PartiallyRunning | Warning |
| **Products failed** | Status = Failed | **Error** |
| **Products in maintenance** | OperationMode = Maintenance | — (kein Alarm bei geplantem Stillstand) |
| **Stacks total / running / failed** | Pro-Stack-Aggregation | Stacks failed → Error |
| **Services running / not running** | Container-Run-State | Services not running → Error |
| **Environments** | Anzahl Environments im RSGO | — |
| **DB health** | DB-Probe (1 = ok via PRTG standard lookup) | — |
| **Uptime** | RSGO-Container-Uptime in Sekunden | — |

Plus ein **Statustext** unter dem Sensor mit den problematischen Produkten als Liste (z. B. `ams.tooling FAILED · ams.project in maintenance`).

## Setup in 3 Schritten

### Schritt 1: API-Key in RSGO erzeugen

Öffne **Settings → CI/CD Integration** und lege einen neuen Key an:

- **Name**: `prtg-sensor` (oder ähnlich, zur Wiedererkennung in den Logs)
- **Permissions**: nur **`Settings:Read`** — der PRTG-Sensor liest nur Status, schreibt nichts
- **Environment scope**: optional, falls du nur eine Umgebung exponieren willst

Kopiere den **einmal angezeigten** Key (Format `rsgo_...`).

### Schritt 2: URL aus der SNMP-Settings-Seite kopieren

Auf **Settings → SNMP Monitoring** findest du die Card **"PRTG HTTP sensor"** mit der vollständigen URL:

```
https://rsgo.example.local/api/integrations/prtg/status?apikey=YOUR_API_KEY
```

Ersetze `YOUR_API_KEY` durch den Key aus Schritt 1.

### Schritt 3: Sensor in PRTG anlegen

1. In der PRTG-Weboberfläche: **Device öffnen** (oder anlegen) → **Add Sensor**.
2. **Search "HTTP Data Advanced"** → Sensor-Typ wählen.
3. Beim **URL**-Feld die in Schritt 2 zusammengebaute URL einfügen.
4. **Interval** auf 60 s lassen (der RSGO-Endpunkt cached intern 30 s, kürzeres Polling bringt nichts).
5. **OK** → Sensor läuft.

Innerhalb von ~60 s sind alle Channels mit Werten gefüllt. Falls einer rot wird (Failed/Stack-Down/Service-Down), zeigt PRTG den Sensor-Text in der Übersicht: dort siehst du sofort, welches Produkt das Problem ist.

## Sicherheit & API-Key in der URL

**Wichtig**: PRTGs HTTP-Data-Advanced-Sensor kann **keine** HTTP-Header senden — der API-Key muss in der URL stehen. Das hat zwei Konsequenzen:

1. **Die Sensor-URL ist ein Geheimnis** — wer sie hat, kann den gleichen Status-Endpunkt lesen wie der Sensor. Behandle sie wie ein Passwort.
2. **PRTG protokolliert URLs** standardmäßig (Sensor-Logs, Debug-Daten). Wenn das ein Problem ist:
   - Den API-Key regelmäßig rotieren (in RSGO `/settings/cicd` → revoken → neu anlegen).
   - **Oder** auf Variant 1 (Device Template) wechseln, die nutzt nur SNMP-Auth.

Der Endpunkt selbst gibt nur Lese-Status zurück (keine Geheimnisse, keine Mutations) — der Impact eines kompromittierten Sensor-URL ist Lesezugriff auf das, was sowieso im Dashboard steht.

## JSON-Format (Referenz)

Wenn du den Endpunkt selbst aufrufst (z. B. mit `curl`):

```bash
curl -H "X-Api-Key: rsgo_..." https://rsgo.example.local/api/integrations/prtg/status | jq
```

Die Antwort entspricht exakt dem von Paessler dokumentierten [Custom Sensors via JSON-Schema](https://www.paessler.com/manuals/prtg/custom_sensors):

```json
{
  "prtg": {
    "result": [
      { "channel": "Products total",        "value": "5",  "unit": "Count" },
      { "channel": "Products healthy",      "value": "4",  "unit": "Count" },
      { "channel": "Products failed",       "value": "1",  "unit": "Count",
        "limitmaxerror": 0, "limitmode": 1 },
      { "channel": "Products in maintenance", "value": "0", "unit": "Count" },
      { "channel": "DB health",             "value": "1",
        "unit": "Custom", "customunit": "state",
        "ValueLookup": "prtg.standardlookups.yesno.stateyesok" },
      { "channel": "Uptime",                "value": "123456", "unit": "TimeSeconds" }
    ],
    "text": "ams.tooling FAILED"
  }
}
```

## Auth via Header (Alternative)

Wenn du den Sensor von einer Custom-Skript-Sensorvariante aus aufrufst (oder ein anderes Tool wie Checkmk, das echte Header schicken kann), kannst du den API-Key statt im Querystring auch im **`X-Api-Key`**-Header senden:

```bash
curl -H "X-Api-Key: rsgo_..." https://rsgo.example.local/api/integrations/prtg/status
```

Beide Wege liefern dieselbe Antwort. Header ist die **bevorzugte** Variante — der Querystring-Pfad existiert nur, weil PRTGs Standard-HTTP-Sensor keine Header injecten kann.

## Fehlerbehandlung

| Symptom | Ursache | Fix |
|---------|---------|-----|
| Sensor zeigt "401 Unauthorized" | API-Key fehlt oder Permission falsch | Key prüfen, sicherstellen dass `Settings:Read` als Permission gesetzt ist |
| Sensor zeigt "404 Not Found" | URL falsch (Tippfehler im Pfad) | URL aus der RSGO-UI neu kopieren — der Pfad ist `/api/integrations/prtg/status` |
| Alle Channels sind 0 | Keine ProductDeployments aktiv | Im RSGO-Dashboard prüfen, ob Deployments laufen |
| Sensor wird rot, aber nichts ist kaputt | Du hast einen Stack manuell gestoppt — "Stacks failed" zählt jeden non-Running als Failed | Stack wieder starten oder Maintenance-Mode setzen (zählt dann nicht mehr als Failure) |
| Sensor lädt nichts in der ersten Minute | RSGO-Snapshot-Cache (30 s) erstellt den ersten Snapshot lazy | Bis zu 60 s warten — danach kommen die Daten |

## Verwandte Seiten

- [SNMP einrichten in ReadyStackGo](/de/docs/monitoring/snmp) — Grundkonfiguration des Agenten
- [PRTG Device Template Bundle (Variant 1)](/de/docs/monitoring/snmp/prtg) — Deep Integration mit Per-Stack-Sensoren
- [SNMP-Grundlagen](/de/docs/monitoring/snmp/grundlagen) — was SNMP eigentlich ist
