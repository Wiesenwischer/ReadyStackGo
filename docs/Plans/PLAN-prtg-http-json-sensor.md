<!-- GitHub Epic: #401 (Variant 4) -->
# Phase: PRTG Integration — Variant 4 (HTTP Data Advanced Sensor)

## Ziel

RSGO liefert unter einem dedizierten Endpoint JSON im **PRTG-"HTTP Data Advanced"-Schema** aus. Ein PRTG-Admin fügt **einen einzelnen Sensor** vom Typ "HTTP Data Advanced" hinzu, hängt die RSGO-URL plus API-Key an, und PRTG pollt ab dem Moment Status-Zahlen direkt vom RSGO-API — ohne Device-Template-Install, ohne Probe-Restart, ohne MIB-Import.

Diese Variante ist die **niedrigschwelligste** der vier — die Einstiegsdroge in PRTG-Monitoring für RSGO-Nutzer, die "nur mal kurz beobachten" wollen. Sie ergänzt Variante 1 (Device-Template-Bundle) für tieferes Per-Stack-Monitoring, ersetzt sie aber nicht.

> **Hinweis**: Die Reaktion eines RSGO-Admins beim ersten Roll-out von Variante 1 ("warum muss ich für eine Status-Abfrage Programm-Files-Dateien anfassen + den Probe-Service neu starten?") war der Auslöser für diese Variante. PRTG belohnt für solche Fälle ein **einziges JSON-Endpoint** mit minimalem Setup-Aufwand.

## Analyse

### Bestehende Architektur

- **SnmpSnapshotProvider** ([SnmpSnapshotProvider.cs](../../src/ReadyStackGo.Application/Snmp/SnmpSnapshotProvider.cs)) liefert bereits konsistente Snapshot-Daten (Environment- + Product-Deployment-Liste mit Status, Stacks, Maintenance) mit 30 s Caching. **Genau die Daten, die wir für PRTG brauchen — wir bauen die Antwort einfach neu zusammen.**
- **CICD-API-Keys** ([API Key Management](../../src/ReadyStackGo.Application/UseCases/ApiKeys/)) sind das vorhandene Auth-Mittel für maschinelle Lese-Zugriffe von extern. Permission `Snmp:Read` oder neu `Monitoring:Read` reicht.
- **FastEndpoints** mit `[RequirePermission("…","…")]` + `RbacPreProcessor`-Pattern — gleicher Aufbau wie `/api/snmp/mib`, `/api/snmp/oid-reference`, `/api/snmp/prtg-bundle`.
- **SNMP-Settings-Page** ([SnmpSettingsPage.tsx](../../src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Settings/Snmp/SnmpSettingsPage.tsx)) bekommt eine neue Card "**PRTG HTTP Sensor**", die die fertige URL inkl. API-Key zum Kopieren anzeigt (analog zur bestehenden "PRTG integration"-Card).

### Das PRTG "HTTP Data Advanced"-Schema

PRTG erwartet vom URL-Endpoint genau ein JSON-Objekt mit folgender Struktur (offizielle Paessler-Doku: [Custom Sensors via JSON](https://www.paessler.com/manuals/prtg/custom_sensors)):

```json
{
  "prtg": {
    "result": [
      {
        "channel": "<channel-name>",
        "value": <number-or-string>,
        "unit": "Count" | "Percent" | "BytesBandwidth" | "Custom" | ...,
        "customunit": "<optional, when unit=Custom>",
        "ValueLookup": "<optional, name of lookup>",
        "limitmaxerror": <optional threshold>,
        "limitmode": 1
      },
      ...
    ],
    "text": "<optional, free-form status text shown in PRTG>"
  },
  "error": <optional 0|1>,
  "error_text": "<optional, set when error=1>"
}
```

Wichtige Eigenschaften:

- **Max. 50 Channels pro Sensor** (PRTG-Limit). Mehr Channels → Sensor wird abgelehnt.
- **Channels sind primitiv**: Integer oder Float. Strings nur für `text`.
- **`ValueLookup`** verweist auf eine vorinstallierte oder Custom-OVL-Datei. PRTG hat **eingebaute Lookups** wie `prtg.standardlookups.yesno.stateyesok` — wir nutzen die für "DB Health" (1 = OK), kein eigener Lookup-Install nötig.
- **`limitmaxerror=0`** + **`limitmode=1`** auf "Failed Stacks" macht den Sensor sofort rot, sobald mindestens ein Stack failed ist — ohne dass der Admin Schwellwerte konfiguriert.

### Welche Channels aus RSGO-Daten?

Die wichtigsten Aggregates auf einen Blick (bewusst unter dem 50-Channel-Limit gehalten — wir verdichten):

| Channel | Source | Unit | Lookup | Failure-Threshold |
|---------|--------|------|--------|-------------------|
| **Healthy stacks** | sum(operational stacks per product) | Count | — | — |
| **Degraded stacks** | sum(PartiallyRunning) | Count | — | `limitmaxwarning=0, limitmode=1` |
| **Failed stacks** | sum(Failed) | Count | — | **`limitmaxerror=0, limitmode=1`** |
| **Stopped stacks** | sum(Stopped) | Count | — | — |
| **Products total** | products without Removed/Superseded | Count | — | — |
| **Products in maintenance** | products with OperationMode=Maintenance | Count | — | — |
| **Services running** | sum(services where running=1) | Count | — | — |
| **Services not running** | sum(services where running=0) | Count | — | `limitmaxerror=0, limitmode=1` |
| **DB health** | rsgoSystemDbHealth (1=ok, 0=unknown, 2=fail) | Custom | `prtg.standardlookups.healthstate` | — |
| **Uptime** | rsgoSystemUptimeSeconds | TimeSeconds | — | — |
| **RSGO version** | embedded in `text` only | — | — | — |

Wenn das spätere Feedback nach mehr Granularität ruft (z. B. ein Channel pro Produkt), gibt es zwei Auswege ohne das 50-Channel-Limit zu sprengen:

1. **Query-Parameter-Filter**: `?productGroupId=ams-project` reduziert die Antwort auf ein einzelnes Produkt. Mehrere PRTG-Sensoren mit unterschiedlichen URLs → pro Produkt ein Sensor.
2. **Mehrere Endpoints**: `/api/integrations/prtg/products`, `/api/integrations/prtg/stacks`, `/api/integrations/prtg/system` — drei Sensoren statt einem.

Beides ist optional und kommt in einem Follow-up, sobald wir aus dem Feldeinsatz wissen, wonach gefragt wird.

### Auth

Maschineller Zugriff von einem PRTG-Probe-Service auf RSGO. **Zwei Auth-Pfade** je nach Geschmack:

1. **API-Key über Header** (`X-Api-Key: rsgo_xxx`) — Standard für CI/CD-Hooks, schon vorhanden. PRTG erlaubt das, aber das HTTP-Data-Advanced-Sensor-UI ist hier umständlich (man muss "Additional Headers" einzeln befüllen).
2. **API-Key über Query-Parameter** (`?apikey=rsgo_xxx`) — bequem in der PRTG-UI (alles in einer URL), aber Token landet in PRTG-Sensor-URLs und damit in PRTG-Logs.

→ **Empfohlen: beide unterstützen**, Header-basiert ist Standard, Query-Parameter ist die Convenience-Variante. Sicherheits-Implikation klar in der Doku ausweisen.

> **Wichtig**: Der Endpoint ist read-only und enthält keine Geheimnisse (Container-Versionen, Stack-Namen sind die "geheimsten" Werte). Token-Leak hätte denselben Impact wie ein kompromittierter `Settings:Read`-User — Lesezugriff auf Status. Kein RCE-Pfad.

### Caching

`SnmpSnapshotProvider` cached schon 30 s. Wir setzen denselben Wert für den HTTP-Endpoint — PRTG-Polling-Intervalle sind typischerweise 60 s, also liefert der Cache praktisch immer einen frischen Snapshot. Optionales `Cache-Control: max-age=15` als HTTP-Header für Reverse-Proxies.

### Betroffene Bounded Contexts

- **Domain** — keine Änderungen. Wir lesen nur bestehende Aggregates.
- **Application** — neuer Query `GetPrtgJsonStatusQuery` + Handler. Erweitert `SnmpSnapshotProvider` nicht selbst, sondern transformiert dessen Output in das PRTG-JSON-Schema. Eigenes `PrtgJsonStatusBuilder` (analog `PrtgBundleBuilder`).
- **Api** — neuer Endpoint `GET /api/integrations/prtg/status`. Permission `Snmp:Read` (gleicher Scope wie MIB-Download). Akzeptiert API-Key per Header **oder** Query-Parameter.
- **WebUI (rsgo-generic)** — neue Card "**PRTG HTTP Sensor**" auf der SNMP-Settings-Page. Zeigt:
  - Die fertige Sensor-URL inkl. Host (aus `Request.Host`) + Beispiel-API-Key-Platzhalter
  - "Generate API Key"-Button → leitet auf `/settings/cicd` mit Pre-Fill des Sensor-Scopes
  - 3-Schritt-Anleitung: API-Key erzeugen, URL kopieren, in PRTG "HTTP Data Advanced" Sensor anlegen + URL einfügen
- **Tests** — Unit-Tests für den Builder (Channel-Liste, Aggregation, leere Umgebung). Integration-Test für den Endpoint (RBAC, Cache-Header). E2E: UI-Card zeigt URL.

## AMS UI Counterpart

> RSGO has two UI distributions with different design systems:
> - **rsgo-generic**: React + Tailwind CSS (reference implementation, `packages/ui-generic`)
> - **AMS UI**: ConsistentUI/Lit web components (separate repo `ReadyStackGo.Ams`)
>
> Shared logic lives in `@rsgo/core` (hooks, API calls, state). Pages/layouts must be reimplemented per distribution.

**Benötigt AMS UI eine Entsprechung?**

- [x] **Ja** — die "PRTG HTTP Sensor"-Card muss auch in der AMS-Distribution erscheinen. Logik (`usePrtgSensorUrl(apiKey)`) wandert in `@rsgo/core`; UI-Section wird in ConsistentUI reimplementiert.

→ AMS-Counterpart-Plan: `C:\proj\ReadyStackGo.Ams\docs\Plans\PLAN-prtg-http-json-sensor.md` (anzulegen, wenn diese Variante umgesetzt wird).

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten:

- [ ] **Feature 1: PrtgJsonStatusBuilder** — Pure Service in `Application/Integrations/Prtg/`. Input: `SnmpSnapshot`. Output: stark typisiertes `PrtgJsonStatusResponse` (DTOs `PrtgEnvelope` / `PrtgResult` / `PrtgChannel`). Aggregation gemäß der Channel-Tabelle oben.
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/Integrations/Prtg/PrtgJsonStatusBuilder.cs`
    - `src/ReadyStackGo.Application/Integrations/Prtg/PrtgJsonModels.cs`
  - Abhängig von: -

- [ ] **Feature 2: GetPrtgJsonStatusQuery + Handler** — CQRS-Query. Handler lädt Snapshot via `ISnmpSnapshotProvider`, ruft Builder, liefert `PrtgJsonStatusResponse`.
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/Integrations/Prtg/GetPrtgJsonStatusQuery.cs`
    - `src/ReadyStackGo.Application/Integrations/Prtg/GetPrtgJsonStatusHandler.cs`
  - Abhängig von: Feature 1

- [ ] **Feature 3: REST-Endpoint** — `GET /api/integrations/prtg/status`. Permission `Snmp:Read`. Serialisierung mit `JsonNamingPolicy.CamelCase`. `Cache-Control: max-age=15`-Header.
  - **API-Key-Support**: akzeptiert sowohl `X-Api-Key`-Header (Standard, via bestehendem `ApiKeyAuthenticationHandler`) als auch `?apikey=`-Query-Parameter. Letzteres ist ein neues Auth-Sub-Schema, das nur für diesen Endpoint gilt (siehe `PRTG-Standard-Pattern`).
  - Betroffene Dateien:
    - `src/ReadyStackGo.Api/Endpoints/Integrations/GetPrtgJsonStatusEndpoint.cs`
    - `src/ReadyStackGo.Infrastructure/Security/Authentication/QueryStringApiKeyHandler.cs` (oder Extension am bestehenden `ApiKeyAuthenticationHandler`)
  - Abhängig von: Feature 2

- [ ] **Feature 4: Core-Helper** — `getPrtgSensorUrl(apiKey: string)` in `@rsgo/core/src/api/integrations.ts` baut die vollständige URL `{origin}/api/integrations/prtg/status?apikey={apiKey}` zusammen (mit `encodeURIComponent`).
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/packages/core/src/api/integrations.ts`
  - Abhängig von: Feature 3

- [ ] **Feature 5: WebUI-Card "PRTG HTTP Sensor"** — Section auf der SNMP-Settings-Page mit:
  - URL-Anzeige + Copy-Button (zeigt URL mit Platzhalter, wenn kein Key vorausgewählt)
  - "Create API Key for PRTG"-Link (öffnet `/settings/cicd` mit Pre-Filled-Name)
  - 3-Schritt-Anleitung
  - Live-Test-Button: "Test Sensor" macht einen Browser-Request gegen die URL und zeigt das gerenderte JSON
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Settings/Snmp/SnmpSettingsPage.tsx` (Section einhängen)
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Settings/Snmp/PrtgHttpSensorCard.tsx`
  - Abhängig von: Feature 4

- [ ] **Feature 6: Public Website Doc** — Neue Sub-Seite `monitoring/snmp/prtg-http-sensor` (DE + EN). Inhalt:
  - Wann nimmt man Variant 4 vs. Variant 1? (Tabelle aus dem Abschnitt "Wann passt welche Variante" unten)
  - Schritt-für-Schritt-Setup mit Screenshots (Sensor anlegen in PRTG)
  - JSON-Schema-Referenz + Channel-Tabelle
  - Troubleshooting
  - Verlinkung von der SNMP-Hauptseite und der Variant-1-Seite
  - Betroffene Dateien:
    - `src/ReadyStackGo.PublicWeb/src/content/docs/de/docs/monitoring/snmp/prtg-http-sensor.md`
    - `src/ReadyStackGo.PublicWeb/src/content/docs/en/docs/monitoring/snmp/prtg-http-sensor.md`

- [ ] **Phase abschließen** — `dotnet build` 0 errors / 0 warnings, alle Tests grün, manueller End-to-End-Test gegen eine echte PRTG-Instanz (siehe "Manueller Test" unten), PR gegen main.

## Test-Strategie

- **Unit-Tests** (`PrtgJsonStatusBuilder`):
  - Leerer Snapshot (keine Produkte) → konsistente Antwort mit allen Channels = 0
  - Snapshot mit 1× Running + 1× Failed Produkt → `Failed stacks > 0`, `text` enthält beide Produktnamen
  - Maintenance-Mode → `Products in maintenance = 1`, `Failed stacks = 0` (Maintenance darf nicht als Failure zählen)
  - 50-Channel-Limit-Check: Builder liefert nie mehr als 50 Channels (Defense in Depth gegen Future-Bugs).
- **Integration-Test** (Endpoint):
  - GET ohne API-Key → 401
  - GET mit `X-Api-Key`-Header → 200 + valid PRTG-JSON
  - GET mit `?apikey=`-Query-Parameter → 200
  - Response enthält `Cache-Control: max-age=15`
  - Response-Schema gegen ein `prtg-schema.json` validieren (zur Sicherheit gegen unbeabsichtigte Brüche)
- **E2E** (Playwright):
  - PRTG-HTTP-Sensor-Card sichtbar auf `/settings/snmp`
  - "Test Sensor"-Button rendert JSON-Vorschau im Modal
- **Manueller End-to-End-Test** (Acceptance):
  - In PRTG einen Device anlegen, Sensor "HTTP Data Advanced" hinzufügen, URL einfügen
  - Innerhalb von 60 s sollten mindestens 8 Channels Werte zeigen
  - Failed-Channel testet sich selbst: ein Produkt-Deployment mutwillig kaputt machen → PRTG-Sensor flippt nach max. 90 s auf rot

## Offene Punkte

- [ ] Soll der Endpoint **mehrere** Antworten unterstützen (z. B. via `Accept`-Header)? Z. B. `application/vnd.prtg+json` vs. `application/vnd.checkmk+json` für Multi-Tool-Support. → Vermutlich Overkill, kommt später als separater Plan, wenn ein anderes Tool angefragt wird.
- [ ] Brauchen wir per-Product- oder per-Environment-Filter (`?productGroupId=...`)? Erste Iteration **global only** ausliefern; Feedback abwarten.
- [ ] Soll die "Test Sensor"-Vorschau in der UI wirklich live gegen den Endpoint gehen, oder einen Mock-Snapshot rendern? Live ist ehrlicher (man sieht real-time-Daten), Mock ist robuster (funktioniert auch ohne API-Key).

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Auth-Mechanismus | Header / Query / beide | **Beide unterstützen** | Header ist Standard, Query ist die PRTG-UI-bequeme Variante. Ein-Klick-Setup geht nur mit Query-Param. |
| Channel-Granularität | Global aggregiert / Per-Product | **Global aggregiert (V1)** | 50-Channel-Limit + "möglichst niedrigschwellig" — Detail-Granularität liefert Variant 1. |
| Cache-TTL | 0s / 15s / 30s | **30s (Provider-Cache) + 15s `Cache-Control`** | Reuse des bestehenden SnmpSnapshot-Caches; matched PRTG-typische Polling-Intervalle. |
| URL-Pfad | `/api/snmp/prtg-status` / `/api/integrations/prtg/status` | **`/api/integrations/prtg/status`** | Setzt den Namespace "Integrations" für künftige Zabbix/Checkmk/Datadog-Endpoints frei. |
| Permission | `Snmp:Read` / neu `Monitoring:Read` | **`Snmp:Read`** | Konsistent mit MIB- und PRTG-Bundle-Endpoint, kein neuer Permission-String. |
| IANA-PEN-Abhängigkeit | blockt / unabhängig | **Unabhängig** | HTTP-JSON kennt keine OIDs — Variant 4 ist von der PEN-Migration komplett entkoppelt. |

## Wann passt welche Variante?

| Szenario | Variante | Setup-Aufwand | Granularität |
|----------|----------|---------------|--------------|
| "Mal kurz schauen, ob alles läuft" — kein Vor-Setup wert | **Variant 4 (HTTP-JSON)** | 5 Min in der PRTG-UI | Aggregated (1 Sensor mit ~10 Channels) |
| Festes Monitoring-Setup, ein Klick für jedes neue Produkt | **Variant 1 (Device Template)** | 1× Admin-Install pro PRTG-Server | Pro-Produkt / Pro-Stack / Pro-Service Tabellen |
| RSGO soll PRTG aktiv informieren statt PRTG zu pollen | **Variant 2 (Active Push)** | Pro Deployment: URL+Token | Pro-Deployment-Device |
| Multi-PRTG, zentrale Connection-Verwaltung in RSGO | **Variant 3 (PrtgConnection Resource)** | Einmalige Connection-Anlage in RSGO-Settings | Pro-Deployment-Device, aus zentraler Connection |

→ **Variant 4 ist die explizite Einstiegsdroge**. Sie ersetzt Variant 1 nicht, sondern senkt die Einstiegshürde für Nutzer, die heute mit der Variant-1-Ceremonie überfordert sind. Ein RSGO-Setup kann beide nebeneinander betreiben (HTTP-JSON-Sensor als Gesamtüberblick, Device-Template für Detail-Sensoren).

## Referenzen

- Variant 1 — Device Template + MIB: [PLAN-prtg-mib-template.md](PLAN-prtg-mib-template.md)
- Variant 2 — Active Push: [PLAN-prtg-api-push.md](PLAN-prtg-api-push.md)
- Variant 3 — PrtgConnection Resource: [PLAN-prtg-connection-resource.md](PLAN-prtg-connection-resource.md)
- Paessler-Doku: [Custom Sensors via JSON (HTTP Data Advanced)](https://www.paessler.com/manuals/prtg/custom_sensors)
- Standard-Lookups in PRTG: [Lookups Reference](https://www.paessler.com/manuals/prtg/define_lookups#define_lookups__standard_lookups)
