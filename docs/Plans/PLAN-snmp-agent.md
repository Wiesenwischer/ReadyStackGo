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

### Listener-Port und Container-Hardening

SNMP läuft traditionell auf UDP/161 — ein privilegierter Port (< 1024), der auf Linux-Hosts und in non-root-Containern (OpenShift restricted SCC, K8s `runAsNonRoot`, gehärtete RHEL-Pods) nicht ohne CAP_NET_BIND_SERVICE bindbar ist. Industry-Best-Practice ist deshalb: Container intern auf einem Non-Privileged-Port lauschen lassen, die Klassik-Portierung außen entscheidet der Operator.

**RSGO-Default**: Container hört intern auf **UDP/1161**. Der Default-`docker-compose.yml` mappt `1161:1161/udp`. Damit ist der Container "rootless-ready" — er kann ohne Änderung als Non-Root-User laufen.

Operator-Optionen sind dokumentiert:
- Standard-Port von außen: `docker-compose.override.yml` mit `"161:1161/udp"` — der Docker-Daemon bindet host:161 (er hat root, das geht). Monitoring-Tools sprechen den klassischen Port an.
- Firewall-Redirect: `iptables -t nat -A PREROUTING -p udp --dport 161 -j REDIRECT --to-port 1161`.
- Kubernetes/OpenShift: Service mit `port: 161, targetPort: 1161`.

### OID-Tree

Environment ist eigener INDEX, damit Monitoring-Tools per `snmpwalk rsgoEnvironmentTable.<idx>` direkt eine Umgebung auswählen können. Die Numerierung ist gegenüber dem ersten Entwurf verschoben (Environment ist jetzt `.2`, Product `.3`, Stack `.4`, Service `.5`).

```
rsgoRoot                    1.3.6.1.4.1.99999.1
├── rsgoSystem              .1
│   ├── rsgoSystemVersion           .1.0  STRING       — RSGO version string ("v0.64.0")
│   ├── rsgoSystemUptime            .2.0  TimeTicks    — Process uptime in 1/100 s
│   ├── rsgoSystemEnvironmentCount  .3.0  Integer32    — Configured environments
│   ├── rsgoSystemSourceCount       .4.0  Integer32    — Stack sources
│   ├── rsgoSystemDbHealth          .5.0  Integer32    — 0 = down, 1 = up
│   └── rsgoSystemBuildTimestamp    .6.0  DateAndTime  — Image build time
│
├── rsgoEnvironmentTable    .2.1
│   └── rsgoEnvironmentEntry        .1    INDEX = rsgoEnvironmentIndex
│       ├── rsgoEnvironmentIndex          .1  Integer32 INDEX
│       ├── rsgoEnvironmentId             .2  STRING (UUID)
│       ├── rsgoEnvironmentName           .3  STRING
│       └── rsgoEnvironmentType           .4  Integer32  — 1=LocalDocker, 2=SshTunnel, 3=DockerTcp, 4=RemoteAgent
│
├── rsgoProductTable        .3.1
│   └── rsgoProductEntry            .1    INDEX = rsgoEnvironmentIndex, rsgoProductIndex
│       ├── rsgoEnvironmentIndex          .1   Integer32 INDEX (FK)
│       ├── rsgoProductIndex              .2   Integer32 INDEX
│       ├── rsgoProductId                 .3   STRING
│       ├── rsgoProductName               .4   STRING
│       ├── rsgoProductVersion            .5   STRING
│       ├── rsgoProductStatus             .6   Integer32  — see PRODUCT-STATUS enum
│       ├── rsgoProductStatusText         .7   STRING
│       ├── rsgoProductOperationMode      .8   Integer32  — 0=Normal, 1=Maintenance
│       ├── rsgoProductTotalStacks        .9   Integer32
│       ├── rsgoProductRunningStacks      .10  Integer32
│       ├── rsgoProductFailedStacks       .11  Integer32
│       ├── rsgoProductLastDeployedAt     .12  DateAndTime
│       └── rsgoProductErrorMessage       .13  STRING
│
├── rsgoStackTable          .4.1
│   └── rsgoStackEntry              .1    INDEX = rsgoEnvironmentIndex, rsgoProductIndex, rsgoStackIndex
│       ├── rsgoEnvironmentIndex          .1  Integer32 INDEX (FK)
│       ├── rsgoProductIndex              .2  Integer32 INDEX (FK)
│       ├── rsgoStackIndex                .3  Integer32 INDEX
│       ├── rsgoStackName                 .4  STRING
│       ├── rsgoStackStatus               .5  Integer32  — see STACK-STATUS enum
│       ├── rsgoStackStatusText           .6  STRING
│       ├── rsgoStackServiceCount         .7  Integer32
│       ├── rsgoStackOrder                .8  Integer32
│       └── rsgoStackErrorMessage         .9  STRING
│
└── rsgoServiceTable        .5.1
    └── rsgoServiceEntry            .1    INDEX = rsgoEnvironmentIndex, rsgoProductIndex, rsgoStackIndex, rsgoServiceIndex
        ├── rsgoEnvironmentIndex          .1  Integer32 INDEX (FK)
        ├── rsgoProductIndex              .2  Integer32 INDEX (FK)
        ├── rsgoStackIndex                .3  Integer32 INDEX (FK)
        ├── rsgoServiceIndex              .4  Integer32 INDEX
        ├── rsgoServiceName               .5  STRING
        ├── rsgoServiceContainerName      .6  STRING
        ├── rsgoServiceRunning            .7  Integer32 — 0/1
        ├── rsgoServiceHealthStatus       .8  Integer32 — 0=Unknown, 1=Healthy, 2=Unhealthy, 3=Starting
        ├── rsgoServiceRestartCount       .9  Counter32
        └── rsgoServiceLastHealthCheck    .10 DateAndTime
```

Enum-Mappings (`PRODUCT-STATUS`, `STACK-STATUS`, `HEALTH-STATUS`, `ENVIRONMENT-TYPE`) liegen 1:1 auf den bestehenden Domain-Enums; die MIB-Datei dokumentiert die Werte als `INTEGER { running(1), partiallyRunning(2), failed(3), ... }`.

### Snapshot-Caching

Pro WALK können je nach Datenmenge 2000+ einzelne SNMP-Requests in wenigen Sekunden eintreffen — alle auf denselben konsistenten Tree-Stand bezogen. Wir bauen deshalb **alle 30 Sekunden** einen vollständigen Snapshot aus den Domain-Repositories und HealthSnapshots auf und bedienen Requests aus diesem Read-Model. Vorteile:
- DB-Last wird auch bei mehreren parallel pollenden Monitoring-Tools auf einen zentralen Build alle 30 s reduziert.
- Ein laufender WALK sieht garantiert einen in sich konsistenten Stand (kein "Stack 3 ist Running aber Service Count zeigt 0", was bei Inflight-Deploys passieren könnte).
- 30 s ist mit allen üblichen Monitoring-Polling-Intervallen (≥ 30 s) kompatibel; bei kürzeren Polls fragt das Tool denselben Snapshot mehrfach ab — kein Problem.

Trade-off dokumentieren: Bei schnellen Statuswechseln (Deploy gerade abgeschlossen) sieht das Monitoring bis zu 30 s lang noch den vorherigen Stand. Für klassisches Health-Polling akzeptabel; falls Customer-Feedback drängt, kann der TTL in einer Folge-Phase konfigurierbar gemacht werden.

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

- [ ] **Feature 1: SNMP Listener Backbone** — Lextm.SharpSnmpLib einbinden, `SnmpAgent`-Service in `Infrastructure`, hostet UDP-Listener auf konfigurierbarem Port (Default UDP/1161, non-privileged → rootless-tauglich; im Compose-File auf `1161:1161/udp` gemappt, optional vom Operator auf `161:1161/udp` re-mappbar). Erst ohne echte OIDs — antwortet mit `noSuchObject` auf alles. End-to-End-Pfad damit erstmal stehend.
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

Alle initial offenen Punkte sind in den Entscheidungs-Block weiter unten überführt. Neue offene Punkte entstehen erst bei der Umsetzung der Features.

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Protokoll-Versionen | v2c only / v2c+v3 / v3 only | **v2c + v3** | Customer-Antwort vom 2026-05-19. v2c für Legacy-Tools, v3 für Compliance-Umgebungen. |
| Funktionsumfang | RO Polling / Polling+Traps / Polling+Traps+SET | **RO Polling** | Customer-Antwort. SET ist Sicherheitsrisiko; Traps sind separater Scope für eine Folge-Phase. |
| Daten-Coverage | nur Products / +Stacks / +Services / +System | **Alle vier Layer** | Customer-Antwort. Mehraufwand ist gering, weil Domain ohnehin alle Daten exponiert. |
| OID-Root | PEN sofort / Experimentell+später PEN / per-Config | **Experimentell + per-Config** | Customer-Antwort. Wir nutzen `1.3.6.1.4.1.99999.1` als Default, machen die Wurzel aber konfigurierbar — Customers mit eigener PEN können sofort umstellen. PEN-Antrag läuft parallel. |
| SNMP-Library | Lextm.SharpSnmpLib / SnmpSharpNet / eigene Impl | **Lextm.SharpSnmpLib** | MIT-lizenziert, aktiv gewartet, voller v1/v2c/v3-Support, Agent + Manager APIs vorhanden. |
| Milestone | v0.63 / v0.64 (neu) / v1.0 | **v0.64 (neu)** | Customer-Antwort. v0.63 ist für Product Updates reserviert; v1.0 ist zu spät für ein Standalone-Feature. v0.64 wird der SNMP-Milestone, ggf. plus AMS-UI-Counterpart. |
| Listener-Port intern | 161 (Standard) / 1161 (non-priv) / konfigurierbar | **1161 intern, 1161:1161 Default-Mapping** | Industry-Best-Practice analog nginx-rootless, postgres etc. Container kann rootless betrieben werden (RHEL/OpenShift-kompatibel). Operator kann via `docker-compose.override.yml` auf `161:1161` re-mappen wenn klassischer Port von außen gewünscht. |
| Snapshot-Caching | Live / 5s / 30s / konfigurierbar | **30 Sekunden** | Maximaler Schutz für große Installationen (200+ Stacks). Polling-Tools mit Standard-Intervallen (≥ 30s) merken nichts. Trade-off (Latenz nach Statuswechseln) ist akzeptabel; Konfigurierbarkeit kann in einer Folge-Phase nachgezogen werden, wenn Customer-Feedback es einfordert. |
| Environment-Adressierung | String-Spalte / eigener Index / beides | **Eigener `rsgoEnvironmentTable`-Index** | Erlaubt Monitoring-Tools sauberes `snmpwalk` pro Environment, ohne im Tool nachfiltern zu müssen. Multi-Env-Setups werden first-class statt nachträglich. Mehraufwand im OID-Tree (~30%) ist überschaubar und einmalig. |
| MIB-Lint in CI | Blocking smilint / non-blocking / manuell | **Blocking smilint im Build-Step** | MIB-Fehler dürfen nie zum Customer durchkommen — kaputte Imports sind teuer im Support. smilint-Setup ist einmalig (apt-get). Bei MIB-Edits (selten) bleibt CI als Sicherheitsnetz. |
