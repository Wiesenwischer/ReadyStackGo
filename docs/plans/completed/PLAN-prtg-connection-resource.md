<!-- GitHub Epic: #401 (Variant 3) -->
# Phase: PRTG Integration — Variant 3 (PrtgConnection Resource)

> **Status: ✅ DONE** — shipped in [PR #405](https://github.com/Wiesenwischer/ReadyStackGo/pull/405) (v0.66.0). `PrtgConnection` aggregate + repository + EF migration + CRUD API + `/settings/prtg-connections` UI + `ProductDeployment.PrtgConnectionId` link + `PrtgApiClient` + 3 MediatR lifecycle handlers (Completed → register, Removed → deregister, Superseded → deregister). Detail-page UI followed in [PR #406](https://github.com/Wiesenwischer/ReadyStackGo/pull/406) with the "Saved connection" tab.

## Ziel

Eine PRTG-Anbindung wird im Domain-Modell als wiederverwendbare **PrtgConnection**-Resource modelliert (analog zu `Registry`). Admins legen pro Umgebung ein- oder mehrere PRTG-Connections an, jeder Product Deployment kann optional einer Connection zugewiesen werden und wird beim Deploy automatisch registriert / beim Remove deregistriert.

Diese Variante ist die nachhaltige Lösung: zentrale Connection-Verwaltung, Token-Rotation an einer Stelle, Self-Service-Auto-Discovery beim Aufsetzen neuer Deployments, sauber im DDD-Modell verankert.

## Analyse

### Bestehende Architektur

- **Registry-Aggregate** ([Registry.cs](../../src/ReadyStackGo.Domain/Deployment/Registries/Registry.cs)) ist die direkte Vorlage: eine wiederverwendbare externe Connection mit Credentials, CRUD-fähig in den Settings, optional verlinkbar von anderen Aggregaten.
- **CredentialEncryptionService** für Token-Verschlüsselung — gleiches Muster wie Registry-Passwörter und SNMPv3-Passphrasen.
- **Settings/Registries-Seite** ([RegistriesList.tsx](../../src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Settings/Registries/RegistriesList.tsx)) ist die UI-Pattern-Vorlage für eine Connection-Liste mit Add/Edit/Delete + Detail-Page.
- **MediatR Notification-Handler** für Lifecycle-Hooks (Register on Completed, Deregister on Removed) — identisch zu Variante 2, aber jetzt mit Lookup über `PrtgConnectionId`.
- **ProductDeployment ↔ PrtgConnection** — Many-to-One: ein Deployment kann (optional) genau einer Connection zugeordnet sein. Eine Connection kann von vielen Deployments referenziert werden.
- **Health-Aggregation-Pattern** aus dem laufenden SNMP-Feature (Snapshot-Provider) lässt sich für die Connection-Health-Anzeige adaptieren — pro Connection wird periodisch geprüft, ob PRTG erreichbar ist.

### Variant 3 vs. Variant 2

| Aspekt | Variant 2 | Variant 3 |
|---|---|---|
| Credentials | Pro Deployment | Pro Connection (N:1 zu Deployments) |
| Token-Rotation | Pro Deployment einzeln | Einmal, alle abhängigen Deployments betroffen |
| Domain-Modell | VO an ProductDeployment | Eigenes Aggregate `PrtgConnection` |
| UI | Wizard-Step "Monitoring" | + Settings-Page "PRTG Connections" + Wizard-Dropdown |
| Auto-Deregister | Ja, wie Variante 2 | Ja, wie Variante 2 |
| Connection-Test | Pro Deployment im Wizard | Pro Connection in Settings, mit periodischem Health-Check |

Variant 3 ist ein echtes Superset von Variant 2 — wenn man Variant 3 baut, ist Variant 2 obsolet (man könnte als Migration einmalig pro existierender PrtgRegistration eine PrtgConnection anlegen).

### Connection-Health-Check

Ein `PrtgConnectionHealthBackgroundService` pingt jede aktive Connection alle 5 Minuten gegen `/api/healthcheck.htm` und persistiert Status in `PrtgConnection.LastHealthCheck` / `.LastHealthCheckStatus`. UI zeigt grünen/roten Dot pro Connection. Bei Status-Wechsel: Domain-Event `PrtgConnectionHealthChanged`.

### Default-Connection-Mechanik

Eine Connection kann als "Default" markiert werden. Neue Deployments bekommen die Default-Connection vorausgewählt. Pro Environment kann es eine Default-Connection geben (nicht systemweit) — das ist wichtig für Multi-Tenant-Szenarien, wo unterschiedliche Environments unterschiedliche PRTG-Server haben.

### Auto-Discovery durch IP/Hostname-Detection

Sub-Feature für später (Out of Scope für initiale v0.66-Phase, aber explizit aufgelistet als Folgeidee): RSGO erkennt beim Deploy automatisch die externe IP/den Hostname des Ziel-Environments und füllt das `host`-Property im PRTG-Device entsprechend. Heute muss das manuell konfiguriert werden im Template-Device — der Wert wird dann beim Duplicate-Call übersteuert.

### Betroffene Bounded Contexts

- **Domain** — neues Aggregate `PrtgConnection` (Id, Name, Url, EncryptedApiToken, TemplateDeviceId, IsDefault, EnvironmentId?, LastHealthCheck, LastHealthCheckStatus, CreatedAt, UpdatedAt). Repository `IPrtgConnectionRepository`. Domain-Events: `PrtgConnectionCreated`, `PrtgConnectionUpdated`, `PrtgConnectionDeleted`, `PrtgConnectionHealthChanged`. Neue Property auf `ProductDeployment`: `PrtgConnectionId?` mit Convenience-Methoden `LinkPrtgConnection(id)` / `UnlinkPrtgConnection()`.
- **Application** — CRUD-Commands/Queries unter `UseCases/PrtgConnections/`. `IPrtgApiClient` wie Variante 2. Handler `PrtgRegisterOnDeploymentCompletedHandler` (jetzt mit Connection-Lookup statt Inline-Credentials). Health-Check-Service.
- **Infrastructure** — EF-Configuration + Migration `AddPrtgConnections`. `PrtgConnectionRepository`. `PrtgApiClient`-Impl wie Variante 2.
- **API** — REST-Endpoints `/api/prtg-connections/*` (GET list, GET by id, POST create, PUT update, DELETE, POST test). Erweiterung von `CreateProductDeploymentCommand` um `PrtgConnectionId?`. `PrtgConnectionHealthBackgroundService`.
- **WebUI (rsgo-generic)** — neue Settings-Section "PRTG Connections" (List + Add/Edit/Delete + Detail mit Health-Status + Test-Button). Deploy-Wizard bekommt Dropdown "PRTG Connection" (None / Connection-Liste). Product-Deployment-Detail-Page zeigt verlinkte Connection + Re-Register-Action.

## AMS UI Counterpart

**Benötigt AMS UI eine Entsprechung?**

- [x] **Ja** — sowohl die Settings-Section als auch die Deploy-Wizard-Erweiterung müssen in der AMS-Distribution erscheinen. Alle Hooks (`usePrtgConnections`, `useCreatePrtgConnection`, etc.) in `@rsgo/core`.

→ AMS-Counterpart-Plan: `C:\proj\ReadyStackGo.Ams\docs\Plans\PLAN-prtg-connection-resource.md`

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten:

- [ ] **Feature 1: PrtgConnection Aggregate + Repository** — Domain-Aggregate mit Invarianten (Name unique, URL-Format, Token-Mindestlänge, Default-Eindeutigkeit pro Environment). Domain-Events. Repository-Interface in Domain, Impl in Infrastructure.
  - Betroffene Dateien:
    - `src/ReadyStackGo.Domain/Monitoring/PrtgConnections/PrtgConnection.cs`
    - `src/ReadyStackGo.Domain/Monitoring/PrtgConnections/PrtgConnectionId.cs`
    - `src/ReadyStackGo.Domain/Monitoring/PrtgConnections/PrtgConnectionEvents.cs`
    - `src/ReadyStackGo.Domain/Monitoring/PrtgConnections/IPrtgConnectionRepository.cs`
    - `src/ReadyStackGo.Infrastructure/Persistence/Repositories/PrtgConnectionRepository.cs`
    - `src/ReadyStackGo.Infrastructure/Persistence/Configurations/PrtgConnectionConfiguration.cs`
    - EF Migration `AddPrtgConnections`
  - Pattern-Vorlage: [Registry.cs](../../src/ReadyStackGo.Domain/Deployment/Registries/Registry.cs)
  - Abhängig von: -

- [ ] **Feature 2: ProductDeployment ↔ Connection Link** — optionale FK `PrtgConnectionId` auf `ProductDeployment`. Methoden `LinkPrtgConnection` / `UnlinkPrtgConnection`. Migration ergänzt Spalte. Beim Löschen einer Connection: alle abhängigen Deployments werden auf `null` gesetzt (nicht kaskadiert).
  - Betroffene Dateien:
    - `src/ReadyStackGo.Domain/Deployment/ProductDeployments/ProductDeployment.cs` (extend)
    - `src/ReadyStackGo.Infrastructure/Persistence/Configurations/ProductDeploymentConfiguration.cs` (extend)
    - EF Migration `LinkProductDeploymentToPrtgConnection`
  - Abhängig von: Feature 1

- [ ] **Feature 3: IPrtgApiClient (shared with Variant 2)** — Application-Interface + Infrastructure-Impl mit HttpClient, Polly-Retry, konfigurierbarer Cert-Validation. Falls Variante 2 bereits implementiert: wiederverwenden.
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/Monitoring/Prtg/IPrtgApiClient.cs`
    - `src/ReadyStackGo.Infrastructure/Monitoring/Prtg/PrtgApiClient.cs`
  - Abhängig von: -

- [ ] **Feature 4: CRUD-Endpoints + Commands/Queries** — `/api/prtg-connections` GET-List/POST/GET-by-id/PUT/DELETE plus `POST /api/prtg-connections/{id}/test`. Permission-Schema: `Settings:Read` / `Settings:Manage`.
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/UseCases/PrtgConnections/{Create,Update,Delete,Get,List,TestConnection}/*`
    - `src/ReadyStackGo.Api/Endpoints/PrtgConnections/*Endpoint.cs`
  - Pattern-Vorlage: Registries-Endpoints
  - Abhängig von: Feature 1, 3

- [ ] **Feature 5: MediatR Lifecycle-Handler** — drei Handler:
  - `PrtgRegisterOnDeploymentCompletedHandler`: lädt PrtgConnection per Id, ruft `IPrtgApiClient.RegisterDeviceAsync`.
  - `PrtgDeregisterOnDeploymentRemovedHandler`: `ProductDeploymentRemoved` + `ProductDeploymentSuperseded`.
  - `PrtgUpdateOnConnectionUpdatedHandler`: bei Connection-Update werden registrierte Devices re-validated.
  - Plus `PrtgConnectionHealthBackgroundService`: pingt alle Connections alle 5 Minuten.
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/Monitoring/Prtg/Handlers/*.cs`
    - `src/ReadyStackGo.Api/BackgroundServices/PrtgConnectionHealthBackgroundService.cs`
  - Pattern-Vorlage: Trap-Handler aus [PLAN-snmp-completion.md](PLAN-snmp-completion.md)
  - Abhängig von: Feature 2, 3, 4

- [ ] **Feature 6: WebUI Settings-Section "PRTG Connections"** — List/Detail/Add/Edit/Delete Pages. Health-Dot pro Connection. Test-Connection-Button. Default-Toggle (eindeutig pro Env). Settings-Index-Eintrag.
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Settings/PrtgConnections/PrtgConnectionsList.tsx`
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Settings/PrtgConnections/{Add,Edit,Delete,Detail}PrtgConnection.tsx`
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Settings/SettingsIndex.tsx` (extend)
    - `src/ReadyStackGo.WebUi/packages/core/src/api/prtgConnections.ts`
    - `src/ReadyStackGo.WebUi/packages/core/src/hooks/usePrtgConnections.ts`
  - Pattern-Vorlage: Registries-Pages
  - Abhängig von: Feature 4

- [ ] **Feature 7: Deploy-Wizard Connection-Dropdown** — Optional-Step (oder erweiteter "Confirm"-Step) mit Dropdown "PRTG Connection" (None / aktive Connections, default = Default-Connection des Ziel-Environments).
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Deploy/Wizard/MonitoringStep.tsx`
    - Wizard-Step-Liste extend
  - Abhängig von: Feature 6

- [ ] **Feature 8: ProductDeployment Detail Page — PRTG-Section** — wenn `PrtgConnectionId` gesetzt: Card mit Connection-Name (Link), Registered Device-ID, Re-Register-Button, Disconnect-Button.
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Deployments/PrtgConnectionCard.tsx`
  - Abhängig von: Feature 7

- [ ] **Feature 9: Public Website Doc + Migration-Hinweise** — Reference-Page "PRTG Connections" mit ConnectionTokens-Setup, Multi-Tenant-Pattern, Health-Check-Interval, Auto-Deregister-Verhalten. Falls Variante 2 vorher released: Migration-Anleitung "Pro-Deployment-Credentials → Connection".

- [ ] **Phase abschließen** — Tests grün, manueller E2E-Smoketest, PR gegen main.

## Test-Strategie

- **Unit Tests**:
  - `PrtgConnection` — Invarianten (Name unique innerhalb Environment, Default-Eindeutigkeit, URL-Format, Token-Encryption-Roundtrip).
  - `ProductDeployment.LinkPrtgConnection` — happy path, doppeltes Linken überschreibt, Unlink.
  - Handler — Lookup über `PrtgConnectionId == null` (nichts tun), Lookup über gültige Id (API-Call), Lookup über gelöschte Connection (Warn-Log, kein Throw).
  - Health-Service — Status-Wechsel emittiert Event genau einmal pro Wechsel.

- **Integration Tests**:
  - CRUD-Endpoints + RBAC (`Settings:Read` vs `Settings:Manage`).
  - Connection-Health-Background-Service über zwei Tick-Intervalle, Status persistiert.
  - Connection löschen: abhängige Deployments behalten Deployment-Status, aber `PrtgConnectionId = null`.

- **E2E Tests** (Playwright):
  - Settings-Section: Connection anlegen, editieren, löschen, Default toggeln.
  - Deploy-Wizard: Dropdown zeigt aktive Connections, Default-Connection vorausgewählt.
  - Deployment-Detail: PRTG-Section sichtbar mit Re-Register-Button.

- **Manueller PRTG-Smoke-Test** (Acceptance):
  - Zwei PRTG-Connections anlegen (zwei verschiedene PRTG-Instanzen). Drei Deployments deployen, je eine an jeder Connection und eines ohne. Verify pro Connection korrekte Device-Anlage. Eine Connection löschen, Deployment-Detail zeigt "kein Monitoring".

## Offene Punkte

- [ ] **Multi-Tenant-Trennung** — Soll eine Connection an genau ein Environment gebunden sein (FK), oder global mit Default-pro-Env? Initial: Default global mit optionalem Env-Scope; bei mehr Kunden-Feedback eventuell auf strikte Env-Bindung umstellen.
- [ ] **Migration von Variante 2** — Falls Variante 2 zuerst released wird: automatisches Aufbauen einer Default-Connection pro existierender PrtgRegistration beim Upgrade auf v0.66+1. Migration-Script-Skizze nötig.
- [ ] **Connection Sharing zwischen ProductDeployments und anderen Aggregaten** — Sollte das `PrtgConnection`-Konzept später für andere Ressourcen (StackSources? Environments?) wiederverwendbar sein? Wenn ja, ggf. Namespace `Monitoring` statt `PrtgConnection`-spezifisch.

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Aggregate-Naming | `PrtgConnection` / `Monitoring`-spezifisch | **`PrtgConnection`** | Konkret und tippbar. Bei weiteren Monitoring-Backends (Zabbix, Nagios) wird parallel ein eigenes Aggregate gebaut — Vermeidung von Premature Generalization. |
| Domain-Namespace | `Deployment/...` / `Monitoring/...` | **`Monitoring/`** | PRTG ist nicht Teil des Deployment-Bounded-Context. Neuer Namespace für künftige Monitoring-Resources offen. |
| FK-Lifecycle | Cascade Delete / Set Null | **Set Null** | Connection-Löschen darf keinen Deployment broken — Deployments bleiben funktional, nur Monitoring entkoppelt. |
| Default-Scope | Global / Per-Environment | **Per-Environment** | Multi-Tenant-Setups mit unterschiedlichen PRTG pro Env sind das Hauptmotiv für Variante 3 selbst. |
| Health-Check-Intervall | 1 min / 5 min / konfigurierbar | **5 min** | Sufficient für Connection-Health (kein Per-Sensor-Polling), reduziert PRTG-API-Last. Bei Bedarf später konfigurierbar. |
| Variante-2-Koexistenz | Beide parallel / Variante 2 obsoletet | **Variante 2 obsoletet** | Wenn Variante 3 da ist, gibt es keinen Grund für die VO-Variante. Migration einmalig automatisiert. |
| IANA-PEN-Abhängigkeit | Blocken bis PEN da / Trotzdem ausliefern | **Blocken bis PEN da** | Wie Variante 2 — persistente PRTG-Devices brauchen stabile OIDs. |
