<!-- GitHub Epic: #377 -->
# Phase: SNMP Agent for Deployment Monitoring (v0.64)

## Ziel

ReadyStackGo wird ein abfragbarer SNMP-Agent. Externe Monitoring-Systeme (Nagios, Zabbix, PRTG, LibreNMS, Icinga, ...) können den Zustand von ProductDeployments, einzelnen Stacks, Container-Health und der RSGO-Instanz selbst per Standard-SNMP-Polling (GET / GETNEXT / WALK) abfragen — sowohl über SNMPv2c (community-based) als auch SNMPv3 (user-based mit auth+priv).

Scope ist explizit **read-only**: keine SET-Operationen, keine Traps. Beides ist als Follow-up-Phase vorgesehen, aber für v0.64 raus aus dem Scope, damit das MVP klein und in einer Phase auslieferbar bleibt.

## Analyse

### Bestehende Architektur

- **ProductDeployment-Aggregate** ([ProductDeployment.cs](../../src/ReadyStackGo.Domain/Deployment/ProductDeployments/ProductDeployment.cs)) hält bereits alles, was wir exposen wollen: `Status`, `OperationMode`, `TotalStacks`, `CompletedStacks`, `FailedStacks`, `CompletedAt`, `ErrorMessage`, plus die `_stacks`-Child-Liste.
- **ProductDeploymentRepository** ([IProductDeploymentRepository.cs](../../src/ReadyStackGo.Domain/Deployment/ProductDeployments/IProductDeploymentRepository.cs)) bietet `GetAllActive()` — exakt was der SNMP-Snapshot-Builder pro Walk braucht.
- **HealthCollectorBackgroundService** ([HealthCollectorBackgroundService.cs](../../src/ReadyStackGo.Api/BackgroundServices/HealthCollectorBackgroundService.cs)) sammelt bereits Container-Health periodisch ein und legt sie als `HealthSnapshot`s ab. Diese Snapshots speisen wir in den SNMP-Service-Tree.
- **BackgroundService-Pattern**: Es gibt sechs etablierte `BackgroundService`s ([src/ReadyStackGo.Api/BackgroundServices/](../../src/ReadyStackGo.Api/BackgroundServices/)). Der SNMP-Agent wird das siebte Exemplar im selben Muster (DI über IServiceProvider-Scope, periodischer Loop optional, hier eher Long-running Listener).
- **Settings-Persistence** — Settings für Cert/Renewal/etc. werden heute teils in `appsettings.json` und teils in der `SystemSettings`-DB abgelegt. SNMP-Settings (Port, Enable, Community, v3-Users) brauchen DB-Persistenz weil sie online über die WebUI bearbeitbar sein müssen — das schließt sich an das `SystemSettings`-Muster an, ggf. mit eigener Tabelle für v3-Users (Hashes nie im Klartext).
- **Notification/Settings UI** — Settings-Seite existiert in [packages/ui-generic/src/pages/Settings/](../../src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Settings/) mit Tabs/Sections. SNMP bekommt einen neuen Section dort.

### OID-Strategie (vorerst experimentell)

Wir nutzen den Platzhalter-Root `1.3.6.1.4.1.99999.1` (`rsgoRoot`). Eine offizielle IANA Private Enterprise Number wird vor v1.0 beantragt; der Platzhalter wird dann durch die zugewiesene PEN ersetzt (Breaking Change für frühe Customers, dokumentiert in den Release Notes).

Der Root macht den Tree leicht konfigurierbar — alle untergeordneten OIDs sind relativ zu einer per `appsettings.json` überschreibbaren `RootOid`-Konstante aufgebaut, sodass Customers mit eigener PEN sie auf ihre Wurzel mappen können.

### OID-Tree (initial)

```
rsgoRoot               1.3.6.1.4.1.99999.1
├── rsgoSystem         .1
│   ├── rsgoSystemVersion        .1.0  STRING       — RSGO version string ("v0.64.0")
│   ├── rsgoSystemUptime         .2.0  TimeTicks    — Process uptime in 1/100 s
│   ├── rsgoSystemEnvironmentCount  .3.0  Integer32 — Configured environments
│   ├── rsgoSystemSourceCount    .4.0  Integer32   — Stack sources
│   ├── rsgoSystemDbHealth       .5.0  Integer32   — 0 = down, 1 = up
│   └── rsgoSystemBuildTimestamp .6.0  DateAndTime — Image build time
│
├── rsgoProductTable   .2.1
│   └── rsgoProductEntry         .1   INDEX = rsgoProductIndex
│       ├── rsgoProductIndex            .1   Integer32 INDEX
│       ├── rsgoProductId               .2   STRING
│       ├── rsgoProductName             .3   STRING
│       ├── rsgoProductVersion          .4   STRING
│       ├── rsgoProductEnvironment      .5   STRING
│       ├── rsgoProductStatus           .6   Integer32  — see PRODUCT-STATUS enum
│       ├── rsgoProductStatusText       .7   STRING
│       ├── rsgoProductOperationMode    .8   Integer32  — 0=Normal, 1=Maintenance
│       ├── rsgoProductTotalStacks      .9   Integer32
│       ├── rsgoProductRunningStacks    .10  Integer32
│       ├── rsgoProductFailedStacks     .11  Integer32
│       ├── rsgoProductLastDeployedAt   .12  DateAndTime
│       └── rsgoProductErrorMessage     .13  STRING
│
├── rsgoStackTable     .3.1
│   └── rsgoStackEntry           .1   INDEX = rsgoProductIndex, rsgoStackIndex
│       ├── rsgoStackIndex              .1  Integer32 INDEX
│       ├── rsgoStackProductIndex       .2  Integer32 INDEX (FK to rsgoProductIndex)
│       ├── rsgoStackName               .3  STRING
│       ├── rsgoStackStatus             .4  Integer32  — see STACK-STATUS enum
│       ├── rsgoStackStatusText         .5  STRING
│       ├── rsgoStackServiceCount       .6  Integer32
│       ├── rsgoStackOrder              .7  Integer32
│       └── rsgoStackErrorMessage       .8  STRING
│
└── rsgoServiceTable   .4.1
    └── rsgoServiceEntry         .1   INDEX = rsgoProductIndex, rsgoStackIndex, rsgoServiceIndex
        ├── rsgoServiceIndex            .1  Integer32 INDEX
        ├── rsgoServiceStackIndex       .2  Integer32 INDEX (FK)
        ├── rsgoServiceProductIndex     .3  Integer32 INDEX (FK)
        ├── rsgoServiceName             .4  STRING
        ├── rsgoServiceContainerName    .5  STRING
        ├── rsgoServiceRunning          .6  Integer32 — 0/1
        ├── rsgoServiceHealthStatus     .7  Integer32 — 0=Unknown, 1=Healthy, 2=Unhealthy, 3=Starting
        ├── rsgoServiceRestartCount     .8  Counter32
        └── rsgoServiceLastHealthCheck  .9  DateAndTime
```

Enum-Mappings (`PRODUCT-STATUS`, `STACK-STATUS`, `HEALTH-STATUS`) liegen 1:1 auf den bestehenden Domain-Enums; die MIB-Datei dokumentiert die Werte als `INTEGER { running(1), partiallyRunning(2), failed(3), ... }`.

### Betroffene Bounded Contexts

- **Domain** — keine neuen Entities. Nur ein neues Value-Object `SnmpV3User(Name, AuthAlgo, AuthSecretHash, PrivAlgo, PrivSecretHash)` mit Salting; alle Status-Mappings sind reine Read-Transformationen.
- **Application** — neuer Query/Provider `SnmpSnapshotProvider` der pro Walk einen konsistenten Snapshot baut (Product-/Stack-/Service-Tabellen). Außerdem `IsnmpSettingsService` für CRUD auf SNMP-Konfiguration und v3-Usern.
- **Infrastructure** — neuer Service `SnmpAgent` der `Lextm.SharpSnmpLib` hostet (UDP-Listener, Message-Dispatcher, OID-Lookup). Neue EF-Configuration für `SnmpV3User`-Tabelle.
- **API** — `SnmpAgentBackgroundService` startet/stoppt den Agent abhängig von Settings. REST-Endpoints unter `/api/snmp/*` für Settings-UI (GET/PUT settings, GET/POST/DELETE v3 users, GET `/mib` für Download).
- **WebUI (rsgo-generic)** — neue Settings-Section "SNMP" (Enable, Port, Listen Address, Community String, v3-User-Liste mit Add/Remove). Pages: ein neuer `SnmpSettingsPage.tsx` plus Section-Eintrag im bestehenden Settings-Layout.

## AMS UI Counterpart

- [x] **Teilweise** — die SNMP-Settings-Section sollte auch in der AMS UI auftauchen, damit Admins beider Distributionen die SNMP-Konfiguration zugänglich haben. Domain/API ist gemeinsam (über `@rsgo/core`), nur die UI-Section muss in ConsistentUI-Komponenten reimplementiert werden.

→ AMS-Counterpart-Plan: nach Issue-Erstellung in `C:\proj\ReadyStackGo.Ams\docs\Plans\PLAN-snmp-settings-ui.md` anlegen (Folgeaufgabe — wir blocken den RSGO-Plan damit nicht).

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten:

- [ ] **Feature 1: SNMP Listener Backbone** — Lextm.SharpSnmpLib einbinden, `SnmpAgent`-Service in `Infrastructure`, hostet UDP-Listener auf konfigurierbarem Port (Default 161, im Container hochmappbar). Erst ohne echte OIDs — antwortet mit `noSuchObject` auf alles. End-to-End-Pfad damit erstmal stehend.
  - Betroffene Dateien: neue `src/ReadyStackGo.Infrastructure/Snmp/SnmpAgent.cs`, `Snmp/IOidTree.cs`, `Snmp/OidTreeBuilder.cs` (stub). `src/ReadyStackGo.Api/BackgroundServices/SnmpAgentBackgroundService.cs`.
  - Pattern-Vorlage: [HealthCollectorBackgroundService.cs](../../src/ReadyStackGo.Api/BackgroundServices/HealthCollectorBackgroundService.cs) für DI-Scope + Cancellation-Handling.
  - Abhängig von: -

- [ ] **Feature 2: OID Tree + Snapshot Provider** — `SnmpSnapshotProvider` in `Application` baut pro Walk-Request einen konsistenten Read-Snapshot (Products-, Stacks-, Services-Tabellen) aus `ProductDeploymentRepository` und HealthSnapshots. `OidTreeBuilder` mapped Snapshot → SharpSnmp-`ObjectIdentifier`s.
  - Betroffene Dateien: `src/ReadyStackGo.Application/Snmp/SnmpSnapshotProvider.cs`, `Snmp/SnmpSnapshot.cs`, plus Tests.
  - Abhängig von: Feature 1

- [ ] **Feature 3: SNMPv2c Community-String Auth** — Settings-Storage in DB (`SnmpSettings`-Entity), Validierung im Listener (Reject mit `authenticationFailure`-Counter). Listener akzeptiert v2c-PDUs nur wenn Community matched. Default-Community ist nicht gesetzt → Agent läuft erst nach expliziter Konfiguration.
  - Betroffene Dateien: `Domain/Snmp/SnmpSettings.cs`, `Infrastructure.DataAccess/Configurations/SnmpSettingsConfiguration.cs`, EF-Migration, plus Tests.
  - Abhängig von: Feature 1, 2

- [ ] **Feature 4: SNMPv3 User Storage + Auth/Priv** — `SnmpV3User`-Tabelle (Name, AuthAlgo: SHA-1/SHA-256, AuthSecretHash; PrivAlgo: AES-128/AES-256, PrivSecretHash). Hashing nach RFC 3414 (key localization). SharpSnmp `User`-Konfiguration aus DB beim Agent-Start. Tests gegen `snmpwalk -v3 -u <user> -l authPriv -a SHA -A <pass> -x AES -X <pass>`.
  - Betroffene Dateien: `Domain/Snmp/SnmpV3User.cs`, `Application/Snmp/SnmpV3UserService.cs`, EF-Migration, plus Tests.
  - Abhängig von: Feature 1, 3

- [ ] **Feature 5: REST-Endpoints für Settings/Users** — FastEndpoints unter `/api/snmp/settings` (GET, PUT), `/api/snmp/v3-users` (GET, POST, DELETE), `/api/snmp/mib` (GET, liefert MIB-Datei-Inhalt). Permission `Settings:Manage` über bestehenden `RbacPreProcessor`.
  - Betroffene Dateien: `src/ReadyStackGo.Api/Endpoints/Snmp/*Endpoint.cs`, Application-Layer Commands/Queries.
  - Abhängig von: Feature 3, 4

- [ ] **Feature 6: WebUI SNMP-Settings-Section** — Enable-Toggle, Port-Input, Community-Input (mit Sichtbarkeits-Toggle), v3-User-Liste mit Add-Dialog und Delete-Action, MIB-Download-Button. Folgt dem bestehenden Settings-Section-Muster ([packages/ui-generic/src/pages/Settings/](../../src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Settings/)).
  - Betroffene Dateien: `packages/ui-generic/src/pages/Settings/SnmpSettings.tsx` plus Hook in `packages/core/src/hooks/useSnmpSettings.ts` und API in `packages/core/src/api/snmp.ts`.
  - Abhängig von: Feature 5

- [ ] **Feature 7: MIB-File Generation** — Hand-curated MIB-Datei (`READYSTACKGO-MIB.txt`) als Embedded Resource, plus Endpoint zum Download. Validierung mit `smilint` als Teil von CI (Build-Step).
  - Betroffene Dateien: `src/ReadyStackGo.Api/Resources/READYSTACKGO-MIB.txt`, CI-Job-Erweiterung.
  - Abhängig von: Feature 2 (OID-Tree finalisiert)

- [ ] **Dokumentation & Website**
  - Public Website DE/EN: Reference-Page "SNMP Monitoring" mit OID-Liste, Auth-Setup, MIB-Download-Link, Beispiel-snmpwalks für die häufigsten Tools.
  - Docs/Architecture-Notiz dass v0.64 SNMP read-only liefert und Traps/PEN-Wechsel separate Follow-up-Phasen sind.
  - Roadmap-Update.

- [ ] **Phase abschließen** — alle Tests grün, dotnet build 0 errors / 0 warnings, manuelles E2E-Smoketest mit `snmpwalk` aus einem zweiten Container, PR gegen main.

## Test-Strategie

- **Unit Tests**:
  - `SnmpSnapshotProvider` — Mapping Domain → Snapshot (Status-Enums, Counts, leere Tabellen, null-safe Fields).
  - `OidTreeBuilder` — GETNEXT-Iteration über Tabellen, lexikalische OID-Ordnung, INDEX-Encoding.
  - `SnmpSettings` / `SnmpV3User` Domain-Invarianten (Port-Range, Algo-Whitelist, Mindest-Schlüssellängen).

- **Integration Tests**:
  - Agent gegen lokalen UDP-Loopback. `snmpsharpnet` (im Test als Client-Lib) führt GET/GETNEXT/WALK aus, Assertions auf zurückgegebene Werte.
  - v2c-Auth: korrekter Community akzeptiert, falscher abgelehnt.
  - v3-Auth: SHA+AES Roundtrip, Replay-Window.
  - Settings-Endpoints: RBAC-PreProcessor blockt nicht-Admin.

- **E2E Tests** (Playwright + Container):
  - Settings-Section: SNMP enablen, Community setzen, v3-User anlegen.
  - Sidecar-Container mit `net-snmp` CLI führt `snmpwalk -v2c -c <community> rsgo` und `snmpwalk -v3 -u <user> ...` aus; Output enthält erwartete Produkte/Stacks/Services.

## Offene Punkte

- [ ] **Port-Mapping im Default-`docker-compose.yml`** — UDP/161 ist privileged. Default soll der Container intern auf 161 lauschen, im Compose-File auf z.B. `1161:161/udp` mappen, damit der Host-Port nicht root-only ist. Dokumentation muss das klar machen, weil Monitoring-Tools dann `:1161` ansprechen müssen oder per `iptables` weitergeleitet werden.
- [ ] **Snapshot-Caching** — pro Walk Snapshot-Build vs. einmal pro X Sekunden cachen? Mit 200+ Stacks könnte ein Walk DB-lastig werden. Tendenz: 5s-Cache reicht für Monitoring (Polling-Intervalle sind typisch ≥ 30s).
- [ ] **Mehrere Environments** — `rsgoProductEnvironment` ist ein Plain-String-Feld; tooling-freundlicher wäre eine zweite Indexspalte. Vorläufig String, Optimierung in Follow-up wenn Customer-Feedback es einfordert.
- [ ] **MIB-Datei-Validierung in CI** — `smilint`-Verfügbarkeit auf dem CI-Runner prüfen; Fallback: Validierung in einem separaten Lint-Job, der nicht den Build blockt.

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Protokoll-Versionen | v2c only / v2c+v3 / v3 only | **v2c + v3** | Customer-Antwort vom 2026-05-19. v2c für Legacy-Tools, v3 für Compliance-Umgebungen. |
| Funktionsumfang | RO Polling / Polling+Traps / Polling+Traps+SET | **RO Polling** | Customer-Antwort. SET ist Sicherheitsrisiko; Traps sind separater Scope für eine Folge-Phase. |
| Daten-Coverage | nur Products / +Stacks / +Services / +System | **Alle vier Layer** | Customer-Antwort. Mehraufwand ist gering, weil Domain ohnehin alle Daten exponiert. |
| OID-Root | PEN sofort / Experimentell+später PEN / per-Config | **Experimentell + per-Config** | Customer-Antwort. Wir nutzen `1.3.6.1.4.1.99999.1` als Default, machen die Wurzel aber konfigurierbar — Customers mit eigener PEN können sofort umstellen. PEN-Antrag läuft parallel. |
| SNMP-Library | Lextm.SharpSnmpLib / SnmpSharpNet / eigene Impl | **Lextm.SharpSnmpLib** | MIT-lizenziert, aktiv gewartet, voller v1/v2c/v3-Support, Agent + Manager APIs vorhanden. |
| Milestone | v0.63 / v0.64 (neu) / v1.0 | **v0.64 (neu)** | Customer-Antwort. v0.63 ist für Product Updates reserviert; v1.0 ist zu spät für ein Standalone-Feature. v0.64 wird der SNMP-Milestone, ggf. plus AMS-UI-Counterpart. |
