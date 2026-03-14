# Phase: Maintenance Mode Redesign

## Ziel

Zwei architektonische Schwächen des Maintenance Mode beheben:
1. **Sicherheitsproblem**: Observer überschreibt manuell gesetzten Maintenance Mode nach 30 Sekunden
2. **Granularitäts-Mismatch**: Observer ist auf Produktebene definiert, greift aber pro Stack — bei Multi-Stack-Produkten N Duplikate

## Analyse

### Ist-Zustand
- `OperationMode` Value Object (Normal/Maintenance) auf `Deployment` Entity
- `MaintenanceObserverConfig` auf `ProductDefinition` definiert, bei Deploy auf jedes `Deployment` kopiert
- `MaintenanceObserverService` iteriert alle aktiven Deployments, pollt Observer, sendet `ChangeOperationModeCommand`
- `Deployment.EnterMaintenance(reason)` / `ExitMaintenance()` — kein Trigger-Tracking
- Observer kann manuell gesetzten Maintenance Mode jederzeit aufheben (30s Polling)
- `ChangeOperationModeHandler` orchestriert Container-Lifecycle (Stop/Start)
- API: `PUT /api/environments/{envId}/deployments/{depId}/operation-mode`
- Frontend: `useDeploymentDetailStore` mit `handleEnterMaintenance()`/`handleExitMaintenance()`
- `HealthSnapshot.OperationMode` wird pro Deployment gespeichert
- EF Core: OperationMode als int (0/1), MaintenanceObserverConfig als JSON

### Pattern-Vorbilder
- Value Object Pattern: `OperationMode.cs` — sealed class mit static instances, implicit int conversion
- Aggregate Root: `ProductDeployment.cs` — Status-basierte State Machine mit EnsureValidTransition
- Command/Handler: `ChangeOperationModeCommand/Handler` — MediatR mit Validation + Container Lifecycle
- Domain Events: `OperationModeChanged` — raised in Aggregate, handled in Application layer
- EF Configuration: `DeploymentConfiguration.cs` — HasConversion für Value Objects

### Neues Regelwerk (MaintenanceTrigger)

| Ausgangslage | Aktion | Ergebnis |
|---|---|---|
| Normal | User klickt "Enter Maintenance" | → Maintenance (Trigger=Manual) |
| Normal | Observer sagt Maintenance | → Maintenance (Trigger=Observer) |
| Maintenance (Trigger=Observer) | Observer sagt Normal | → Normal |
| Maintenance (Trigger=Observer) | User klickt "Exit" | → **Blockiert** (Observer sagt noch Maintenance) |
| Maintenance (Trigger=Manual) | Observer sagt Normal | → Nichts (Manual hat Vorrang) |
| Maintenance (Trigger=Manual) | User klickt "Exit" | → Normal |

**Kernprinzip:** Wer Maintenance aktiviert hat, kontrolliert auch das Ende.
- Observer-Maintenance kann nur vom Observer beendet werden (wenn externe Quelle Normal meldet)
- Manual-Maintenance kann nur manuell beendet werden
- Beide können Maintenance jederzeit **aktivieren**
- Kein Trigger-Reset nötig — klare Ownership

## Features / Schritte

### Phase 1 — Trigger-Tracking (Sicherheitsfix)

- [x] **Feature 1: MaintenanceTrigger Value Object + Deployment-Integration** – PR #263
- [x] **Feature 2: Command/Handler + API + Observer-Service anpassen** – PR #264
- [x] **Feature 3: Unit Tests Phase 1** – PR #265

### Phase 2 — Product-Level Maintenance (Granularitätsfix)

- [x] **Feature 4: ProductDeployment um Maintenance erweitern** – PR #266
- [x] **Feature 5: Observer-Service + Handler Refactoring** – PR #267
  - Observer iteriert ProductDeployments (ein Check pro Product statt N Duplikate)
  - `ChangeProductOperationModeCommand` + Handler (stops/starts ALL child stacks)
  - Legacy Deployment-level methods delegate to parent product lookup
- [x] **Feature 6: Unit Tests Phase 2** – PR #268 (23 handler tests)

### Phase 3 — API & UI (UX-Vervollständigung)

- [x] **Feature 7: Product-Level API-Endpoint** – merged with PR #267
  - `PUT /api/environments/{envId}/product-deployments/{id}/operation-mode`
  - 409 Conflict for ownership-blocked transitions
- [x] **Feature 8: Frontend — Product-Level Maintenance UI** – direct commit to integration
  - Backend: operationMode, canEnterMaintenance, canExitMaintenance, maintenanceTrigger in response DTOs
  - Frontend: @rsgo/core API functions, store hook with maintenance actions, UI controls on ProductDeploymentDetail
  - AMS UI: not affected (new store fields unused)
- [-] **Feature 9: E2E Tests Phase 3** — Skipped (requires running Docker environment with deployed product)
- [-] **Dokumentation & Website** — Skipped (will be done in docs phase)
- [ ] **Phase abschließen** – Integration PR gegen main

## Offene Punkte

Alle geklärt.

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Release-Strategie | Sofort Phase 1, alle zusammen | Alle zusammen | Ein Epic, 3 Phasen als Feature-Branches |
| Trigger-Persistenz | JSON-Column, Owned Entity, Separate Columns | JSON-Column | Konsistent mit MaintenanceObserverConfig, Value Object als Einheit |
| Exit-Verhalten bei Observer-Maintenance | Erlauben mit Trigger-Reset, Blockieren | Blockieren | Einfachere Logik, kein Trigger-Reset nötig, User kann sich nicht versehentlich aus echtem Maintenance klicken |
| Standalone-Deployments | Eigener OperationMode, Kein Maintenance | Kein Maintenance | Alle Deployments haben ein Parent-Product, Standalone existiert nicht |
| Hook/API-Key Trigger-Source | Eigene Source, Manual | Manual | Semantisch gleichzusetzen mit manueller Aktion |
