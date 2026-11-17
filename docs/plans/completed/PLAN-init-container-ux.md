# Phase: Init Container UI/UX Improvements (v0.18)

## Ziel
Init Container Handling in der UI verbessern: Separate Zählung, Log-Streaming, Health-Monitoring-Ausschluss und automatische Bereinigung. Die Benutzererfahrung bei Deployments mit Init-Containern soll deutlich verbessert werden.

## Analyse

### Bestehende Init-Container-Implementierung
- **Domain**: `ServiceLifecycle` Enum (`Service`, `Init`) in `Domain/StackManagement/Manifests/ServiceLifecycle.cs`
- **Manifest**: Services mit `lifecycle: init` werden als Init-Container behandelt
- **DeploymentEngine**: Trennt Init-Container von regulären Services, führt Init-Container zuerst aus (Phase 70-80%), danach reguläre Services (80-100%)
- **Container Labels**: `rsgo.lifecycle=init` bzw. `rsgo.lifecycle=service` auf jedem Container
- **Restart Policy**: Init-Container nutzen `restart: no`, reguläre Services `restart: unless-stopped`
- **Wait-Mechanismus**: `StartInitContainerAndWaitAsync()` pollt alle 500ms mit 5-Min-Timeout

### Aktuelle Schwächen
1. **Zählung**: `totalSteps = plan.Steps.Count` mischt Init- und reguläre Container. UI zeigt "Services: 1/5" ohne Unterscheidung
2. **Logs**: Init-Container-Logs werden nur bei Fehlern abgerufen (letzte 50 Zeilen). Kein Echtzeit-Streaming
3. **Health Monitoring**: Init-Container werden in `CollectSelfHealthAsync()` mitgezählt. Exited(0) = Healthy, aber sie tauchen im Gesamtcount auf
4. **Cleanup**: Keine automatische Bereinigung beendeter Init-Container
5. **UI**: Kein visueller Unterschied zwischen Init-Containern und regulären Services in der Container-Ansicht

### Betroffene Architektur-Schichten
- **Frontend** (React/TSX): `DeployStack.tsx`, `DeploymentDetail.tsx`, `Containers.tsx`, `HealthDashboard.tsx`, `useDeploymentHub.ts`
- **SignalR**: `DeploymentHub.cs`, `IDeploymentNotificationService.cs`
- **Application**: `IDeploymentService.cs`, `DeploymentService.cs`, `HealthMonitoringService.cs`
- **Infrastructure**: `DeploymentEngine.cs`, `DockerService.cs`
- **Domain**: `DeployedService.cs`, `Deployment.cs`

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten und logischem Aufbau:

- [x] **Feature 1: Separate Init-Container-Zählung im Deployment-Progress** (PR #73)
  - Init-Container und reguläre Services getrennt zählen und in der UI anzeigen
  - `DeploymentProgressCallback` um `totalInitContainers` / `completedInitContainers` erweitert
  - UI zeigt während Init-Phase "Init Containers: X/Y", während Service-Phase "Services: X/Y"

- [x] **Feature 2: Real-time Init-Container-Logs während Deployment** (PR #74)
  - Init-Container-Logs über SignalR an die UI streamen
  - `StreamContainerLogsAsync` in DockerService mit `IAsyncEnumerable<string>`
  - `InitContainerLogCallback` Delegate durch alle Schichten
  - Collapsible Log-Panel in DeployStack/UpgradeStack/RollbackStack UI

- [x] **Feature 3: Init-Container aus Health Monitoring ausschließen** (PR #75)
  - Container mit `rsgo.lifecycle=init` Label werden in `CollectSelfHealthAsync()` gefiltert
  - `IsInitContainer()` Helper in `HealthMonitoringService`
  - 3 Unit Tests: Init excluded, only-init returns empty, exited regular service still monitored

- [x] **Feature 4: Automatische Bereinigung beendeter Init-Container** (PR #76)
  - Nach erfolgreicher Init-Phase: `RemoveContainerAsync` für jeden Init-Container
  - Init-Container aus `result.DeployedContainers` entfernt (nicht als Services persistiert)
  - Cleanup-Fehler sind nicht-fatal (Deployment läuft weiter)
  - 4 Unit Tests: Erfolg, Fehler, non-fatal, mehrere Container

- [x] **Dokumentation & Website** – Roadmap, Container-Lifecycle, Health-Monitoring
- [x] **Phase abschließen** – Alle Tests grün, PR gegen main

## Offene Punkte
- [x] **Log-Streaming Ansatz** → SignalR-Streaming gewählt
- [x] **Health-Monitoring-Ausschluss** → Komplett ausschließen gewählt
- [x] **Cleanup-Zeitpunkt** → Am Ende der Init-Phase, nach erfolgreicher Completion aller Init-Container
- [x] **Deployment-Detail-Ansicht** → Erfolgs-/Fehlerstatus mit Möglichkeit, Logs anzuschauen
- [x] **TODO im Code entfernen**: `DeploymentEngine.cs:349` hat `// TODO: Remove this delay after testing` – `Task.Delay(2000)` entfernt

## Entscheidungen
| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Log-Streaming | A) SignalR-Event, B) API-Polling | A) SignalR | Echtzeit-Charakter wichtig für UX während Init-Phase |
| Health-Ausschluss | A) Komplett entfernen, B) Separat anzeigen | A) Komplett | Init-Container sind keine langlebigen Services, gehören nicht ins Health Dashboard |
| Cleanup-Zeitpunkt | A) Sofort nach Exit, B) Nach Init-Phase, C) Kein Cleanup | B) Nach Init-Phase | Logs bleiben verfügbar falls einzelne Init-Container fehlschlagen. Container werden am Ende der gesamten Init-Phase entfernt. |
| UI-Darstellung Init | A) Separate Sektion, B) Badge/Icon, C) Minimal | C) Minimal | Nur Erfolg/Fehler-Status anzeigen mit Option die Logs einzusehen. Kein Bloat in der Deployment-Detail-Ansicht. |
