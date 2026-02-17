# Phase: Product Deployment (Deploy, Upgrade, Remove)

## Ziel

Ein ganzes Produkt (alle Stacks) in einem Vorgang deployen, upgraden und entfernen. Aktuell muss der User bei Multi-Stack-Produkten wie ams.project (16 Stacks) jeden Stack manuell einzeln deployen. Die neue Funktion orchestriert N einzelne Deployments in einem zusammenhängenden Flow mit einheitlicher Variable-Konfiguration und Gesamtfortschrittsanzeige.

**Architektur-Entscheidung**: N einzelne Deployment-Aggregates (eins pro Stack), orchestriert durch Application-Layer Commands. Kein neues ProductDeployment-Aggregate in dieser Phase (siehe Appendix A für zukünftige Spezifikation).

## Analyse

### Ist-Zustand

| Aspekt | Aktuell | Ziel |
|--------|---------|------|
| Deploy | Ein Stack pro Vorgang, manuell wiederholt | Alle Stacks eines Produkts am Stück |
| Variables | Pro Stack einzeln konfigurieren | Shared Variables einmal, dann pro Stack |
| Progress | Ein SessionId, ein Stack | Ein SessionId, N Stacks mit Overall-Fortschritt |
| Upgrade | Pro Stack einzeln | Alle Stacks eines Produkts upgraden |
| Remove | Pro Stack einzeln | Alle Deployments eines Produkts entfernen |
| ProductDetail | "Deploy All" Button zeigt `alert()` | Navigiert zu DeployProduct-Seite |

### Bestehende Architektur

| Komponente | Pfad | Relevanz |
|---|---|---|
| `ProductDefinition` | `Domain/StackManagement/Stacks/ProductDefinition.cs` | `Stacks`, `IsMultiStack`, `GroupId` |
| `StackDefinition` | `Domain/StackManagement/Stacks/StackDefinition.cs` | `ProductId`, `ProductName`, `Variables` |
| `Deployment` | `Domain/Deployment/Deployments/Deployment.cs` | `StackId` enthält Product-Info |
| `IDeploymentRepository` | `Domain/Deployment/Deployments/IDeploymentRepository.cs` | Braucht `GetByProductGroupIdAsync()` |
| `DeployStackCommand` | `Application/UseCases/Deployments/DeployStack/` | Wird intern wiederverwendet |
| `UpgradeStackHandler` | `Application/UseCases/Deployments/UpgradeStack/` | Wird intern wiederverwendet |
| `RemoveDeploymentHandler` | `Application/UseCases/Deployments/RemoveDeployment/` | Wird intern wiederverwendet |
| `IProductCache` | `Application/Services/IProductCache.cs` | `GetProduct()`, `GetStack()`, `GetAvailableUpgrades()` |
| `DeploymentHub` | `Api/Hubs/DeploymentHub.cs` | SignalR-Events erweitern |
| `ProductDetail.tsx` | `WebUi/pages/Catalog/ProductDetail.tsx` | "Deploy All" Button (aktuell TODO) |
| `DeployStack.tsx` | `WebUi/pages/Deployments/DeployStack.tsx` | Pattern für Variable-Form |
| `useDeploymentHub.ts` | `WebUi/hooks/useDeploymentHub.ts` | Erweitern für Multi-Stack |
| `deployments.ts` | `WebUi/api/deployments.ts` | Neue API-Funktionen |

### Product-Deployment Gruppierung

Deployments werden über StackId gruppiert. Format: `sourceId:productName:version:stackName`

```csharp
// StackId-Parsing ergibt Product-Zugehörigkeit
var stackId = StackId.TryParse("stacks:ams.project:3.1.0:identity");
// stackId.SourceId = "stacks", stackId.ProductName = "ams.project"
// → GroupId = "stacks:ams.project" (= ProductDefinition.GroupId)
```

Neuer Repository-Query:
```csharp
Task<IReadOnlyList<Deployment>> GetByProductGroupIdAsync(
    EnvironmentId environmentId, string productGroupId);
```

### SignalR Progress-Erweiterung

Bestehende Events werden um optionale Felder erweitert (backward-kompatibel):

```csharp
// Neue optionale Felder in DeploymentProgress
{
    // ... bestehende Felder ...

    // Neu (nullable, nur bei Product-Deployment gesetzt):
    currentStackName?: string,       // "IdentityAccess"
    currentStackIndex?: number,      // 2 (0-basiert)
    totalStacks?: number,            // 16
    completedStacks?: number,        // 1
    overallPercentComplete?: number  // 12 (gewichtet über alle Stacks)
}
```

Berechnung `overallPercentComplete`:
```
overallPercent = ((completedStacks * 100) + currentStackPercent) / totalStacks
```

### Variables-Flow (Wizard)

```
Product: ams.project
├── Shared Variables (Product-Level, Schritt 1)
│   ├── LOG_LEVEL = "info"
│   ├── CULTURE_CODE = "de-DE"
│   └── AMS_DB = "Server=..." (required)
│
├── Stack: Infrastructure (Schritt 2, Accordion)
│   ├── [erbt Shared Variables]
│   └── EVENTSTORE_PORT = "2113"
│
├── Stack: IdentityAccess (Schritt 2, Accordion)
│   ├── [erbt Shared Variables]
│   ├── IDENTITY_DB = "..." (required)
│   └── MAIL_SERVER = "..."
│
└── Stack: BusinessServices (Schritt 2, Accordion)
    ├── [erbt Shared Variables]
    └── REDIS_CONNECTION = "cachedata:6379"
```

UI-Flow:
1. **Schritt 1**: Shared Variables konfigurieren (gelten für alle Stacks)
2. **Schritt 2**: Pro Stack die stack-spezifischen Variables (Accordion, ausklappbar)
3. **Schritt 3**: Deploy starten → Fortschritt pro Stack + gesamt
4. **Schritt 4**: Ergebnis (Erfolg / Partial Failure / Fehler)

### Deployment-Reihenfolge

Stacks werden in **Manifest-Reihenfolge** deployed (Reihenfolge der `stacks:` Keys im YAML). Der Manifest-Autor kennt die Abhängigkeiten und ordnet die Stacks entsprechend:

```yaml
stacks:
  infrastructure:   # 1. zuerst (EventStore, Redis)
    include: infrastructure.yaml
  identity-access:  # 2. braucht Infrastructure
    include: identity-access.yaml
  platform:         # 3. braucht Identity
    include: platform.yaml
  business:         # 4. braucht alles vorherige
    include: business-services.yaml
```

`ProductDefinition.Stacks` behält diese Reihenfolge bei → Handler iteriert `Stacks` in Reihenfolge.

### Stack-Name-Generation

Automatisch generiert aus `stackName` (lowercase, kebab-case):
```
ams-project-infrastructure
ams-project-identity-access
ams-project-platform
ams-project-business
```

Pattern: `{productName}-{stackName}` normalisiert. User kann im Wizard optional überschreiben.

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten — von innen nach außen:

- [ ] **Feature 1: Product Deployment Query** — Repository-Erweiterung
  - Neue Methode `GetByProductGroupIdAsync(EnvironmentId, string productGroupId)`
  - Implementierung: EF Core Query filtert auf `StackId` Prefix-Match
  - Nur aktive Deployments (Status != Removed)
  - Neue Dateien:
    - –
  - Geänderte Dateien:
    - `Domain/Deployment/Deployments/IDeploymentRepository.cs`
    - `Infrastructure.DataAccess/Repositories/DeploymentRepository.cs`
  - Pattern-Vorlage: `GetByStackNameAsync()`
  - Abhängig von: –

- [ ] **Feature 2: DeployProduct Backend** — Command + Handler + Endpoint
  - `DeployProductCommand`:
    - `EnvironmentId`, `ProductId` (z.B. "stacks:ams.project:3.1.0")
    - `Stacks[]`: `{ StackId, StackName, Variables<string,string> }` pro Stack
    - `SessionId`: Für SignalR-Tracking
  - `DeployProductHandler`:
    1. Produkt aus Cache laden, validieren
    2. Environment-Lock prüfen (wie bei Single-Stack)
    3. Für jeden Stack sequentiell:
       - Variables mergen (Shared + Stack-spezifisch + User-Input)
       - `DeployStackCommand` über MediatR dispatchen
       - Progress-Events mit Stack-Index/Total anreichern
    4. Bei Fehler: Option zum Abbrechen oder Weitermachen (konfigurierbar)
  - `DeployProductEndpoint`:
    - `POST /api/environments/{envId}/products/{productId}/deploy`
    - Permission: `Deployments.Create`
  - Neue Dateien:
    - `Application/UseCases/Deployments/DeployProduct/DeployProductCommand.cs`
    - `Application/UseCases/Deployments/DeployProduct/DeployProductHandler.cs`
    - `Api/Endpoints/Deployments/DeployProductEndpoint.cs`
  - Geänderte Dateien:
    - SignalR-Progress DTO (neue optionale Felder)
  - Abhängig von: Feature 1

- [ ] **Feature 3: DeployProduct UI** — Wizard-Style Seite
  - Neue Seite `DeployProduct.tsx` mit State-Machine:
    - `loading` → `configure-shared` → `configure-stacks` → `deploying` → `success` / `error`
  - **configure-shared**: Shared Variables Form (wie DeployStack aber nur Shared)
  - **configure-stacks**: Accordion mit pro-Stack Variables
    - Jeder Stack-Abschnitt zeigt: Name, Description, Service-Count, Variables
    - Ausklappbar, standardmäßig eingeklappt (nur Stacks mit required Variables offen)
    - Stack-Name-Feld (vorausgefüllt mit Auto-Name, editierbar)
  - **deploying**: Fortschrittsanzeige
    - Overall Progress Bar (0-100%)
    - Pro Stack: Status-Badge (pending / deploying / success / error)
    - Aktueller Stack: Detail-Progress (Phase, Service, Init-Container-Logs)
  - **success**: Zusammenfassung aller deployed Stacks mit Links
  - **error**: Fehler-Details, erfolgreich deployed Stacks, Option zum Wiederholen
  - `ProductDetail.tsx`: "Deploy All" Button verlinkt auf `/deploy-product/{productId}`
  - Neue Dateien:
    - `WebUi/src/pages/Deployments/DeployProduct.tsx`
  - Geänderte Dateien:
    - `WebUi/src/pages/Catalog/ProductDetail.tsx` (Button-Link)
    - `WebUi/src/api/deployments.ts` (`deployProduct()`)
    - `WebUi/src/App.tsx` (Route)
  - Pattern-Vorlage: `DeployStack.tsx` (State-Machine, SignalR-Integration, Variable-Form)
  - Abhängig von: Feature 2

- [ ] **Feature 4: UpgradeProduct Backend** — Command + Handler + Endpoint
  - `UpgradeProductCommand`:
    - `EnvironmentId`, `ProductGroupId` (z.B. "stacks:ams.project")
    - `TargetProductId` (z.B. "stacks:ams.project:3.2.0")
    - `StackOverrides[]`: Optional per-Stack Variable-Overrides
    - `SessionId`
  - `UpgradeProductHandler`:
    1. Alle Deployments des Produkts laden via `GetByProductGroupIdAsync`
    2. Target-Produkt aus Cache laden
    3. Deployments → Target-Stacks matchen (über Stack-Name)
    4. Für jeden Stack sequentiell:
       - Variables mergen (Stack Defaults < Existing Deployment < Target Defaults < Overrides)
       - Bestehenden `UpgradeStackHandler`-Flow nutzen
       - Progress-Events mit Stack-Index anreichern
    5. Stacks die im Target neu sind: Als neue Deployments anlegen
    6. Stacks die im Target fehlen: Warnung (nicht automatisch entfernen)
  - `UpgradeProductEndpoint`:
    - `POST /api/environments/{envId}/products/{productGroupId}/upgrade`
    - Permission: `Deployments.Update`
  - Neue Dateien:
    - `Application/UseCases/Deployments/UpgradeProduct/UpgradeProductCommand.cs`
    - `Application/UseCases/Deployments/UpgradeProduct/UpgradeProductHandler.cs`
    - `Api/Endpoints/Deployments/UpgradeProductEndpoint.cs`
  - Abhängig von: Feature 1

- [ ] **Feature 5: UpgradeProduct UI** — Upgrade-Seite für ganzes Produkt
  - Neue Seite `UpgradeProduct.tsx`:
    - Zeigt: Current Version → Target Version
    - Pro Stack: Aktueller Status, Version, was sich ändert
    - Neue Stacks im Target: Badge "New Stack"
    - Fehlende Stacks im Target: Badge "Will not be affected"
    - Shared + Per-Stack Variable-Konfiguration (vorausgefüllt)
    - Fortschrittsanzeige pro Stack + gesamt
  - Zugang: ProductDetail "Upgrade" Button (wenn Upgrade verfügbar)
  - Neue Dateien:
    - `WebUi/src/pages/Deployments/UpgradeProduct.tsx`
  - Geänderte Dateien:
    - `WebUi/src/pages/Catalog/ProductDetail.tsx` (Upgrade-Button)
    - `WebUi/src/api/deployments.ts` (`upgradeProduct()`, `checkProductUpgrade()`)
    - `WebUi/src/App.tsx` (Route)
  - Abhängig von: Feature 4

- [ ] **Feature 6: RemoveProduct Backend** — Command + Handler + Endpoint
  - `RemoveProductCommand`:
    - `EnvironmentId`, `ProductGroupId`, `SessionId`
  - `RemoveProductHandler`:
    1. Alle aktiven Deployments des Produkts laden
    2. In umgekehrter Manifest-Reihenfolge entfernen (Business → Identity → Infrastructure)
    3. Für jedes Deployment: `RemoveDeploymentHandler`-Flow nutzen
    4. Progress-Events mit Stack-Index anreichern
  - `RemoveProductEndpoint`:
    - `DELETE /api/environments/{envId}/products/{productGroupId}`
    - Permission: `Deployments.Delete`
  - Neue Dateien:
    - `Application/UseCases/Deployments/RemoveProduct/RemoveProductCommand.cs`
    - `Application/UseCases/Deployments/RemoveProduct/RemoveProductHandler.cs`
    - `Api/Endpoints/Deployments/RemoveProductEndpoint.cs`
  - Abhängig von: Feature 1

- [ ] **Feature 7: RemoveProduct UI** — Bestätigungsseite mit Fortschritt
  - Neue Seite `RemoveProduct.tsx`:
    - Listet alle Stacks/Deployments die entfernt werden
    - Service-Count, Container-Count pro Stack
    - Warnung: "This will stop and remove all N stacks with M containers"
    - Fortschrittsanzeige pro Stack + gesamt
    - Ergebnis: Zusammenfassung
  - Zugang: ProductDetail "Remove" Button (wenn deployed)
  - Neue Dateien:
    - `WebUi/src/pages/Deployments/RemoveProduct.tsx`
  - Geänderte Dateien:
    - `WebUi/src/pages/Catalog/ProductDetail.tsx` (Remove-Button)
    - `WebUi/src/api/deployments.ts` (`removeProduct()`)
    - `WebUi/src/App.tsx` (Route)
  - Abhängig von: Feature 6

- [ ] **Feature 8: Product Deployment Status in ProductDetail** — UI-Erweiterung
  - `ProductDetail.tsx` erweitern:
    - Zeigt Deploy-Status pro Stack (deployed/not deployed/failed)
    - Versionsnummer des deployed Stacks
    - Actions: Deploy All / Upgrade All / Remove All (je nach Zustand)
  - Neuer API-Endpoint: `GET /api/environments/{envId}/products/{productGroupId}/status`
    - Response: `{ deployed: boolean, stacks: [{ stackName, deploymentId, status, version }] }`
  - Neue Dateien:
    - `Application/UseCases/Deployments/GetProductStatus/GetProductStatusQuery.cs`
    - `Application/UseCases/Deployments/GetProductStatus/GetProductStatusHandler.cs`
    - `Api/Endpoints/Deployments/GetProductStatusEndpoint.cs`
  - Geänderte Dateien:
    - `WebUi/src/pages/Catalog/ProductDetail.tsx`
    - `WebUi/src/api/deployments.ts` (`getProductStatus()`)
  - Abhängig von: Feature 1

- [ ] **Feature 9: Tests** — Unit + Integration
  - Unit Tests:
    - `DeployProductHandlerTests`: Sequentielle Orchestrierung, Partial Failure, Variable-Merging, Reihenfolge
    - `UpgradeProductHandlerTests`: Deployment-Matching, neue/fehlende Stacks, Variable-Merge-Priority
    - `RemoveProductHandlerTests`: Umgekehrte Reihenfolge, Partial Failure
    - `GetProductStatusHandlerTests`: Deployed/Not-Deployed/Mixed Status
    - `DeploymentRepositoryTests`: `GetByProductGroupIdAsync` mit verschiedenen StackId-Formaten
  - Integration Tests:
    - Endpoint-Tests für alle neuen Endpoints
  - Edge Cases:
    - Product mit nur 1 Stack (Grenzfall)
    - Partial Failure: Stack 3/16 schlägt fehl
    - Upgrade mit neuen Stacks im Target
    - Remove eines teilweise deployed Produkts
    - Concurrent Product-Deploy auf gleichem Environment
  - Abhängig von: Feature 1-8

- [ ] **Dokumentation & Website** — Wiki, Public Website (DE/EN), Roadmap
  - Abhängig von: Feature 9

- [ ] **Phase abschließen** — Alle Tests grün, PR gegen main
  - Abhängig von: alle

## Test-Strategie

- **Unit Tests**: Handler-Logik (Orchestrierung, Error-Handling, Variable-Merging, Reihenfolge)
- **Integration Tests**: Endpoint-Tests mit gemocktem IDeploymentService
- **Manuell**: ams.project (16 Stacks) komplett deployen, upgraden und entfernen über UI

## Offene Punkte

- [ ] Partial Failure Handling: Abbrechen oder weitermachen bei Stack-Fehler? (Vermutlich: Default weitermachen, Option zum Abbrechen im UI)
- [ ] Rollback bei Product-Deploy: Alle erfolgreichen Stacks zurückrollen wenn ein Stack fehlschlägt? (Vermutlich: Nein, manuell per Remove)
- [ ] Single-Stack-Products: Sollen sie auch den Product-Deploy-Flow nutzen oder weiterhin direkt DeployStack? (Vermutlich: Weiterhin DeployStack, kein Overhead)
- [ ] Hooks (CI/CD): Soll es einen `POST /api/hooks/deploy-product` Webhook geben? (Vermutlich: Ja, in separater Phase)

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Deployment-Modell | A) ProductDeployment Aggregate, B) N einzelne Deployments | **B** | Einfacher, bestehende Upgrade/Rollback/Remove pro Stack bleibt erhalten. Aggregate als Future Feature (Appendix A). |
| Variables UX | A) Alles auf einer Seite, B) Wizard (Shared → Per-Stack) | **B** | Übersichtlicher bei 16+ Stacks mit vielen Variables |
| SignalR Events | A) Neue Event-Types, B) Bestehende erweitern | **B** | Backward-kompatibel, neue Felder nullable |
| Deployment-Reihenfolge | A) Alphabetisch, B) Manifest-Reihenfolge, C) User-defined | **B** | Manifest-Autor kennt die Abhängigkeiten, YAML key order bleibt erhalten |
| Remove-Reihenfolge | A) Gleich wie Deploy, B) Umgekehrt | **B** | Abhängige Stacks zuerst entfernen (Business vor Infrastructure) |
| Stack-Name | A) Auto-generiert, B) User-Input pro Stack | **A + optional B** | Auto-Name (`product-stack`), editierbar im Wizard |
| Partial Failure | – | – | Offen (siehe Offene Punkte) |

---

## Appendix A: Future ProductDeployment Aggregate — Spezifikation

### Motivation

Die aktuelle Lösung (N einzelne Deployments, orchestriert) hat Grenzen:
- Kein atomarer Rollback des gesamten Produkts
- Keine einheitliche Version-Tracking auf Product-Ebene
- Kein Status "Product partially deployed"
- Deployment-Gruppierung nur über StackId-Prefix (fragil)

### Domain-Modell

```csharp
// Neues Aggregate Root
public class ProductDeployment : AggregateRoot<ProductDeploymentId>
{
    public EnvironmentId EnvironmentId { get; }
    public string ProductGroupId { get; }         // "stacks:ams.project"
    public string ProductVersion { get; }         // "3.1.0"
    public ProductDeploymentStatus Status { get; }

    // Stacks als Child-Entities
    public IReadOnlyList<StackDeployment> Stacks { get; }

    // Shared Variable-Konfiguration (Product-Level)
    public IReadOnlyDictionary<string, string> SharedVariables { get; }

    // Factory
    public static ProductDeployment Create(
        EnvironmentId envId, string productGroupId, string productVersion,
        IReadOnlyList<StackDeploymentConfig> stackConfigs,
        IReadOnlyDictionary<string, string> sharedVariables);

    // Commands
    public void StartDeployment();
    public void CompleteStack(string stackName);
    public void FailStack(string stackName, string error);
    public void CompleteDeployment();
    public void StartUpgrade(string targetVersion);
    public void StartRemoval();
}

// Child Entity
public class StackDeployment
{
    public string StackName { get; }
    public string StackId { get; }
    public DeploymentId? DeploymentId { get; }    // Link zum bestehenden Deployment
    public StackDeploymentStatus Status { get; }
    public int Order { get; }                      // Deployment-Reihenfolge
    public IReadOnlyDictionary<string, string> Variables { get; }
}

// Status
public enum ProductDeploymentStatus
{
    Pending,
    Deploying,
    Running,          // Alle Stacks erfolgreich
    PartiallyRunning, // Einige Stacks failed
    Upgrading,
    Removing,
    Removed,
    Failed
}
```

### Vorteile

- **Atomare Operationen**: Rollback aller Stacks bei Fehler
- **Version-Tracking**: Product-Version zentral gespeichert
- **Status-Aggregation**: "Product X is partially deployed" als Domain-Status
- **Upgrade-Koordination**: Alle Stacks eines Produkts auf gleiche Version bringen
- **Audit-Trail**: Wann wurde welches Product deployed/upgraded/removed

### Migration

1. Bestehende einzelne Deployments behalten (backward-kompatibel)
2. Neue Product-Deployments erzeugen automatisch auch einzelne Deployments (für Container-Zuordnung)
3. `ProductDeployment` ist ein Orchestrations-Overlay, kein Ersatz

### Betroffene Bounded Contexts

- **Domain**: `ProductDeployment` Aggregate + `StackDeployment` Entity + Events
- **Application**: Neue Commands/Queries, bestehende Handlers erweitern
- **Infrastructure.DataAccess**: Neue Tabelle `ProductDeployments` + `StackDeployments`
- **API**: Endpoint-Erweiterungen
- **WebUI**: Deployment-Ansicht gruppiert nach Products

### Abhängigkeiten

- Setzt Phase "Product Deployment (orchestriert)" voraus
- Kann inkrementell eingeführt werden: Erst Domain + Persistenz, dann UI
