# Phase: Product Container Control

## Ziel

Stop und Restart aller Container eines Product Deployments — auf Produkt-Ebene (alle Stacks) oder für einzelne Stacks. Umfasst API-Endpoints, UI-Buttons auf der ProductDeploymentDetail-Seite und Hook-Endpoints für CI/CD-Pipelines. Restart = Stop + Start sequenziell (kein Docker Restart).

**Nicht-Ziel**: Änderung der ProductDeployment State Machine (Stop/Restart ändert den Deployment-Status nicht — Container werden nur angehalten/gestartet). Kein Restart einzelner Container (existiert bereits über Container-Management-Seite).

## Analyse

### Bestehende Infrastruktur

| Komponente | Status | Details |
|---|---|---|
| `IDockerService.StopStackContainersAsync(envId, stackName)` | **Vorhanden** | Stoppt alle Running-Container eines Stacks (filtered by `rsgo.stack` label, `WaitBeforeKillSeconds = 10`) |
| `IDockerService.StartStackContainersAsync(envId, stackName)` | **Vorhanden** | Startet alle Exited/Created-Container eines Stacks |
| `IDockerService.StopContainerAsync(envId, containerId)` | **Vorhanden** | Stoppt einzelnen Container |
| `IDockerService.StartContainerAsync(envId, containerId)` | **Vorhanden** | Startet einzelnen Container |
| Container-Labels (`rsgo.stack`, `rsgo.product`) | **Vorhanden** | Container sind über `rsgo.stack = DeploymentStackName` identifizierbar |
| `ProductDeployment.Stacks[].DeploymentStackName` | **Vorhanden** | Mapping ProductStack → Docker Stack Name |
| Per-Container Start/Stop UI | **Vorhanden** | `Containers.tsx` mit Start/Stop-Buttons pro Container |
| Bulk Stop/Restart per Product | **Fehlt** | Kein Command, Handler, Endpoint oder UI |

### Bestehende Nutzung von StopStack/StartStack

`ChangeOperationModeHandler.cs` (Maintenance Mode) nutzt `StopStackContainersAsync` / `StartStackContainersAsync` für einzelne Deployments. Dieses Pattern wird auf Product-Ebene erweitert.

### Betroffene Bounded Contexts

- **Domain**: Keine Änderung — ProductDeployment State Machine bleibt unverändert (Stop/Restart ist eine Container-Operation, kein Deployment-Status-Wechsel)
- **Application**: Neue Commands + Handlers: `StopProductContainersCommand`, `RestartProductContainersCommand`
- **Infrastructure**: Keine Änderung — `IDockerService` hat bereits alle nötigen Methoden
- **API**: Neue Endpoints für Product Stop/Restart + Hook-Erweiterung
- **WebUI**: Stop/Restart-Buttons auf `ProductDeploymentDetail.tsx`, optional per Stack

### Pattern-Vorbilder

| Aspekt | Vorbild-Datei |
|--------|--------------|
| Multi-Stack Orchestration | `RemoveProductHandler.cs` (Loop über Stacks, SignalR Progress) |
| Stack Container Operations | `ChangeOperationModeHandler.cs` (StopStackContainersAsync/StartStackContainersAsync) |
| Product Endpoint Pattern | `RetryProductEndpoint.cs` (`POST .../product-deployments/{id}/...`) |
| Hook Pattern | `RedeployStackCommand.cs` (Hook mit productId Support) |

## Features / Schritte

### Backend

- [x] **Feature 1: StopProductContainers Command + Handler** — Alle Container eines Product Deployments stoppen
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/UseCases/ProductDeployments/StopContainers/StopProductContainersCommand.cs` (NEU)
    - `src/ReadyStackGo.Application/UseCases/ProductDeployments/StopContainers/StopProductContainersHandler.cs` (NEU)
  - Command-Shape:
    ```csharp
    public record StopProductContainersCommand(
        string EnvironmentId,
        string ProductDeploymentId,
        List<string>? StackNames  // null = alle, sonst nur benannte Stacks
    ) : IRequest<StopProductContainersResponse>;

    public record StopProductContainersResponse(
        bool Success, string Message,
        int TotalStacks, int StoppedStacks, int FailedStacks,
        List<StackContainerResult> Results);

    public record StackContainerResult(
        string StackName, bool Success, int ContainersStopped, string? Error);
    ```
  - Handler-Flow:
    1. Load ProductDeployment, prüfe Status ist `Running` oder `PartiallyRunning`
    2. Resolve EnvironmentId
    3. Bestimme Stacks: Wenn `StackNames == null` → alle Stacks mit `DeploymentStackName != null`, sonst nur benannte (case-insensitive Match)
    4. Für jeden Stack: `_dockerService.StopStackContainersAsync(environmentId, stack.DeploymentStackName)`
    5. Ergebnis pro Stack sammeln (Anzahl gestoppte Container, Fehler)
    6. Response mit Gesamt-Summary
  - **Kein State-Machine-Übergang** — ProductDeployment bleibt im aktuellen Status
  - Pattern-Vorlage: `RemoveProductHandler.cs` (Multi-Stack Loop), `ChangeOperationModeHandler.cs` (Stop-Logik)
  - Abhängig von: -
  - Tests:
    - Unit: Alle Stacks stoppen, einzelne Stacks stoppen, unbekannter Stack-Name → Fehler
    - Unit: ProductDeployment nicht gefunden, falscher Status (z.B. `Deploying`)
    - Unit: Teilweiser Fehler (ein Stack schlägt fehl, Rest erfolgreich)

- [x] **Feature 2: RestartProductContainers Command + Handler** — Alle Container stoppen und wieder starten
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/UseCases/ProductDeployments/RestartContainers/RestartProductContainersCommand.cs` (NEU)
    - `src/ReadyStackGo.Application/UseCases/ProductDeployments/RestartContainers/RestartProductContainersHandler.cs` (NEU)
  - Command-Shape: Analog zu Stop, Response enthält zusätzlich `ContainersStarted` pro Stack
  - Handler-Flow:
    1. Wie Stop (Schritte 1-3)
    2. Für jeden Stack:
       a. `_dockerService.StopStackContainersAsync(...)` — Stop
       b. `_dockerService.StartStackContainersAsync(...)` — Start
    3. Ergebnis pro Stack sammeln
    4. Response mit Gesamt-Summary
  - Pattern-Vorlage: `StopProductContainersHandler` (Feature 1)
  - Abhängig von: Feature 1 (gleiche Validierungs-Logik, ggf. Shared Helper)
  - Tests:
    - Unit: Stop + Start sequenziell aufgerufen, Reihenfolge korrekt
    - Unit: Stop fehlgeschlagen → Start wird NICHT aufgerufen für diesen Stack
    - Unit: Teilweiser Fehler

### API

- [x] **Feature 3: API Endpoints** — FastEndpoints für Stop/Restart
  - Betroffene Dateien:
    - `src/ReadyStackGo.Api/Endpoints/Deployments/StopProductContainersEndpoint.cs` (NEU)
    - `src/ReadyStackGo.Api/Endpoints/Deployments/RestartProductContainersEndpoint.cs` (NEU)
  - Routes:
    - `POST /api/environments/{environmentId}/product-deployments/{productDeploymentId}/stop-containers`
    - `POST /api/environments/{environmentId}/product-deployments/{productDeploymentId}/restart-containers`
  - Permission: `Deployments.Execute`
  - Request-Body:
    ```json
    { "stackNames": ["Analytics"] }  // optional, null = alle
    ```
  - Pattern-Vorlage: `RetryProductEndpoint.cs`
  - Abhängig von: Feature 1, Feature 2
  - Tests:
    - Integration: Endpoint erreichbar, Permission-Check, Response-Format

### UI

- [x] **Feature 4: UI — Stop/Restart Buttons auf ProductDeploymentDetail** — Buttons + Bestätigungsdialog
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/src/pages/Deployments/ProductDeploymentDetail.tsx` — Buttons hinzufügen
    - `src/ReadyStackGo.WebUi/src/api/productDeployments.ts` — `stopProductContainers()`, `restartProductContainers()` API-Funktionen
  - UI-Design:
    - Product-Level: "Stop All Containers" und "Restart All Containers" Buttons (nur bei `Running`/`PartiallyRunning`)
    - Per-Stack: Kleine Stop/Restart-Icons neben jedem Stack in der Stack-Liste (nur für Stacks mit Status `Running`)
    - Bestätigungsdialog: "Are you sure you want to stop/restart all containers of 'Product Name'?" mit Stack-Liste
    - Ergebnis-Anzeige: Inline-Feedback welche Stacks gestoppt/gestartet wurden
  - `canStop` / `canRestart` Flags in Response-DTO: Analog zu `canRetry`, `canUpgrade` — basieren auf ProductDeployment Status
  - Pattern-Vorlage: Bestehende Action-Buttons in `ProductDeploymentDetail.tsx`
  - Abhängig von: Feature 3

### Hook

- [x] **Feature 5: Hook — Stop/Restart via API Key** — `/api/hooks/stop-containers` und `/api/hooks/restart-containers`
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/UseCases/Hooks/StopContainers/StopContainersViaHookCommand.cs` (NEU)
    - `src/ReadyStackGo.Application/UseCases/Hooks/RestartContainers/RestartContainersViaHookCommand.cs` (NEU)
    - `src/ReadyStackGo.Api/Endpoints/Hooks/StopContainersEndpoint.cs` (NEU)
    - `src/ReadyStackGo.Api/Endpoints/Hooks/RestartContainersEndpoint.cs` (NEU)
  - Request-Format:
    ```json
    {
      "productId": "ams.project",           // required
      "stackDefinitionName": "Analytics",    // optional, null = alle
      "environmentId": "..."                 // optional, falls API Key env-scoped
    }
  - Logik:
    1. Resolve `productId` → aktives ProductDeployment via `GetActiveByProductGroupId`
    2. Falls `stackDefinitionName` gesetzt: nur diesen Stack
    3. Dispatch `StopProductContainersCommand` / `RestartProductContainersCommand`
  - Permission: Neue Permission `StopContainers` / `RestartContainers` (oder Reuse `Redeploy`?)
  - Pattern-Vorlage: `DeployViaHookHandler` (ProductId-Resolution)
  - Abhängig von: Feature 1, Feature 2
  - Tests:
    - Unit: Product Resolve, einzelner Stack, alle Stacks
    - Integration: Hook-Endpoint mit API Key

### Abschluss

- [x] **Dokumentation & Website** — Wiki, Public Website (DE/EN), Roadmap
- [x] **Phase abschließen** — Alle Tests grün, PR gegen main

## Test-Strategie

### Unit Tests
- **Commands**: Stop/Restart mit allen Stacks, einzelnen Stacks, ungültigen Stack-Namen
- **Validation**: Falscher Status (Deploying, Removing), nicht existierendes ProductDeployment
- **Fehler-Handling**: Teilweiser Fehler (ein Stack fehlgeschlagen), Docker-Exception
- **Restart-Logik**: Stop-Fehler verhindert Start, Reihenfolge Stop→Start

### Integration Tests
- **API**: Endpoints erreichbar, Permission-Check, Response-Format
- **Hook**: API Key Auth, ProductId-Resolution

## Offene Punkte

- [x] Braucht es neue API Key Permissions (`StopContainers`, `RestartContainers`) oder reichen bestehende (`Redeploy`)? → **Klären bei Implementierung**
- [x] Soll es eine Notification nach Stop/Restart geben? → **Ja, Info-Level Notification analog zu anderen Operationen**

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| State Machine | A) Neuer Status (Stopped/Restarting), B) Status unverändert | **B) Unverändert** | Stop/Restart ist eine Container-Operation, kein Deployment-Lifecycle-Event. Health-Sync erkennt gestoppte Container automatisch. |
| Restart-Implementierung | A) Docker Restart API, B) Stop + Start sequenziell | **B) Stop + Start** | Docker Restart API existiert nicht in IDockerService. Stop+Start nutzt bestehende Methoden und ist konsistent mit Maintenance Mode. |
| Hook-Endpoints | A) Neue Endpoints `/api/hooks/stop-containers` + `/restart-containers`, B) Bestehende erweitern | **A) Neue Endpoints** | Klare Trennung der Verantwortlichkeiten. Bestehende Hooks haben anderen Scope (Deploy/Redeploy/Upgrade). |
| Per-Stack Control | A) Nur Product-Level, B) Optional per Stack | **B) Optional per Stack** | Flexibler — Pipeline kann einzelne Stacks neustarten ohne das gesamte Product zu beeinflussen. |
