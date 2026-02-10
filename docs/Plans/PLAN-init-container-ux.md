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

- [x] **Feature 1: Separate Init-Container-Zählung im Deployment-Progress** – Init-Container und reguläre Services getrennt zählen und in der UI anzeigen
  - Betroffene Dateien:
    - `src/ReadyStackGo.Infrastructure/Services/Deployment/DeploymentEngine.cs` (Progress-Callback erweitern)
    - `src/ReadyStackGo.Infrastructure/Services/Deployment/IDeploymentEngine.cs` (Callback-Delegate)
    - `src/ReadyStackGo.Application/Services/IDeploymentService.cs` (Callback-Delegate)
    - `src/ReadyStackGo.Application/Services/IDeploymentNotificationService.cs` (Notification-Methoden)
    - `src/ReadyStackGo.Api/Hubs/DeploymentNotificationService.cs` (SignalR-Implementierung)
    - `src/ReadyStackGo.WebUi/src/hooks/useDeploymentHub.ts` (DTO erweitern)
    - `src/ReadyStackGo.WebUi/src/pages/Deployments/DeployStack.tsx` (UI-Anzeige)
    - `src/ReadyStackGo.WebUi/src/pages/Deployments/UpgradeStack.tsx` (UI-Anzeige)
    - `src/ReadyStackGo.WebUi/src/pages/Deployments/RollbackStack.tsx` (UI-Anzeige)
  - Abhängig von: -

- [ ] **Feature 2: Real-time Init-Container-Logs während Deployment** – Init-Container-Logs über SignalR an die UI streamen
  - Betroffene Dateien:
    - `src/ReadyStackGo.Infrastructure.Docker/DockerService.cs` (Log-Streaming-Methode)
    - `src/ReadyStackGo.Application/Services/IDockerService.cs` (Interface)
    - `src/ReadyStackGo.Infrastructure/Services/Deployment/DeploymentEngine.cs` (Log-Callback)
    - `src/ReadyStackGo.Application/Services/IDeploymentNotificationService.cs` (Log-Notification)
    - `src/ReadyStackGo.Api/Hubs/DeploymentNotificationService.cs` (SignalR Log-Event)
    - `src/ReadyStackGo.WebUi/src/hooks/useDeploymentHub.ts` (Log-Event-Handler)
    - `src/ReadyStackGo.WebUi/src/pages/Deployments/DeployStack.tsx` (Log-Panel in UI)
  - Abhängig von: Feature 1

- [ ] **Feature 3: Init-Container aus Health Monitoring ausschließen** – Init-Container nicht als reguläre Services im Health Dashboard zählen
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/Services/Impl/HealthMonitoringService.cs` (Filter erweitern)
    - `src/ReadyStackGo.Domain/Deployment/Deployments/DeployedService.cs` (IsInitContainer Property)
    - `src/ReadyStackGo.Domain/Deployment/Deployments/Deployment.cs` (Service-Zählung)
    - `src/ReadyStackGo.WebUi/src/components/health/HealthStackCard.tsx` (Anzeige)
    - `src/ReadyStackGo.WebUi/src/components/dashboard/HealthWidget.tsx` (Anzeige)
  - Abhängig von: -

- [ ] **Feature 4: Automatische Bereinigung beendeter Init-Container** – Nach erfolgreichem Deployment Init-Container entfernen
  - Betroffene Dateien:
    - `src/ReadyStackGo.Infrastructure/Services/Deployment/DeploymentEngine.cs` (Cleanup-Phase)
    - `src/ReadyStackGo.Domain/Deployment/Deployments/Deployment.cs` (Service-Management)
  - Abhängig von: Feature 1

- [ ] **Dokumentation & Website** – Wiki, Public Website, Roadmap
- [ ] **Phase abschließen** – Alle Tests grün, PR gegen main

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
