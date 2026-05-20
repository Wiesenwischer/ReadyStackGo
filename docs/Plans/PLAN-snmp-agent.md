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

### Index-Strategie und Stabilität

SNMP-Tabellen verwenden Integer-Indizes zur Zeilen-Adressierung — diese sind reine Positionen ohne semantische Bedeutung. Damit Monitoring-Tools ihre OID-Konfiguration langfristig stabil halten können (z.B. "OID `.3.1.6.2.1` ist und bleibt ams.project auf test-amsproject"), müssen die Indizes **über Server-Restarts und Add/Delete-Operationen hinweg stabil** sein.

**Gewählte Strategie: Hash-derived Integer-Indizes.**

- `rsgoEnvironmentIndex` = `Math.Abs(EnvironmentId.GetHashCode())` (positiver Int32)
- `rsgoProductIndex` = `Math.Abs(ProductGroupId.GetHashCode())`
- `rsgoStackIndex` = `Math.Abs($"{ProductGroupId}/{StackName}".GetHashCode())`
- `rsgoServiceIndex` = `Math.Abs($"{ProductGroupId}/{StackName}/{ServiceName}".GetHashCode())`

Vorteile:
- Identische Entity → identischer Index, deterministisch, unabhängig von Snapshot-Build-Reihenfolge oder Add/Delete-Vorgängen.
- Kompakte OIDs (ein Integer pro Indextiefe).
- Funktioniert nahtlos mit `snmpwalk` und allen Standard-MIB-Browsern.

Kollisions-Handling:
- Snapshot-Builder prüft beim Build, ob zwei Entities denselben Hash haben.
- Bei Kollision: deterministischer Tiebreaker (zweite Entity bekommt `hash + 1` — falls auch besetzt iterativ weiter), plus `WARN`-Log mit beiden konkreten IDs.
- Kollisions-Wahrscheinlichkeit bei realistischen Größenordnungen (<100 Envs, <500 Products, <2000 Stacks): deutlich unter 1:1.000.000 (Birthday-Bound auf 31-bit Hash-Space).

Discovery durch Monitoring-Tools: Die Daten-Spalten (`rsgoEnvironmentName`, `rsgoEnvironmentId`, `rsgoProductName`, `rsgoProductId`, `rsgoStackName`, `rsgoServiceName`) bilden die "Identitäts-Schicht". Tools walken zuerst diese Spalten, bauen sich daraus ihre eigene Index → Name-Mapping, und können dann gezielt einzelne Indizes pollen.

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

- [x] **Feature 1: SNMP Listener Backbone** — Lextm.SharpSnmpLib einbinden, `SnmpAgent`-Service in `Infrastructure`, hostet UDP-Listener auf konfigurierbarem Port (Default UDP/1161, non-privileged → rootless-tauglich; im Compose-File auf `1161:1161/udp` gemappt, optional vom Operator auf `161:1161/udp` re-mappbar). Erst ohne echte OIDs — antwortet mit `noSuchObject` auf alles. End-to-End-Pfad damit erstmal stehend.
  - Betroffene Dateien: neue `src/ReadyStackGo.Infrastructure/Snmp/SnmpAgent.cs`, `Snmp/IOidTree.cs`, `Snmp/OidTreeBuilder.cs` (stub). `src/ReadyStackGo.Api/BackgroundServices/SnmpAgentBackgroundService.cs`.
  - Pattern-Vorlage: [HealthCollectorBackgroundService.cs](../../src/ReadyStackGo.Api/BackgroundServices/HealthCollectorBackgroundService.cs) für DI-Scope + Cancellation-Handling.
  - Abhängig von: -

- [ ] **Feature 2: OID Tree + Snapshot Provider** — `SnmpSnapshotProvider` in `Application` baut pro Walk-Request einen konsistenten Read-Snapshot (Products-, Stacks-, Services-Tabellen) aus `ProductDeploymentRepository` und HealthSnapshots. `OidTreeBuilder` mapped Snapshot → SharpSnmp-`ObjectIdentifier`s.
  - Betroffene Dateien: `src/ReadyStackGo.Application/Snmp/SnmpSnapshotProvider.cs`, `Snmp/SnmpSnapshot.cs`, plus Tests.
  - Abhängig von: Feature 1

- [x] **Feature 3: SNMPv2c Community-String Auth** — Community-String validation im Listener; mismatched requests werden silent dropped. Config kommt via `appsettings.json` (Snmp:Community); DB-Persistierung und UI-Editing rutschen in eine Folgephase, sodass die Anzahl der EF-Migrationen pro Release klein bleibt.

- [-] **Feature 4: SNMPv3 User Storage + Auth/Priv** — *Teilweise auf v0.65 verschoben.* In v0.64 ist die USM-Plumbing fertig: `SnmpV3UserOption`-Liste aus `appsettings.json`, `UserRegistry` mit den Providern (SHA-1/256/384/512, MD5; AES-128/192/256, DES) wird beim Agent-Start gebaut, eingehende v3-Pakete werden gegen die Registry dekodiert. Volle Response-Konstruktion (Engine-ID, Time-Window, PrivacyProvider re-encrypt) braucht den SharpSnmpLib `SnmpEngine`-Framework-Aufsatz und wird als eigene Phase v0.65 nachgeschoben. v0.64 antwortet nur via SNMPv2c; v3-Pakete werden derzeit silent verworfen, falls die Registry kein matching User enthält. DB-Persistenz statt `appsettings.json` wandert ebenfalls in die Folgephase.

- [x] **Feature 5: REST-Endpoints für Settings/Users** — Read-only Variante in v0.64: `GET /api/snmp/status`, `GET /api/snmp/oid-reference`, `GET /api/snmp/mib`. PUT/POST/DELETE Endpoints für Live-Editing wandern in die v0.65-Folgephase zusammen mit der DB-Persistierung von Feature 3/4.
  - Betroffene Dateien: `src/ReadyStackGo.Api/Endpoints/Snmp/*Endpoint.cs`, Application-Layer Commands/Queries.
  - Abhängig von: Feature 3, 4

- [x] **Feature 6: WebUI SNMP-Settings-Section** — Read-only Settings-Section unter `/settings/snmp` mit Status (Enabled/Port/RootOid/Community/V3-Users) plus MIB-Download-Button. Live-Editing (Toggles, Inputs, v3-User-Add) wandert mit dem Settings-API auf v0.65.
  - Betroffene Dateien: `packages/ui-generic/src/pages/Settings/SnmpSettings.tsx` plus Hook in `packages/core/src/hooks/useSnmpSettings.ts` und API in `packages/core/src/api/snmp.ts`.
  - Abhängig von: Feature 5

- [x] **Feature 7: OID Reference Browser** — Eine WebUI-Seite, die die aktuell aktiven OIDs für die laufende RSGO-Instanz zeigt. Admins können vor dem Einrichten ihrer Monitoring-Tool-Checks sehen, welche konkreten OIDs welche Environments/Products/Stacks/Services repräsentieren — kein vorheriges `snmpwalk` zum Discovery nötig.
  - **Backend**: REST-Endpoint `GET /api/snmp/oid-reference` (Permission `Snmp:Read`), liefert Snapshot mit allen materialisierten OIDs:
    ```json
    {
      "rootOid": "1.3.6.1.4.1.99999.1",
      "system": [
        { "oid": "1.3.6.1.4.1.99999.1.1.1.0", "symbol": "rsgoSystemVersion", "type": "STRING", "currentValue": "0.64.0" },
        ...
      ],
      "environments": [
        {
          "name": "test-amsproject",
          "envId": "c4acceb4-cc2b-4451-9ade-44a90d4163e7",
          "envIndex": 1572834219,
          "baseOid": "1.3.6.1.4.1.99999.1.2.1.<column>.1572834219",
          "products": [
            {
              "name": "ams.project",
              "productId": "SPE-Project:ams.project:3.2.0-ci",
              "prodIndex": 998012345,
              "baseOid": "1.3.6.1.4.1.99999.1.3.1.<column>.1572834219.998012345",
              "stacks": [
                {
                  "name": "Infrastructure",
                  "stackIndex": 123456789,
                  "baseOid": "1.3.6.1.4.1.99999.1.4.1.<column>.1572834219.998012345.123456789",
                  "services": [...]
                },
                ...
              ]
            }
          ]
        }
      ]
    }
    ```
  - **WebUI**: Neuer Tab "OID Reference" auf der Settings/SNMP-Seite (oder eigener Reiter in `Settings → SNMP`). Baum-Darstellung Environment → Product → Stack → Service mit allen OIDs neben jedem Knoten. Pro Knoten ein Copy-Button für die OID. Pro Knoten ein "Export"-Button: lädt eine Textdatei für genau diesen Subtree mit allen Spalten — direkt in Nagios/Zabbix/PRTG kopierbar.
  - **Filter**: nach Environment, nach Produktname, "nur Failed-Stacks anzeigen". Standardmäßig Tree zusammengeklappt, ausklappen pro Environment.
  - **Format-Toggle**: numerische OID (`1.3.6.1.4.1.99999.1.3.1.6.…`) ↔ symbolisch (`RSGO-MIB::rsgoProductStatus.…`).
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/Snmp/GetOidReferenceQuery.cs` (CQRS, nutzt denselben `SnmpSnapshotProvider` aus Feature 2)
    - `src/ReadyStackGo.Api/Endpoints/Snmp/GetOidReferenceEndpoint.cs`
    - `packages/core/src/api/snmp.ts` (Client-Funktion `getOidReference()`)
    - `packages/ui-generic/src/pages/Settings/SnmpOidReference.tsx` (neue Seite)
  - Abhängig von: Feature 2 (Snapshot-Provider muss stehen), Feature 6 (UI-Settings-Section, weil neuer Tab dort)

- [x] **Feature 8: MIB-File Generation** — Hand-curated MIB-Datei (`READYSTACKGO-MIB.txt`) als Embedded Resource, plus Endpoint zum Download. Validierung mit `smilint` als Teil von CI (Build-Step). CI-Lint kommt mit dem CI-Workflow-Update in v0.65 nach (smilint braucht apt-get-Provisionierung im Runner).
  - Betroffene Dateien: `src/ReadyStackGo.Api/Resources/READYSTACKGO-MIB.txt`, CI-Job-Erweiterung.
  - Abhängig von: Feature 2 (OID-Tree finalisiert)

- [ ] **Dokumentation & Website**
  - Public Website DE/EN: Reference-Page "SNMP Monitoring" mit OID-Liste, Auth-Setup, MIB-Download-Link, Beispiel-snmpwalks für die häufigsten Tools.
  - Docs/Architecture-Notiz dass v0.64 SNMP read-only liefert und Traps/PEN-Wechsel separate Follow-up-Phasen sind.
  - Roadmap-Update.

- [ ] **Phase abschließen** — alle Tests grün, dotnet build 0 errors / 0 warnings, manuelles E2E-Smoketest mit `snmpwalk` aus einem zweiten Container, PR gegen main.

- [ ] **PEN-Migration nach IANA-Zuweisung** — Sobald IANA die Private Enterprise Number zugewiesen hat, Placeholder `99999` ersetzen in:
  - `appsettings.json` → `Snmp.RootOid` Default
  - `READYSTACKGO-MIB.txt` → `enterprises` Branch
  - Documentation (Plan, Public-Website, README)
  - Beilegender Migration-Hinweis in den Release Notes (Customers, die `99999` schon nutzen, müssen Monitoring-Tools umstellen).
  - **Status (2026-05-19):** PEN-Antrag bei IANA eingereicht. Kontakt: `dev@wiesenwischer.de`. Bestätigung erwartet 1–3 Wochen.

## Test-Strategie

- **Unit Tests**:
  - `SnmpSnapshotProvider` — Mapping Domain → Snapshot (Status-Enums, Counts, leere Tabellen, null-safe Fields).
  - `OidTreeBuilder` — GETNEXT-Iteration über Tabellen, lexikalische OID-Ordnung, INDEX-Encoding.
  - `SnmpSettings` / `SnmpV3User` Domain-Invarianten (Port-Range, Algo-Whitelist, Mindest-Schlüssellängen).
  - `GetOidReferenceQuery` — Tree-Aufbau (Env → Product → Stack → Service), Hash-Indizes konsistent mit OidTreeBuilder, leere Tabellen.

- **Integration Tests**:
  - Agent gegen lokalen UDP-Loopback. `snmpsharpnet` (im Test als Client-Lib) führt GET/GETNEXT/WALK aus, Assertions auf zurückgegebene Werte.
  - v2c-Auth: korrekter Community akzeptiert, falscher abgelehnt.
  - v3-Auth: SHA+AES Roundtrip, Replay-Window.
  - Settings-Endpoints: RBAC-PreProcessor blockt nicht-Admin.

- **E2E Tests** (Playwright + Container):
  - Settings-Section: SNMP enablen, Community setzen, v3-User anlegen.
  - Sidecar-Container mit `net-snmp` CLI führt `snmpwalk -v2c -c <community> rsgo` und `snmpwalk -v3 -u <user> ...` aus; Output enthält erwartete Produkte/Stacks/Services.
  - **OID Reference Page**: Seite öffnen, Tree expanded sehen mit Environment/Product/Stack-Namen. Copy-Button kopiert die OID korrekt. OID aus dem Browser-Tree gegen `snmpget` im Sidecar-Container abfragen — Werte stimmen überein.

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
| Index-Stabilität | Sort+sequentiell / Hash-derived / ID-als-OID-Suffix | **Hash-derived Int32** | Monitoring-Tools speichern OIDs langfristig — Indizes dürfen sich bei Add/Delete-Operationen nicht verschieben. Hash über die fachlichen IDs ist deterministisch, kompakt, unabhängig von der Snapshot-Reihenfolge. Hash-Kollisionen sind bei RSGO-Skalen (<2000 Stacks total) statistisch vernachlässigbar; ein Tiebreaker + Warning fängt den Edge-Case ab. |
