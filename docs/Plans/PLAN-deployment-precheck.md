<!-- GitHub Epic: #353 -->
# Phase: Deployment Precheck (v0.59)

## Ziel

Pre-Flight-Checks vor jedem Deployment durchführen, um Infrastruktur-Probleme **vor** dem Point of No Return (Container-Entfernung in der DeploymentEngine) zu erkennen. Der User sieht eine klare Übersicht mit OK/Warning/Error-Status pro Prüfung und kann informiert entscheiden, ob er fortfahren möchte.

## Analyse

### Bestehende Architektur

**Deployment Flow (aktuell):**
```
UI: DeployStack Page → Variablen eingeben → Deploy klicken → Deployment startet sofort
API: POST /api/environments/{envId}/stacks/{stackId}/deploy → DeployStackHandler → DeploymentEngine
Hooks: POST /api/hooks/deploy → DeployViaHookHandler → DeploymentEngine
```

**Point of No Return** in `DeploymentEngine.cs` (Zeile ~238):
- Phase `RemovingOldContainers` entfernt existierende Container
- Danach ist Rollback nötig wenn Image-Pull oder Start fehlschlägt

**Bestehende Validierung:**
- `DeploymentPrerequisiteValidationService` (Domain) — prüft Environment, Org, User, Variablen, Services
- `StackVariableResolver.Resolve()` — löst Variablen auf und validiert Required/Pattern
- Diese Validierung prüft aber **keine Infrastruktur** (Images, Ports, Networks, Volumes)

**Bestehende Docker-Services:**
- `IDockerService.ImageExistsAsync()` — lokale Image-Prüfung
- `IDockerService.ListContainersAsync()` — laufende Container + Port-Bindings
- `IDockerService.ListVolumesRawAsync()` / `InspectVolumeAsync()` — Volume-Status
- `IDockerService.EnsureNetworkAsync()` — Network-Management
- `IRegistryAccessChecker.CheckAccessAsync()` — Registry-Erreichbarkeit + Auth

**UI-States (DeployStack.tsx):**
- `loading` → `error` → `configure` → `deploying` → `success`
- Store: `useDeployStackStore` in `@rsgo/core`

### Betroffene Bounded Contexts

- **Domain**: Neue Value Objects `PrecheckResult`, `PrecheckItem`, `PrecheckSeverity`
- **Application**: Neuer Query `RunDeploymentPrecheckQuery` + Handler, Interface `IDeploymentPrecheckRule`, Application-Layer Rules (VariableValidation, ExistingDeployment)
- **Infrastructure**: Infrastructure-Layer Rules (ImageAvailability, PortConflict, NetworkAvailability, VolumeStatus), DI-Registration
- **API**: Neuer Precheck-Endpoint, Hooks-API-Erweiterung (automatischer Precheck + dryRun)
- **WebUI (rsgo-generic)**: PrecheckPanel-Komponente, DeployStack-Flow-Erweiterung, API-Client

## AMS UI Counterpart

> RSGO has two UI distributions with different design systems:
> - **rsgo-generic**: React + Tailwind CSS (reference implementation, `packages/ui-generic`)
> - **AMS UI**: ConsistentUI/Lit web components (separate repo `ReadyStackGo.Ams`)
>
> Shared logic lives in `@rsgo/core` (hooks, API calls, state). Pages/layouts must be reimplemented per distribution.

**Benötigt AMS UI eine Entsprechung?**

- [x] **Ja (deferred)** — AMS-Counterpart wird später geplant
- Die `@rsgo/core` Hooks und API-Client-Erweiterungen sind shared und sofort nutzbar
- Die PrecheckPanel-UI-Komponente muss im AMS Repo mit ConsistentUI nachgebaut werden

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| UI Precheck Trigger | Eager, Lazy, Hybrid | **Hybrid** | Auto-Run beim Betreten des Deploy-Flows für schnelles Feedback + Re-Check Button nach Korrekturen |
| Hooks API Precheck | Opt-in Dry-Run, Immer automatisch, Beides | **Immer automatisch** | Jeder Hook-Deploy führt Precheck durch. Bei Errors wird abgebrochen. Zusätzlich `dryRun: true` für reine Prüfung ohne Deploy |
| Precheck Timeout | 10s, 30s, 60s | **30s** | Registry-Checks können langsam sein, aber >30s deutet auf echtes Problem hin |
| Precheck Caching | Kein Cache, 30s Cache | **Kein Cache** | Infrastruktur-Status kann sich schnell ändern, Cache wäre irreführend |
| Rule-Interface Location | Domain, Application | **Application** | Rules brauchen Zugriff auf Services (IDockerService, IRegistryAccessChecker) |
| Product Precheck API | Frontend-Aggregation, Backend-Endpoint, Beides | **Backend-Endpoint** | Saubere API, wiederverwendbar für Hooks, Backend kann Docker-Kontext einmal laden und für alle Stacks nutzen |
| Product Precheck Orchestrierung | Sequentiell, Parallel | **Parallel via MediatR** | Handler dispatcht RunDeploymentPrecheckQuery pro Stack parallel, aggregiert Ergebnisse |

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten:

- [x] **Feature 1: Domain Model** — Value Objects für Precheck-Ergebnisse
  - Neue Dateien:
    - `Domain/Deployment/Precheck/PrecheckSeverity.cs` (Enum: OK, Warning, Error)
    - `Domain/Deployment/Precheck/PrecheckItem.cs` (Record: Rule, Severity, Title, Detail?, ServiceName?)
    - `Domain/Deployment/Precheck/PrecheckResult.cs` (Value Object: Checks, HasErrors, HasWarnings, CanDeploy, Summary)
  - Pattern-Vorlage: `DeploymentPrerequisiteResult` (ähnliche Struktur mit IsValid + Fehler-Liste)
  - Abhängig von: -

- [x] **Feature 2: Rule Interface + Implementierungen** — 6 Precheck-Regeln
  - Neue Dateien (Application Layer):
    - `Application/UseCases/Deployments/Precheck/IDeploymentPrecheckRule.cs` (Interface)
    - `Application/UseCases/Deployments/Precheck/PrecheckContext.cs` (Context-DTO)
    - `Application/UseCases/Deployments/Precheck/Rules/VariableValidationRule.cs`
    - `Application/UseCases/Deployments/Precheck/Rules/ExistingDeploymentRule.cs`
  - Neue Dateien (Infrastructure Layer):
    - `Infrastructure/Services/Deployment/Precheck/ImageAvailabilityRule.cs`
    - `Infrastructure/Services/Deployment/Precheck/PortConflictRule.cs`
    - `Infrastructure/Services/Deployment/Precheck/NetworkAvailabilityRule.cs`
    - `Infrastructure/Services/Deployment/Precheck/VolumeStatusRule.cs`
  - DI-Registration in `Infrastructure/DependencyInjection.cs`: alle Rules als `IDeploymentPrecheckRule`
  - Pattern-Vorlage: Strategy Pattern wie bestehende Service-Registrierungen
  - Abhängig von: Feature 1

- [x] **Feature 3: MediatR Query Handler** — Orchestrierung aller Rules
  - Neue Dateien:
    - `Application/UseCases/Deployments/Precheck/RunDeploymentPrecheckQuery.cs` (Query + Handler)
  - Handler sammelt PrecheckContext, führt alle Rules parallel aus, aggregiert zu PrecheckResult
  - Timeout: 30s für gesamten Precheck (CancellationToken)
  - Pattern-Vorlage: `DeployStackHandler` (MediatR-Handler mit Repository/Service-Zugriff)
  - Abhängig von: Feature 2

- [x] **Feature 4: API Endpoint** — Precheck-Endpoint für UI
  - Neue Dateien:
    - `Api/Endpoints/Deployments/PrecheckEndpoint.cs` (`POST /api/environments/{envId}/stacks/{stackId}/precheck`)
  - Request: `{ variables: { key: value } }`
  - Response: `{ canDeploy, hasErrors, hasWarnings, summary, checks: [...] }`
  - Permission: `Deployments.Create` (selbe Permission wie Deploy)
  - Pattern-Vorlage: `DeployStackEndpoint.cs`
  - Abhängig von: Feature 3

- [x] **Feature 5: Hooks API Integration** — Automatischer Precheck + Dry-Run
  - Bestehende Datei erweitern:
    - `Api/Endpoints/Hooks/DeployEndpoint.cs` — `dryRun: bool` Parameter hinzufügen
    - `Application/UseCases/Hooks/DeployViaHook/DeployViaHookCommand.cs` — DryRun Property
    - `Application/UseCases/Hooks/DeployViaHook/DeployViaHookHandler.cs` — Precheck vor Deploy ausführen, bei Errors abbrechen
  - Verhalten:
    - `dryRun: true` → nur Precheck ausführen, PrecheckResult zurückgeben
    - `dryRun: false` (default) → Precheck automatisch ausführen, bei Errors 422 mit PrecheckResult zurückgeben, bei OK/Warnings normal deployen
  - Abhängig von: Feature 3

- [x] **Feature 6: UI — PrecheckPanel + DeployStack Flow** — Frontend-Integration
  - Neue Dateien (rsgo-core):
    - `packages/core/src/api/precheck.ts` — API-Client für Precheck-Endpoint
    - `packages/core/src/hooks/usePrecheck.ts` — React Hook für Precheck-State
  - Neue Dateien (ui-generic):
    - `packages/ui-generic/src/components/deployment/PrecheckPanel.tsx` — Ergebnis-Anzeige
    - `packages/ui-generic/src/components/deployment/PrecheckItem.tsx` — Einzelne Check-Zeile
  - Bestehende Dateien erweitern:
    - `packages/ui-generic/src/pages/Deployments/DeployStack.tsx` — Neue States: `prechecking`, `precheck-done`
  - Verhalten:
    - Hybrid-Trigger: Precheck startet automatisch wenn Variablen fertig + Re-Check Button
    - Deploy-Button disabled wenn `hasErrors`
    - OK-Items collapsed, Errors/Warnings expanded
  - Abhängig von: Feature 4

- [x] **Feature 7: Tests** — Unit, Integration, E2E
  - Unit Tests:
    - `PrecheckResult` Aggregation (HasErrors, HasWarnings, CanDeploy, Summary)
    - Jede Rule einzeln mit allen Szenarien (OK, Warning, Error, Edge Cases)
    - `RunDeploymentPrecheckHandler` mit gemockten Rules
  - Integration Tests:
    - API Endpoint Roundtrip
    - Hooks API mit dryRun
  - E2E Tests:
    - Happy Path: Stack deployen mit bestandenem Precheck
    - Error Case: Belegter Port → Deploy-Button disabled
    - Warning Case: Existierendes Volume → Warnung → Deploy trotzdem möglich
  - Abhängig von: Feature 4, Feature 5, Feature 6

- [x] **Feature 8: Product Precheck Backend** — Neuer Endpoint + MediatR Query für Produkt-Precheck
  - Neue Dateien:
    - `Application/UseCases/Deployments/Precheck/RunProductPrecheckQuery.cs` (Query + Handler)
    - `Api/Endpoints/Deployments/ProductPrecheckEndpoint.cs` (`POST /api/environments/{envId}/product-deployments/precheck`)
  - Request: `{ productId, deploymentName, stackConfigs: [{ stackId, variables }], sharedVariables }`
  - Response: `{ canDeploy, hasErrors, hasWarnings, summary, stacks: [{ stackId, stackName, canDeploy, hasErrors, hasWarnings, summary, checks: [...] }] }`
  - Handler:
    - Produkt laden, SharedVariables mit PerStack-Variables mergen (pro Stack)
    - Pro Stack den bestehenden `RunDeploymentPrecheckQuery` via MediatR dispatchen (parallel)
    - Ergebnisse aggregieren zu `ProductPrecheckResult` (canDeploy = alle Stacks canDeploy)
    - Stack-Name-Ableitung: `deploymentName-stackName` (gleiche Logik wie `DeployProductHandler`)
  - Neue Domain-Klasse:
    - `Domain/Deployment/Precheck/ProductPrecheckResult.cs` (Value Object: Stacks-Liste + Aggregation)
  - Permission: `Deployments.Create` (wie DeployProduct)
  - Pattern-Vorlage: `DeployProductEndpoint.cs` (Request-Struktur), `PrecheckEndpoint.cs` (Response-Mapping)
  - Abhängig von: Feature 3, Feature 4

- [x] **Feature 9: Product Precheck UI** — useProductPrecheck Hook + DeployProduct.tsx Integration
  - Neue Dateien (rsgo-core):
    - `packages/core/src/api/precheck.ts` — Erweitern um `runProductPrecheck()` API-Client
    - `packages/core/src/hooks/useProductPrecheck.ts` — React Hook für Product-Precheck-State
  - Neue Dateien (ui-generic):
    - `packages/ui-generic/src/components/deployments/ProductPrecheckPanel.tsx` — Ergebnis-Anzeige pro Stack gruppiert
  - Bestehende Dateien erweitern:
    - `packages/ui-generic/src/pages/Deployments/DeployProduct.tsx` — Precheck-Integration:
      - "Run Precheck" Button in der Sidebar (über dem Deploy-Button)
      - Auto-Run Precheck beim Betreten des Configure-States (Hybrid wie DeployStack)
      - ProductPrecheckPanel zwischen Stack-Configuration und Sidebar
      - Deploy-Button disabled wenn `hasErrors === true`
  - Verhalten:
    - Hybrid-Trigger: Auto-Run beim Laden + Re-Check Button
    - Ergebnis-Anzeige: Accordion pro Stack mit PrecheckItems, Summary-Banner oben
    - Bei Errors in einem Stack: Stack-Accordion automatisch aufklappen
    - Deploy-Button disabled solange ein Stack Errors hat
  - Abhängig von: Feature 8

- [x] **Feature 10: Product Precheck Tests** — Unit, Integration, E2E
  - Unit Tests:
    - `ProductPrecheckResult` Aggregation (canDeploy nur wenn alle Stacks canDeploy)
    - `RunProductPrecheckHandler`: parallele Ausführung, Variable-Merging, Error-Aggregation
    - Edge Cases: Produkt nicht gefunden, leere Stack-Liste, einzelner Stack mit Error
  - Integration Tests:
    - Product Precheck API Endpoint Roundtrip
    - Variable-Merging (shared + per-stack korrekt zusammengeführt)
  - E2E Tests:
    - Happy Path: Produkt mit allen Checks OK → Deploy möglich
    - Error Case: Ein Stack hat Port-Konflikt → Deploy-Button disabled, betroffener Stack markiert
    - Partial Warning: Ein Stack mit Warning → Deploy trotzdem möglich
  - Abhängig von: Feature 8, Feature 9

- [ ] **Dokumentation & Website** — Wiki, Public Website (DE/EN), Roadmap
- [ ] **Phase abschließen** — Alle Tests grün, PR gegen main

## Test-Strategie

- **Unit Tests**:
  - `PrecheckResult`: Aggregation, Edge Cases (leere Liste, nur OK, nur Errors, gemischt)
  - `ImageAvailabilityRule`: Image lokal ✓, Image lokal ✗ + Registry ✓, Image lokal ✗ + Registry ✗, Image lokal ✗ + Registry AuthRequired
  - `PortConflictRule`: Kein Konflikt, Konflikt mit anderem Stack, eigener Stack (Upgrade), Port 0 (random), mehrere Konflikte
  - `VariableValidationRule`: Alle OK, Required fehlt, Pattern-Validierung ✗, Password maskiert in Output
  - `NetworkAvailabilityRule`: rsgo-net existiert, rsgo-net fehlt, Custom-Network gehört anderem Stack
  - `VolumeStatusRule`: Neues Volume, existierendes Volume (Upgrade vs. Fresh Install)
  - `ExistingDeploymentRule`: Kein bestehendes, Running (Upgrade), Installing/Upgrading (Blocked), Failed (Retry)
  - `ProductPrecheckResult`: canDeploy Aggregation (nur true wenn alle Stacks canDeploy)
  - `RunProductPrecheckHandler`: parallele Stack-Prechecks, SharedVariable-Merging, Fehler-Aggregation, Produkt nicht gefunden
- **Integration Tests**: Handler + API Endpoint mit Mock-Docker-Service, Hooks dryRun, Product Precheck Endpoint
- **E2E Tests**: Voller UI-Flow mit Precheck-Panel (Stack + Product), Error/Warning-Szenarien

## Offene Punkte

- [ ] Soll bei einem Upgrade-Deployment im Precheck die aktuelle Version angezeigt werden? (Nice-to-have)
- [ ] Sollen Precheck-Ergebnisse im Deployment-Log (SignalR) als initiale Phase erscheinen?
