# Phase: Product Deployment

## Ziel

Ein ganzes Produkt (alle Stacks) in einem Vorgang deployen, upgraden und entfernen. Aktuell muss der User bei Multi-Stack-Produkten wie ams.project (16 Stacks) jeden Stack manuell einzeln deployen. Die neue Funktion bietet einen zusammenhängenden Flow mit einheitlicher Variable-Konfiguration, Gesamtfortschrittsanzeige und einem echten Domain-Modell (`ProductDeployment` Aggregate).

**Architektur-Entscheidung**: Eigenes `ProductDeployment` Aggregate Root im Deployment Bounded Context. Jeder Stack erzeugt intern weiterhin ein einzelnes `Deployment` Aggregate (für Container-Zuordnung, Health Monitoring, etc.). Das `ProductDeployment` ist das Orchestrations-Overlay mit Product-Level State Machine, Audit-Trail und koordiniertem Lifecycle.

## Analyse

### Ist-Zustand

| Aspekt | Aktuell | Ziel |
|--------|---------|------|
| Deploy | Ein Stack pro Vorgang, manuell wiederholt | Alle Stacks eines Produkts am Stück |
| Variables | Pro Stack einzeln konfigurieren | Shared Variables einmal, dann pro Stack |
| Progress | Ein SessionId, ein Stack | Ein SessionId, N Stacks mit Overall-Fortschritt |
| Upgrade | Pro Stack einzeln | Alle Stacks eines Produkts upgraden |
| Remove | Pro Stack einzeln | Alle Deployments eines Produkts entfernen |
| Status | Kein aggregierter Product-Status | `ProductDeploymentStatus` mit State Machine |
| Version-Tracking | Pro Stack in `Deployment.StackVersion` | Zentral in `ProductDeployment.ProductVersion` |
| ProductDetail | "Deploy All" Button zeigt `alert()` | Navigiert zu DeployProduct-Seite |

### Bestehende Architektur

| Komponente | Pfad | Relevanz |
|---|---|---|
| `ProductDefinition` | `Domain/StackManagement/Stacks/ProductDefinition.cs` | `Stacks`, `IsMultiStack`, `GroupId` |
| `StackDefinition` | `Domain/StackManagement/Stacks/StackDefinition.cs` | `ProductId`, `ProductName`, `Variables` |
| `Deployment` | `Domain/Deployment/Deployments/Deployment.cs` | Bestehendes Aggregate, wird intern weiter genutzt |
| `DeployStackCommand` | `Application/UseCases/Deployments/DeployStack/` | Wird intern wiederverwendet |
| `UpgradeStackHandler` | `Application/UseCases/Deployments/UpgradeStack/` | Wird intern wiederverwendet |
| `RemoveDeploymentHandler` | `Application/UseCases/Deployments/RemoveDeployment/` | Wird intern wiederverwendet |
| `IProductCache` | `Application/Services/IProductCache.cs` | `GetProduct()`, `GetStack()`, `GetAvailableUpgrades()` |
| `AggregateRoot<T>` | `Domain/SharedKernel/AggregateRoot.cs` | Base class mit Version + DomainEvents |
| `DeploymentConfiguration` | `Infrastructure.DataAccess/Configurations/` | EF Core Pattern-Vorlage |
| `DeploymentHub` | `Api/Hubs/DeploymentHub.cs` | SignalR-Events erweitern |
| `ProductDetail.tsx` | `WebUi/pages/Catalog/ProductDetail.tsx` | "Deploy All" Button (aktuell TODO) |
| `DeployStack.tsx` | `WebUi/pages/Deployments/DeployStack.tsx` | Pattern für Variable-Form + State-Machine |

### Zwei-Ebenen-Architektur

```
┌─────────────────────────────────────────────────┐
│ ProductDeployment (Aggregate Root)               │
│ Status: Running | ProductVersion: 3.1.0          │
│                                                  │
│ ┌─ ProductStackDeployment: infrastructure ──┐    │
│ │ Status: Running | Order: 0                │────│──→ Deployment (ams-infra)
│ │ DeploymentId: abc-123                     │    │    Container-Level Operations
│ └───────────────────────────────────────────┘    │
│                                                  │
│ ┌─ ProductStackDeployment: identity-access ─┐    │
│ │ Status: Running | Order: 1                │────│──→ Deployment (ams-identity)
│ │ DeploymentId: def-456                     │    │    Container-Level Operations
│ └───────────────────────────────────────────┘    │
│                                                  │
│ ┌─ ProductStackDeployment: business ────────┐    │
│ │ Status: Running | Order: 2                │────│──→ Deployment (ams-business)
│ │ DeploymentId: ghi-789                     │    │    Container-Level Operations
│ └───────────────────────────────────────────┘    │
└─────────────────────────────────────────────────┘
```

**ProductDeployment** = Source of Truth für Product-Level Status (Deploy, Upgrade, Remove, Version).
**Deployment** = Source of Truth für Container-Level (Health, Start/Stop, Logs).
**Eventual Consistency**: Background-Service gleicht Product↔Deployment Status periodisch ab.

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

### Deployment-Reihenfolge

Stacks werden in **Manifest-Reihenfolge** deployed. Der Manifest-Autor kennt die Abhängigkeiten.
Remove erfolgt in **umgekehrter Reihenfolge** (Business → Identity → Infrastructure).

### Stack-Name-Generation

Automatisch: `{productName}-{stackName}` (lowercase, kebab-case). Im Wizard optional editierbar.

## Domain-Modell

### ProductDeployment (Aggregate Root)

```csharp
namespace ReadyStackGo.Domain.Deployment.ProductDeployments;

public class ProductDeployment : AggregateRoot<ProductDeploymentId>
{
    // ── Identity & References ──────────────────────────────────────────
    public EnvironmentId EnvironmentId { get; private set; }
    public string ProductGroupId { get; private set; }        // "stacks:ams.project"
    public string ProductId { get; private set; }             // "stacks:ams.project:3.1.0"
    public string ProductName { get; private set; }           // "ams.project"
    public string ProductDisplayName { get; private set; }    // "ams.project Enterprise"
    public string ProductVersion { get; private set; }        // "3.1.0"
    public UserId DeployedBy { get; private set; }

    // ── Status & Lifecycle ─────────────────────────────────────────────
    public ProductDeploymentStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? ErrorMessage { get; private set; }

    public bool IsTerminal => Status == ProductDeploymentStatus.Removed;
    public bool IsInProgress => Status is ProductDeploymentStatus.Deploying
                                       or ProductDeploymentStatus.Upgrading
                                       or ProductDeploymentStatus.Removing;
    public bool IsOperational => Status is ProductDeploymentStatus.Running
                                        or ProductDeploymentStatus.PartiallyRunning;

    // ── Stacks (Child Entities) ────────────────────────────────────────
    private readonly List<ProductStackDeployment> _stacks = new();
    public IReadOnlyList<ProductStackDeployment> Stacks => _stacks.AsReadOnly();

    public int TotalStacks => _stacks.Count;
    public int CompletedStacks => _stacks.Count(s => s.Status == StackDeploymentStatus.Running);
    public int FailedStacks => _stacks.Count(s => s.Status == StackDeploymentStatus.Failed);

    // ── Shared Variables (Product-Level Snapshot) ──────────────────────
    public IReadOnlyDictionary<string, string> SharedVariables { get; private set; }

    // ── Upgrade Tracking ───────────────────────────────────────────────
    public string? PreviousVersion { get; private set; }
    public DateTime? LastUpgradedAt { get; private set; }
    public int UpgradeCount { get; private set; }

    // ── Phase History ──────────────────────────────────────────────────
    public IReadOnlyCollection<ProductDeploymentPhaseRecord> PhaseHistory { get; }

    // ═══════════════════════════════════════════════════════════════════
    // Factory Methods
    // ═══════════════════════════════════════════════════════════════════

    public static ProductDeployment InitiateDeployment(
        ProductDeploymentId id, EnvironmentId environmentId,
        string productGroupId, string productId,
        string productName, string productDisplayName, string productVersion,
        UserId deployedBy,
        IReadOnlyList<StackDeploymentConfig> stackConfigs,
        IReadOnlyDictionary<string, string> sharedVariables);
    // → Status = Deploying, Stacks = Pending, raises ProductDeploymentInitiated

    public static ProductDeployment InitiateUpgrade(
        ProductDeploymentId id, EnvironmentId environmentId,
        string productGroupId, string productId,
        string productName, string productDisplayName, string targetVersion,
        UserId deployedBy,
        ProductDeployment existingDeployment,
        IReadOnlyList<StackDeploymentConfig> targetStackConfigs,
        IReadOnlyDictionary<string, string> sharedVariables);
    // → Status = Upgrading, PreviousVersion from existing, raises ProductUpgradeInitiated

    // ═══════════════════════════════════════════════════════════════════
    // Stack Lifecycle (called by orchestrator)
    // ═══════════════════════════════════════════════════════════════════

    public void StartStack(string stackName, DeploymentId deploymentId);
    // → Stack.Status = Deploying, raises ProductStackDeploymentStarted

    public void CompleteStack(string stackName);
    // → Stack.Status = Running, if ALL Running → CompleteDeployment()

    public void FailStack(string stackName, string errorMessage);
    // → Stack.Status = Failed, raises ProductStackDeploymentFailed

    // ═══════════════════════════════════════════════════════════════════
    // Product Lifecycle
    // ═══════════════════════════════════════════════════════════════════

    public void MarkAsPartiallyRunning(string reason);
    // → Status = PartiallyRunning (some Running, some Failed/Pending)

    public void MarkAsFailed(string errorMessage);
    // → Status = Failed (critical error)

    public void StartRemoval();
    // → Status = Removing, all stacks reset to Pending

    public void MarkStackRemoved(string stackName);
    // → Stack.Status = Removed, if ALL Removed → Status = Removed

    // ═══════════════════════════════════════════════════════════════════
    // Query Methods
    // ═══════════════════════════════════════════════════════════════════

    public bool CanUpgrade => IsOperational;
    public bool CanRemove => IsOperational || Status == ProductDeploymentStatus.Failed;
    public bool CanRollback => Status == ProductDeploymentStatus.Failed
                               && PreviousVersion is not null;

    public IReadOnlyList<ProductStackDeployment> GetStacksInDeployOrder();
    public IReadOnlyList<ProductStackDeployment> GetStacksInRemoveOrder();
}
```

### ProductStackDeployment (Child Entity)

```csharp
public class ProductStackDeployment
{
    public int Id { get; private set; }                       // Auto-PK
    public string StackName { get; private set; }             // "infrastructure"
    public string StackDisplayName { get; private set; }      // "Infrastructure"
    public string StackId { get; private set; }               // Full catalog ref
    public DeploymentId? DeploymentId { get; private set; }   // FK to Deployment
    public string? DeploymentStackName { get; private set; }  // "ams-project-infrastructure"
    public StackDeploymentStatus Status { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int Order { get; private set; }                    // Manifest order
    public int ServiceCount { get; private set; }
    public IReadOnlyDictionary<string, string> Variables { get; private set; }
    public bool IsNewInUpgrade { get; private set; }
}
```

### State Machine

```
ProductDeploymentStatus:

    Deploying  ──→ Running           (all stacks succeed)
               ──→ PartiallyRunning  (some succeed, orchestrator stops)
               ──→ Failed            (critical error or all fail)

    Running    ──→ Upgrading         (upgrade initiated)
               ──→ Removing          (removal initiated)

    PartiallyRunning ──→ Upgrading   (retry/upgrade remaining)
                     ──→ Removing    (remove all)

    Upgrading  ──→ Running           (all stacks upgraded)
               ──→ PartiallyRunning  (some fail)
               ──→ Failed            (critical error)

    Failed     ──→ Upgrading         (rollback = upgrade to previous)
               ──→ Removing          (remove all)

    Removing   ──→ Removed           (terminal)
```

### Domain Events

```csharp
// Product-Level
record ProductDeploymentInitiated(ProductDeploymentId, EnvironmentId, string ProductName, string ProductVersion, int TotalStacks)
record ProductDeploymentCompleted(ProductDeploymentId, string ProductName, string ProductVersion, int TotalStacks, TimeSpan Duration)
record ProductDeploymentPartiallyCompleted(ProductDeploymentId, string ProductName, int RunningStacks, int FailedStacks, string Reason)
record ProductDeploymentFailed(ProductDeploymentId, string ProductName, string ErrorMessage, int CompletedStacks, int FailedStacks)
record ProductUpgradeInitiated(ProductDeploymentId, string ProductName, string PreviousVersion, string TargetVersion, int TotalStacks)
record ProductRemovalInitiated(ProductDeploymentId, string ProductName, int TotalStacks)
record ProductDeploymentRemoved(ProductDeploymentId, string ProductName)

// Stack-Level
record ProductStackDeploymentStarted(ProductDeploymentId, string StackName, DeploymentId, int StackIndex, int TotalStacks)
record ProductStackDeploymentCompleted(ProductDeploymentId, string StackName, DeploymentId, int CompletedStacks, int TotalStacks)
record ProductStackDeploymentFailed(ProductDeploymentId, string StackName, string ErrorMessage, int CompletedStacks, int TotalStacks)
```

## Persistenz

### Tabellen

```sql
-- ProductDeployments
CREATE TABLE ProductDeployments (
    Id TEXT PRIMARY KEY, EnvironmentId TEXT NOT NULL, ProductGroupId TEXT NOT NULL,
    ProductId TEXT NOT NULL, ProductName TEXT NOT NULL, ProductDisplayName TEXT NOT NULL,
    ProductVersion TEXT NOT NULL, DeployedBy TEXT NOT NULL,
    Status INTEGER NOT NULL, CreatedAt TEXT NOT NULL, CompletedAt TEXT,
    ErrorMessage TEXT, PreviousVersion TEXT, LastUpgradedAt TEXT,
    UpgradeCount INTEGER NOT NULL DEFAULT 0,
    SharedVariablesJson TEXT NOT NULL, PhaseHistoryJson TEXT NOT NULL,
    Version INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IX_ProductDeployments_EnvironmentId ON ProductDeployments(EnvironmentId);
CREATE INDEX IX_ProductDeployments_ProductGroupId ON ProductDeployments(ProductGroupId);
CREATE INDEX IX_ProductDeployments_Status ON ProductDeployments(Status);

-- ProductStackDeployments (owned by ProductDeployment)
CREATE TABLE ProductStackDeployments (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ProductDeploymentId TEXT NOT NULL REFERENCES ProductDeployments(Id) ON DELETE CASCADE,
    StackName TEXT NOT NULL, StackDisplayName TEXT NOT NULL, StackId TEXT NOT NULL,
    DeploymentId TEXT, DeploymentStackName TEXT,
    Status INTEGER NOT NULL, StartedAt TEXT, CompletedAt TEXT, ErrorMessage TEXT,
    "Order" INTEGER NOT NULL, ServiceCount INTEGER NOT NULL,
    VariablesJson TEXT NOT NULL, IsNewInUpgrade INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IX_ProductStackDeployments_ProductDeploymentId ON ProductStackDeployments(ProductDeploymentId);
CREATE INDEX IX_ProductStackDeployments_DeploymentId ON ProductStackDeployments(DeploymentId);
```

### Repository

```csharp
public interface IProductDeploymentRepository
{
    ProductDeploymentId NextIdentity();
    void Add(ProductDeployment productDeployment);
    void Update(ProductDeployment productDeployment);
    ProductDeployment? Get(ProductDeploymentId id);
    ProductDeployment? GetActiveByProductGroupId(EnvironmentId environmentId, string productGroupId);
    IEnumerable<ProductDeployment> GetByEnvironment(EnvironmentId environmentId);
    IEnumerable<ProductDeployment> GetAllActive();
    void SaveChanges();
}
```

## API Endpoints

| Method | Route | Permission | Description |
|--------|-------|-----------|-------------|
| POST | `/api/environments/{envId}/product-deployments` | Deployments.Create | Deploy entire product |
| GET | `/api/environments/{envId}/product-deployments` | Deployments.Read | List product deployments |
| GET | `/api/environments/{envId}/product-deployments/{id}` | Deployments.Read | Get detail |
| GET | `/api/environments/{envId}/product-deployments/by-product/{groupId}` | Deployments.Read | Get active by product |
| POST | `/api/environments/{envId}/product-deployments/{id}/upgrade` | Deployments.Update | Upgrade product |
| DELETE | `/api/environments/{envId}/product-deployments/{id}` | Deployments.Delete | Remove product |

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten — von innen nach außen:

- [ ] **Feature 1: Domain-Modell** — ProductDeployment Aggregate + Child Entities + Events
  - Neue Dateien:
    - `Domain/Deployment/ProductDeployments/ProductDeployment.cs`
    - `Domain/Deployment/ProductDeployments/ProductStackDeployment.cs`
    - `Domain/Deployment/ProductDeployments/ProductDeploymentStatus.cs`
    - `Domain/Deployment/ProductDeployments/StackDeploymentStatus.cs`
    - `Domain/Deployment/ProductDeployments/ProductDeploymentId.cs`
    - `Domain/Deployment/ProductDeployments/ProductDeploymentEvents.cs`
    - `Domain/Deployment/ProductDeployments/IProductDeploymentRepository.cs`
  - State Machine: ValidTransitions Dictionary, alle Transitions + Assertions
  - Factory Methods: `InitiateDeployment()`, `InitiateUpgrade()`
  - Tests: Alle gültigen/ungültigen Transitions, Factory-Validierung, Domain Events, Computed Properties
  - Abhängig von: –

- [ ] **Feature 2: Persistenz** — EF Core Configuration + Repository
  - Neue Dateien:
    - `Infrastructure.DataAccess/Configurations/ProductDeploymentConfiguration.cs`
    - `Infrastructure.DataAccess/Repositories/ProductDeploymentRepository.cs`
  - Geänderte Dateien:
    - `Infrastructure.DataAccess/ReadyStackGoDbContext.cs` (DbSet)
  - Value Object Conversions, JSON Columns, Owned Entities, Indexes
  - Tests: CRUD, Owned Entities laden, JSON-Serialisierung, Concurrency Token
  - Abhängig von: Feature 1

- [ ] **Feature 3: DeployProduct Backend** — Command + Handler + Endpoint
  - `DeployProductCommand`: EnvironmentId, ProductId, Stacks[] (StackId, StackName, Variables), SessionId
  - `DeployProductHandler`:
    1. ProductDeployment.InitiateDeployment() → Aggregate erzeugen + persistieren
    2. Für jeden Stack in Manifest-Reihenfolge:
       a. `productDeployment.StartStack(stackName, deploymentId)` → persistieren
       b. `DeployStackCommand` via MediatR dispatchen
       c. Success: `productDeployment.CompleteStack(stackName)` → persistieren
       d. Failure: `productDeployment.FailStack(stackName, error)` → persistieren
    3. Nach allen: MarkAsPartiallyRunning oder automatisch Running (via CompleteStack)
    4. Progress via SignalR mit Stack-Index/Total
  - `DeployProductEndpoint`: `POST .../product-deployments`, Permission `Deployments.Create`
  - Neue Dateien:
    - `Application/UseCases/Deployments/DeployProduct/DeployProductCommand.cs`
    - `Application/UseCases/Deployments/DeployProduct/DeployProductHandler.cs`
    - `Api/Endpoints/Deployments/DeployProductEndpoint.cs`
  - Tests: Orchestrierung, Partial Failure, Variable-Merging, Reihenfolge
  - Abhängig von: Feature 1, 2

- [ ] **Feature 4: DeployProduct UI** — Wizard-Style Seite
  - Neue Seite `DeployProduct.tsx` mit State-Machine:
    `loading` → `configure-shared` → `configure-stacks` → `deploying` → `success` / `error`
  - **configure-shared**: Shared Variables Form
  - **configure-stacks**: Accordion mit pro-Stack Variables
    - Ausklappbar, Stacks mit required Variables standardmäßig offen
    - Stack-Name-Feld (vorausgefüllt, editierbar)
  - **deploying**: Overall Progress Bar + pro-Stack Status-Badges
    - Aktueller Stack: Detail-Progress (Phase, Service, Init-Logs)
  - **success**: Zusammenfassung aller deployed Stacks
  - **error**: Fehler-Details, erfolgreiche Stacks, Option zum Wiederholen
  - `ProductDetail.tsx`: "Deploy All" Button → `/deploy-product/{productId}`
  - Neue Dateien:
    - `WebUi/src/pages/Deployments/DeployProduct.tsx`
  - Geänderte Dateien:
    - `WebUi/src/pages/Catalog/ProductDetail.tsx` (Button-Link)
    - `WebUi/src/api/deployments.ts` (`deployProduct()`)
    - `WebUi/src/App.tsx` (Route)
  - Pattern-Vorlage: `DeployStack.tsx`
  - Abhängig von: Feature 3

- [ ] **Feature 5: UpgradeProduct Backend** — Command + Handler + Endpoint
  - `UpgradeProductCommand`: EnvironmentId, ProductDeploymentId, TargetProductId, StackOverrides[], SessionId
  - `UpgradeProductHandler`:
    1. Bestehendes ProductDeployment laden
    2. `ProductDeployment.InitiateUpgrade()` → neues Aggregate
    3. Deployments → Target-Stacks matchen (über StackName)
    4. Für jeden Stack: Variables mergen (Defaults < Existing < Target < Overrides), upgrade
    5. Neue Stacks im Target: als neue Deployments anlegen
    6. Fehlende Stacks im Target: Warnung (nicht automatisch entfernen)
  - `UpgradeProductEndpoint`: `POST .../product-deployments/{id}/upgrade`, Permission `Deployments.Update`
  - Neue Dateien:
    - `Application/UseCases/Deployments/UpgradeProduct/UpgradeProductCommand.cs`
    - `Application/UseCases/Deployments/UpgradeProduct/UpgradeProductHandler.cs`
    - `Api/Endpoints/Deployments/UpgradeProductEndpoint.cs`
  - Tests: Deployment-Matching, neue/fehlende Stacks, Variable-Merge-Priority
  - Abhängig von: Feature 1, 2

- [ ] **Feature 6: UpgradeProduct UI** — Upgrade-Seite für ganzes Produkt
  - Neue Seite `UpgradeProduct.tsx`:
    - Current Version → Target Version Anzeige
    - Pro Stack: Status, Version, "New Stack" / "Unchanged" Badge
    - Shared + Per-Stack Variable-Konfiguration (vorausgefüllt)
    - Fortschrittsanzeige pro Stack + gesamt
  - Zugang: ProductDetail "Upgrade" Button (wenn Upgrade verfügbar)
  - Neue Dateien:
    - `WebUi/src/pages/Deployments/UpgradeProduct.tsx`
  - Geänderte Dateien:
    - `WebUi/src/pages/Catalog/ProductDetail.tsx` (Upgrade-Button)
    - `WebUi/src/api/deployments.ts` (`upgradeProduct()`, `checkProductUpgrade()`)
    - `WebUi/src/App.tsx` (Route)
  - Abhängig von: Feature 5

- [ ] **Feature 7: RemoveProduct Backend** — Command + Handler + Endpoint
  - `RemoveProductCommand`: EnvironmentId, ProductDeploymentId, SessionId
  - `RemoveProductHandler`:
    1. ProductDeployment laden
    2. `productDeployment.StartRemoval()` → Status = Removing
    3. Für jeden Stack in **umgekehrter** Reihenfolge:
       - Einzelnes Deployment via `RemoveDeploymentHandler` entfernen
       - `productDeployment.MarkStackRemoved(stackName)`
    4. Alle entfernt → Status = Removed (automatisch)
  - `RemoveProductEndpoint`: `DELETE .../product-deployments/{id}`, Permission `Deployments.Delete`
  - Neue Dateien:
    - `Application/UseCases/Deployments/RemoveProduct/RemoveProductCommand.cs`
    - `Application/UseCases/Deployments/RemoveProduct/RemoveProductHandler.cs`
    - `Api/Endpoints/Deployments/RemoveProductEndpoint.cs`
  - Abhängig von: Feature 1, 2

- [ ] **Feature 8: RemoveProduct UI** — Bestätigungsseite mit Fortschritt
  - Neue Seite `RemoveProduct.tsx`:
    - Listet alle Stacks/Deployments die entfernt werden
    - Service-Count, Container-Count pro Stack
    - Warnung: "This will stop and remove all N stacks with M containers"
    - Fortschrittsanzeige pro Stack + gesamt
  - Zugang: ProductDetail "Remove" Button (wenn deployed)
  - Neue Dateien:
    - `WebUi/src/pages/Deployments/RemoveProduct.tsx`
  - Geänderte Dateien:
    - `WebUi/src/pages/Catalog/ProductDetail.tsx` (Remove-Button)
    - `WebUi/src/api/deployments.ts` (`removeProduct()`)
    - `WebUi/src/App.tsx` (Route)
  - Abhängig von: Feature 7

- [ ] **Feature 9: ProductDetail Status** — UI-Erweiterung
  - `ProductDetail.tsx` erweitern:
    - Zeigt Deploy-Status pro Stack (deployed/not deployed/failed/partially running)
    - Versionsnummer des deployed Produkts
    - Actions: Deploy All / Upgrade All / Remove All (je nach Zustand)
  - Neuer Query: `GetProductDeploymentStatusQuery` → liest aus ProductDeployment Aggregate
  - Neue Dateien:
    - `Application/UseCases/Deployments/GetProductStatus/GetProductStatusQuery.cs`
    - `Application/UseCases/Deployments/GetProductStatus/GetProductStatusHandler.cs`
    - `Api/Endpoints/Deployments/GetProductStatusEndpoint.cs`
  - Geänderte Dateien:
    - `WebUi/src/pages/Catalog/ProductDetail.tsx`
    - `WebUi/src/api/deployments.ts` (`getProductStatus()`)
  - Abhängig von: Feature 2

- [ ] **Feature 10: Health-Sync Service** — Eventual Consistency
  - Neuer Background-Service: `ProductDeploymentHealthSyncService`
  - Periodisch (60s): ProductStackDeployment.Status ↔ Deployment.Status abgleichen
  - Bei Inkonsistenz (z.B. Container crashed): ProductStackDeployment.Status aktualisieren
  - Wenn alle Stacks Running aber ProductDeployment.Status != Running → korrigieren
  - Neue Dateien:
    - `Application/Services/ProductDeploymentHealthSyncService.cs` (oder Infrastructure)
  - Abhängig von: Feature 2

- [ ] **Feature 11: Tests** — Unit + Integration
  - **Domain Unit Tests** (Hauptfokus):
    - State Machine: Alle gültigen Transitions, alle ungültigen Transitions
    - Factory Methods: Validierung, Default-Werte, Events
    - CompleteStack → automatisches CompleteDeployment wenn letzter Stack
    - FailStack → PartiallyRunning vs Failed
    - GetStacksInRemoveOrder: Umgekehrte Reihenfolge
    - CanUpgrade/CanRemove/CanRollback für jeden Status
  - **Repository Integration Tests**:
    - CRUD mit Owned Entities
    - JSON-Serialisierung (SharedVariables, PhaseHistory, Variables)
    - Concurrency Token
    - Queries: ByEnvironment, ActiveByProductGroupId
  - **Handler Unit Tests**:
    - Deploy: Orchestrierung, Partial Failure, Variable-Merging
    - Upgrade: Deployment-Matching, neue Stacks, Variable-Merge-Priority
    - Remove: Umgekehrte Reihenfolge, Partial Failure
    - Status: Deployed/Not-Deployed/Mixed
  - **Integration Tests**: Endpoint-Tests
  - **Edge Cases**:
    - Product mit nur 1 Stack
    - Partial Failure: Stack 3/16 schlägt fehl
    - Upgrade mit neuen Stacks im Target
    - Remove eines teilweise deployed Produkts
    - Concurrent Product-Deploy auf gleichem Environment
  - Abhängig von: Feature 1-10

- [ ] **Dokumentation & Website** — Wiki, Public Website (DE/EN), Roadmap
  - Abhängig von: Feature 11

- [ ] **Phase abschließen** — Alle Tests grün, PR gegen main
  - Abhängig von: alle

## Test-Strategie

- **Unit Tests**: Domain State Machine (alle Pfade), Handler-Orchestrierung, Variable-Merging
- **Integration Tests**: Endpoint-Tests, Repository mit EF Core
- **Manuell**: ams.project (16 Stacks) komplett deployen, upgraden und entfernen über UI

## Offene Punkte

- [x] Partial Failure Handling → Konfigurierbar (continueOnError Flag, default: true)
- [x] Single-Stack-Products → Immer ProductDeployment (einheitlicher Flow)
- [ ] Hooks (CI/CD): `POST /api/hooks/deploy-product` Webhook in separater Phase?
- [x] Health-Sync Interval → Fest 60s

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Deployment-Modell | A) N einzelne Deployments (orchestriert), B) ProductDeployment Aggregate | **B** | Gleicher Frontend-Aufwand, ~20% mehr Backend-Aufwand, dafür PartiallyRunning, Audit-Trail, kein Throwaway-Code |
| Variables UX | A) Alles auf einer Seite, B) Wizard (Shared → Per-Stack) | **B** | Übersichtlicher bei 16+ Stacks |
| SignalR Events | A) Neue Event-Types, B) Bestehende erweitern | **B** | Backward-kompatibel, neue Felder nullable |
| Deployment-Reihenfolge | A) Alphabetisch, B) Manifest-Reihenfolge | **B** | Manifest-Autor kennt die Abhängigkeiten |
| Remove-Reihenfolge | A) Gleich wie Deploy, B) Umgekehrt | **B** | Abhängige Stacks zuerst entfernen |
| Stack-Name | A) Auto-generiert, B) User-Input | **A + optional B** | Auto-Name (`product-stack`), editierbar |
| Consistency | A) Strict (transaktional), B) Eventual (Sync-Service) | **B** | Kein verteilter Transaktions-Overhead |
| Aggregate Boundary | A) Wrapper um Deployment, B) Eigenständig mit FK | **B** | Klare Aggregate-Grenzen, referenziert Deployments nur via DeploymentId |
| Partial Failure | A) Immer weitermachen, B) Abbrechen, C) Konfigurierbar | **C** | `continueOnError` Flag im Request (default: true). User kann bei Deploy wählen |
| Single-Stack Products | A) Direkt DeployStack, B) Immer ProductDeployment | **B** | Einheitlicher Flow, kein Sonderfall. 1-Stack ProductDeployment ist minimal overhead |
| Health-Sync Interval | A) Konfigurierbar, B) Fest 60s | **B** | Einfach, reicht für v1. Kann später konfigurierbar gemacht werden |
