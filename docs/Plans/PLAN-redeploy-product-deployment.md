# Phase: Redeploy Product Deployment

## Ziel

Ermöglicht das erneute Deployen einzelner Stacks oder aller Stacks eines laufenden Product Deployments — ohne Versionsänderung. Typischer Anwendungsfall: ein neues Docker Image wurde gepusht (gleicher Tag) und soll frisch gepullt und deployed werden. Betrifft API, UI und Hook-Integration.

**Nicht-Ziel**: Upgrade (Versionsänderung) oder Retry (nur fehlgeschlagene Stacks). Redeploy betrifft ausschließlich **laufende** Product Deployments und setzt alle (oder ausgewählte) Stacks neu auf.

## Analyse

### Bestehende Architektur

| Aktion | Zielstatus | Neue Entity? | Betroffene Stacks | Variable-Quelle |
|--------|-----------|-------------|-------------------|----------------|
| **Deploy** | `Deploying` | Ja (neu) | Alle | Wizard-Input |
| **Upgrade** | `Upgrading` | Ja (neu) | Alle (neue Version) | 4-Tier-Merge (Defaults → Stored → Shared → Per-Stack) |
| **Retry** | `Deploying` | Nein (in-place) | Nur `Failed` | Gespeicherte `stack.Variables` |
| **Remove** | `Removing` | Nein (in-place) | Alle | - |
| **Redeploy** (NEU) | `Redeploying` | Nein (in-place) | Alle oder einzelne | Gespeicherte Variables + optionale Overrides |

### Lücken im Ist-Zustand

1. **State Machine**: Kein `Running → Redeploying` Übergang. Aktuell: `Running → Upgrading | Removing`.
2. **Domain**: Kein `StartRedeploy()` auf `ProductDeployment`, kein `CanRedeploy` Guard.
3. **Application**: Kein `RedeployProductCommand` / Handler.
4. **API**: Kein Redeploy-Endpoint für Product Deployments.
5. **WebUI**: Kein "Redeploy"-Button auf `ProductDeploymentDetail.tsx`.
6. **Hooks**: `/api/hooks/redeploy` kennt nur standalone Stacks, keine Product Deployments.

### Betroffene Bounded Contexts

- **Domain**: `ProductDeployment` (State Machine, neue Methoden), `ProductDeploymentStatus` (neuer Wert), `ProductStackDeployment` (Reset-Logik)
- **Application**: Neuer `RedeployProductCommand` + Handler, Event-Handling
- **Infrastructure**: Repository (keine Änderung erwartet — `Save` reicht)
- **API**: Neuer Endpoint `POST .../product-deployments/{id}/redeploy`, Hook-Erweiterung
- **WebUI**: Redeploy-Button, Redeploy-Seite (Bestätigung + Fortschritt), API-Client-Funktion

### Pattern-Vorbilder

| Aspekt | Vorbild-Datei |
|--------|--------------|
| State Machine Transition | `ProductDeployment.StartRetry()` (line ~430) |
| Handler-Pattern | `RetryProductHandler.cs` (in-place Mutation, DeployStackCommand per Stack) |
| Endpoint-Pattern | `RetryProductEndpoint.cs` (`POST .../retry`) |
| UI-Pattern | `RetryProduct.tsx` (Confirm + Progress) |
| Hook-Pattern | `RedeployStackCommand.cs` (standalone Redeploy) |
| Variable-Merge | `DeployViaHookHandler` step 2 (stored base + webhook overrides) |

## Features / Schritte

### Kernfunktionalität

- [ ] **Feature 1: Domain — Redeploying Status & State Machine** — `ProductDeploymentStatus.Redeploying`, Transition `Running → Redeploying`, Guards
  - Betroffene Dateien:
    - `src/ReadyStackGo.Domain/Deployment/ProductDeployments/ProductDeploymentStatus.cs` — neuer Enum-Wert `Redeploying = 7`
    - `src/ReadyStackGo.Domain/Deployment/ProductDeployments/ProductDeployment.cs`:
      - `ValidTransitions`: `Running → Redeploying`, `Redeploying → Running | PartiallyRunning | Failed`
      - `CanRedeploy` Property: `Status == Running` (nur laufende Deployments)
      - `StartRedeploy(UserId initiatedBy, IReadOnlyList<string>? stackNames = null)`:
        - Wenn `stackNames == null`: Alle Stacks auf Pending resetten
        - Wenn `stackNames != null`: Nur die genannten Stacks resetten, Rest bleibt Running
      - `IsInProgress` Property um `Redeploying` erweitern
      - Notification-Factory für Redeploy-Result
    - `src/ReadyStackGo.Domain/Deployment/ProductDeployments/ProductStackDeployment.cs` — `ResetToPending()` existiert bereits, wird wiederverwendet
  - Pattern-Vorlage: `StartRetry()` (resets Failed stacks) — Redeploy resets alle (oder ausgewählte) Stacks
  - Abhängig von: -
  - Tests:
    - Unit: `CanRedeploy` nur im Status `Running`, Transition-Guards, einzelne vs. alle Stacks resetten
    - Unit: Ungültige Übergänge (z.B. `Deploying → Redeploying` muss scheitern)

- [ ] **Feature 2: Application — RedeployProductCommand & Handler** — MediatR Command + orchestrierter Redeploy-Flow
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/UseCases/Deployments/RedeployProduct/RedeployProductCommand.cs` (NEU)
    - `src/ReadyStackGo.Application/UseCases/Deployments/RedeployProduct/RedeployProductHandler.cs` (NEU)
  - Command-Shape:
    ```csharp
    public record RedeployProductCommand(
        string EnvironmentId,
        string ProductDeploymentId,
        List<string>? StackNames,           // null = alle, sonst nur benannte Stacks
        Dictionary<string, string>? VariableOverrides,  // optionale Overrides
        string? SessionId
    ) : IRequest<RedeployProductResponse>;
    ```
  - Handler-Flow (analog zu RetryProductHandler):
    1. Load ProductDeployment, prüfe `CanRedeploy`
    2. Lock Environment
    3. Call `productDeployment.StartRedeploy(userId, stackNames)`
    4. Persist
    5. Für jeden Stack in deploy order (nur Pending-Stacks):
       - Merge Variables: `stack.Variables` als Basis + `VariableOverrides`
       - Dispatch `DeployStackCommand` mit `SuppressNotification: true`
       - `StartStack` → `CompleteStack` / `FailStack`
    6. `FinalizeProductStatus()`, Persist
    7. SignalR Notifications + In-App Notification
  - Pattern-Vorlage: `RetryProductHandler.cs`
  - Abhängig von: Feature 1
  - Tests:
    - Unit: Happy Path (alle Stacks), Single Stack, Variable Merge, Fehler-Handling
    - Unit: Ungültige Inputs (nicht existierendes ProductDeployment, falscher Status, unbekannter Stack-Name)

- [ ] **Feature 3: API — Redeploy Endpoint** — FastEndpoint für Product Redeploy
  - Betroffene Dateien:
    - `src/ReadyStackGo.Api/Endpoints/Deployments/RedeployProductEndpoint.cs` (NEU)
    - `src/ReadyStackGo.Api/Endpoints/Deployments/Dtos/RedeployProductRequest.cs` (NEU)
  - Route: `POST /api/environments/{environmentId}/product-deployments/{productDeploymentId}/redeploy`
  - Permission: `Deployments.Execute`
  - Request-Body:
    ```json
    {
      "stackNames": ["Analytics"],    // optional, null = alle
      "variables": { "key": "val" }   // optional overrides
    }
    ```
  - Response: `RedeployProductResponse` (success, message, productDeploymentId)
  - Pattern-Vorlage: `RetryProductEndpoint.cs`
  - Abhängig von: Feature 2
  - Tests:
    - Integration: Endpoint erreichbar, Permission-Check, Response-Format

- [ ] **Feature 4: WebUI — Redeploy Button & Seite** — UI für Product Redeploy
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/src/pages/Deployments/ProductDeploymentDetail.tsx` — Redeploy-Button hinzufügen
    - `src/ReadyStackGo.WebUi/src/pages/Deployments/RedeployProduct.tsx` (NEU) — Bestätigungsseite mit Stack-Auswahl
    - `src/ReadyStackGo.WebUi/src/api/productDeployments.ts` — `redeployProduct()` API-Funktion
    - `src/ReadyStackGo.WebUi/src/App.tsx` — Route hinzufügen
  - UI-Flow:
    1. `ProductDeploymentDetail`: "Redeploy" Button (nur wenn `canRedeploy`)
    2. `RedeployProduct` Page:
       - Produkt-Info (Name, Version, Stacks)
       - Stack-Auswahl: "All Stacks" (default) oder individuelle Checkboxen
       - Optional: Variable Overrides Eingabe
       - "Redeploy" Confirm-Button
    3. Nach Bestätigung: Deployment Progress Panel (bestehende `DeploymentProgressPanel` Komponente)
  - Response-DTO erweitern: `canRedeploy` Flag in `GetProductDeploymentResponse`
  - Pattern-Vorlage: `RetryProduct.tsx`, `UpgradeProduct.tsx`
  - Abhängig von: Feature 3

### Hook-Integration

- [ ] **Feature 5: Hook — Product Redeploy via Hook** — `/api/hooks/redeploy` für Product Deployments erweitern
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/UseCases/Hooks/RedeployStack/RedeployStackCommand.cs` — erweitern oder neuen Command
    - `src/ReadyStackGo.Api/Endpoints/Hooks/RedeployEndpoint.cs` — erweitern um `productId` Support
  - Neues Request-Format (additiv, abwärtskompatibel):
    ```json
    {
      "stackName": "my-stack",
      "environmentId": "...",
      "productId": "ams.project",          // NEU: optional
      "stackDefinitionName": "Analytics",   // NEU: optional, null = alle Stacks
      "variables": { "key": "val" }         // optional overrides
    }
    ```
  - Logik:
    - Wenn `productId` gesetzt: Finde aktives ProductDeployment via `GetActiveByProductGroupId`
    - Wenn `stackDefinitionName` gesetzt: Redeploy nur diesen Stack
    - Wenn `stackDefinitionName` null: Redeploy alle Stacks
    - Dispatch `RedeployProductCommand` (nicht `DeployStackCommand` direkt)
  - Permission: `Redeploy` (existiert bereits)
  - Pattern-Vorlage: `DeployViaHookHandler` (ProductId-Resolution)
  - Abhängig von: Feature 2
  - Tests:
    - Unit: Product Redeploy via Hook (alle Stacks, einzelner Stack, Variable Merge)
    - Integration: Hook-Endpoint mit API Key Auth

### Abschluss

- [ ] **Dokumentation & Website** — Wiki, Public Website (DE/EN), Roadmap
- [ ] **Phase abschließen** — Alle Tests grün, PR gegen main

## Test-Strategie

### Unit Tests
- **Domain**: `CanRedeploy` Guards, `StartRedeploy()` mit allen/einzelnen Stacks, ungültige Übergänge, Status-Finalisierung nach Redeploy
- **Application**: `RedeployProductHandler` — Happy Path, Single Stack, Variable Merge, ProductDeployment not found, wrong status, unknown stack name, environment lock
- **Hooks**: Product Redeploy via Hook — Routing-Logik, Fallback auf standalone Redeploy

### Integration Tests
- **API**: Endpoint erreichbar, Permission-Check, Response-Format, End-to-End mit TestContainers
- **Hooks**: Hook-Endpoint mit API Key, Product Redeploy Flow

### E2E Tests (Playwright)
- Redeploy All Stacks: ProductDeploymentDetail → Redeploy → Confirm → Progress → Success
- Redeploy Single Stack: Stack-Auswahl → Confirm → Progress → Nur ausgewählter Stack deployt
- Error Flow: Redeploy bei nicht-laufendem Deployment zeigt Fehlermeldung

## Offene Punkte

- [x] Soll Redeploy einzelne Stacks unterstützen? → **Ja, optional via `stackNames` Parameter**
- [x] Soll ein neuer Status oder ein bestehender verwendet werden? → **Neuer Status `Redeploying`**
- [x] Variable Overrides? → **Ja, Merge-Logik wie bei Deploy Hook (stored + overrides)**
- [x] Roadmap-Position? → **Nach Notifications Phase 2**

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Status | A) Neuer `Redeploying`, B) Reuse `Upgrading`, C) In `Running` bleiben | **A) `Redeploying`** | Klare Abgrenzung zu Upgrade (Versionsänderung) und Running (idle). User kann erkennen dass ein Redeploy läuft. |
| Entity-Modell | A) In-Place Mutation, B) Neue Entity (wie Upgrade) | **A) In-Place Mutation** | Redeploy ändert keine Version. Gleiche Entity, gleiche Stacks. Analog zu Retry. |
| Hook-Design | A) Neuer Endpoint `/api/hooks/redeploy-product`, B) Bestehenden `/api/hooks/redeploy` erweitern | **B) Erweitern** | Konsistent mit Deploy-Hook-Erweiterung (PR #170). `productId` als Signalfeld für Product-Pfad. |
| Stack-Auswahl | A) Immer alle, B) Optionale Auswahl | **B) Optionale Auswahl** | Flexibler: Pipeline kann einzelne Stacks nach Image-Push redeployen, UI bietet "alle" als Default. |
| Variable-Handling | A) Nur gespeicherte, B) Merge mit Overrides | **B) Merge mit Overrides** | Konsistent mit bestehendem Hook-Verhalten. Pipeline kann z.B. Build-Nummer als Override mitgeben. |
