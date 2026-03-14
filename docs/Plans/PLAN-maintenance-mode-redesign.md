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
| Maintenance (Trigger=Observer) | Observer sagt Normal | → Normal ✓ |
| Maintenance (Trigger=Observer) | User klickt "Exit" | → **Blockiert** (Observer sagt noch Maintenance) |
| Maintenance (Trigger=Manual) | Observer sagt Normal | → Nichts (Manual hat Vorrang) |
| Maintenance (Trigger=Manual) | User klickt "Exit" | → Normal ✓ |

**Kernprinzip:** Wer Maintenance aktiviert hat, kontrolliert auch das Ende.
- Observer-Maintenance kann nur vom Observer beendet werden (wenn externe Quelle Normal meldet)
- Manual-Maintenance kann nur manuell beendet werden
- Beide können Maintenance jederzeit **aktivieren**
- Kein Trigger-Reset nötig — klare Ownership

## Features / Schritte

### Phase 1 — Trigger-Tracking (Sicherheitsfix)

- [ ] **Feature 1: MaintenanceTrigger Value Object + Deployment-Integration** – Neues Value Object + Deployment-Methoden anpassen
  - Neue Datei: `Domain/Deployment/Observers/MaintenanceTrigger.cs`
  - `MaintenanceTriggerSource` Enum: Manual, Observer
  - Properties: Source, Reason, TriggeredAtUtc, TriggeredBy
  - `Deployment.MaintenanceTrigger?` Property
  - `EnterMaintenance(MaintenanceTrigger trigger)` statt `EnterMaintenance(string? reason)`
  - `ExitMaintenance(MaintenanceTriggerSource source)` mit Validierung
  - `OperationModeChanged` Event um `MaintenanceTrigger?` erweitern
  - EF Core: MaintenanceTrigger als JSON-Column auf Deployment
  - Abhängig von: -

- [ ] **Feature 2: Command/Handler + API + Observer-Service anpassen** – Trigger durch alle Schichten durchreichen
  - `ChangeOperationModeCommand`: neuer `Source` Parameter (Default: "Manual")
  - `ChangeOperationModeHandler`: erstellt MaintenanceTrigger, reicht an Domain
  - Handler: bei Exit mit Source=Manual → Observer live abfragen, blockieren wenn Observer Maintenance meldet
  - `ChangeOperationModeEndpoint`: Request um optional `Source` erweitern
  - `MaintenanceObserverService`: sendet Source="Observer" im Command
  - Observer: vor Exit prüfen ob `Trigger.Source == Manual` → NICHT beenden
  - Abhängig von: Feature 1

- [ ] **Feature 3: Unit Tests Phase 1** – Umfassende Tests für Trigger-Logik
  - Tests für MaintenanceTrigger Value Object (Erstellung, Validierung, Equality)
  - Tests für Deployment.EnterMaintenance/ExitMaintenance mit Trigger
  - Tests: Manual-Exit blockiert wenn Observer Maintenance meldet
  - Tests: Observer-Exit blockiert wenn Trigger=Manual
  - Tests: Observer-Enter funktioniert immer
  - Tests: Manual-Enter funktioniert immer
  - Tests für Handler mit Source-Parameter
  - Tests für Endpoint mit Source-Feld
  - Abhängig von: Feature 1-2

### Phase 2 — Product-Level Maintenance (Granularitätsfix)

- [ ] **Feature 4: ProductDeployment um Maintenance erweitern** – OperationMode + Trigger auf ProductDeployment
  - `ProductDeployment`: OperationMode, MaintenanceTrigger?, MaintenanceObserverConfig? Properties
  - `EnterMaintenance(trigger)` / `ExitMaintenance(source)` Methoden (gleiche Regeln wie Phase 1)
  - Domain Event: `ProductMaintenanceModeChanged`
  - EF Core: Properties auf ProductDeployment konfigurieren
  - Abhängig von: Phase 1

- [ ] **Feature 5: Observer-Service + Handler Refactoring** – ProductDeployments statt Deployments
  - Observer iteriert ProductDeployments (ein Check pro Product statt N Duplikate)
  - Neuer `ChangeProductOperationModeCommand` + Handler
  - Handler stoppt/startet Container ALLER Child-Stacks
  - `Deployment.OperationMode` + `MaintenanceObserverConfig` entfernen (kommt vom Parent)
  - Deployment-Level ChangeOperationMode Endpoint: 409 Conflict (alle Deployments haben Parent)
  - Abhängig von: Feature 4

- [ ] **Feature 6: Unit + Integration Tests Phase 2**
  - ProductDeployment EnterMaintenance/ExitMaintenance Tests
  - Observer-Service iteriert ProductDeployments Tests
  - Container-Lifecycle für alle Child-Stacks Tests
  - 409 Conflict bei Deployment-Level Endpoint Tests
  - Abhängig von: Feature 4-5

### Phase 3 — API & UI (UX-Vervollständigung)

- [ ] **Feature 7: Product-Level API-Endpoint** – Neuer Endpoint für ProductDeployment Maintenance
  - `PUT /api/environments/{envId}/product-deployments/{id}/operation-mode`
  - Request: `{ mode: "Maintenance"|"Normal", reason?: string }`
  - Response: Success/Fail mit PreviousMode/NewMode + Trigger-Info
  - Abhängig von: Feature 5

- [ ] **Feature 8: Frontend — Product-Level Maintenance UI**
  - Maintenance-Button auf Product-Detailseite
  - Trigger-Anzeige (Manual/Observer, Zeitpunkt, Grund)
  - Blockierungs-Meldung bei Observer-Maintenance: "Cannot exit while observer reports maintenance"
  - API-Client + Hook für neuen Endpoint
  - Stack-Detailseite: Maintenance-Button entfernen, Hinweis "Controlled by product" anzeigen
  - Abhängig von: Feature 7

- [ ] **Feature 9: E2E Tests Phase 3**
  - Manual Enter/Exit Maintenance auf Product-Ebene
  - Observer-blockierter Exit (Fehlermeldung sichtbar)
  - Stack-Detailseite zeigt "Controlled by product"
  - Abhängig von: Feature 8

- [ ] **Dokumentation & Website** – Maintenance Mode Doku aktualisieren (DE/EN)
- [ ] **Phase abschließen** – Alle Tests grün, PR gegen main

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
