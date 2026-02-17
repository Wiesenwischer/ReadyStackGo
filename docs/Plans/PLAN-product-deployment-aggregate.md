# Phase: ProductDeployment Aggregate

## Ziel

Einführung eines eigenen `ProductDeployment` Aggregate Root im Deployment Bounded Context. Dieses Aggregate bildet den vollständigen Lifecycle eines Multi-Stack-Produkts ab: Deploy, Upgrade, Rollback und Remove — als atomare, koordinierte Operationen über alle Stacks hinweg.

**Voraussetzung**: Phase "Product Deployment (orchestriert)" muss abgeschlossen sein. Diese Phase baut darauf auf und ersetzt die StackId-Prefix-basierte Gruppierung durch ein echtes Domain-Modell.

**Nicht-Ziel**: Die bestehenden Einzelstack-Deployments werden nicht entfernt. ProductDeployment ist ein Orchestrations-Overlay, das intern weiterhin einzelne Deployments erzeugt (für Container-Zuordnung, Health Monitoring, etc.).

## Analyse

### Ist-Zustand (nach Phase "Product Deployment orchestriert")

| Aspekt | Aktuell | Ziel |
|--------|---------|------|
| Gruppierung | StackId-Prefix-Query (`GetByProductGroupIdAsync`) | Echtes Aggregate mit Child-Entities |
| Status-Tracking | Kein aggregierter Product-Status | `ProductDeploymentStatus` mit State Machine |
| Version-Tracking | Pro Stack in `Deployment.StackVersion` | Zentral in `ProductDeployment.ProductVersion` |
| Shared Variables | Nur zur Deploy-Zeit gemerged, nicht persistiert | Persistiert als Product-Level Snapshot |
| Rollback | Pro Stack einzeln | Koordiniert über alle Stacks |
| Audit | N einzelne Deployment-Events | Product-Level Domain Events |
| Partial Failure | Kein expliziter Status | `PartiallyRunning` Status im Aggregate |

### Bestehende Patterns (aus Deployment Aggregate)

| Pattern | Deployment | ProductDeployment |
|---------|-----------|-------------------|
| Identity | `DeploymentId` (Guid) | `ProductDeploymentId` (Guid) |
| State Machine | `DeploymentStatus` + `ValidTransitions` | `ProductDeploymentStatus` + `ValidTransitions` |
| Factory | `StartInstallation()`, `StartUpgrade()` | `InitiateDeployment()`, `InitiateUpgrade()` |
| Domain Events | `DeploymentStarted`, `DeploymentCompleted` | `ProductDeploymentInitiated`, `ProductDeploymentCompleted` |
| Owned Entities | `DeployedService`, `DeploymentPhaseRecord` | `ProductStackDeployment` |
| JSON Storage | Variables, HealthCheckConfigs | SharedVariables, StackConfigurations |
| Concurrency | `Version` (optimistic locking) | `Version` (optimistic locking) |
| Snapshots | MaintenanceObserverConfig at deploy time | ProductVersion + SharedVariables at deploy time |

### Betroffene Bounded Contexts

- **Domain**: `ProductDeployment` Aggregate Root + `ProductStackDeployment` Child Entity + Events + Value Objects
- **Application**: Commands (Deploy, Upgrade, Rollback, Remove), Queries (Status, List), Handlers
- **Infrastructure.DataAccess**: EF Core Configuration, Repository, Neue Tabellen
- **API**: Neue Endpoints unter `/api/environments/{envId}/product-deployments/`
- **WebUI**: ProductDetail Status, Deployment-Liste gruppiert, Product-Lifecycle Pages

## Domain-Modell

### ProductDeployment (Aggregate Root)

```csharp
namespace ReadyStackGo.Domain.Deployment.ProductDeployments;

/// <summary>
/// Aggregate Root representing the deployment of an entire multi-stack product.
/// Orchestrates the lifecycle of N individual stack deployments as a coordinated unit.
/// </summary>
public class ProductDeployment : AggregateRoot<ProductDeploymentId>
{
    // ── Identity & References ──────────────────────────────────────────

    public EnvironmentId EnvironmentId { get; private set; }
    public string ProductGroupId { get; private set; }        // e.g., "stacks:ams.project"
    public string ProductId { get; private set; }             // e.g., "stacks:ams.project:3.1.0"
    public string ProductName { get; private set; }           // e.g., "ams.project"
    public string ProductDisplayName { get; private set; }    // e.g., "ams.project Enterprise"
    public string ProductVersion { get; private set; }        // e.g., "3.1.0"
    public UserId DeployedBy { get; private set; }

    // ── Status & Lifecycle ─────────────────────────────────────────────

    public ProductDeploymentStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? ErrorMessage { get; private set; }         // Aggregated error info

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
    public int PendingStacks => _stacks.Count(s => s.Status == StackDeploymentStatus.Pending);

    // ── Shared Variables (Product-Level Snapshot) ──────────────────────

    public IReadOnlyDictionary<string, string> SharedVariables { get; private set; }
        = new Dictionary<string, string>();

    // ── Upgrade Tracking ───────────────────────────────────────────────

    public string? PreviousVersion { get; private set; }
    public DateTime? LastUpgradedAt { get; private set; }
    public int UpgradeCount { get; private set; }

    // ── Phase History ──────────────────────────────────────────────────

    private readonly List<ProductDeploymentPhaseRecord> _phaseHistory = new();
    public IReadOnlyCollection<ProductDeploymentPhaseRecord> PhaseHistory
        => _phaseHistory.AsReadOnly();

    // ═══════════════════════════════════════════════════════════════════
    // Factory Methods
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Initiates a new product deployment. All stacks start in Pending status.
    /// </summary>
    public static ProductDeployment InitiateDeployment(
        ProductDeploymentId id,
        EnvironmentId environmentId,
        string productGroupId,
        string productId,
        string productName,
        string productDisplayName,
        string productVersion,
        UserId deployedBy,
        IReadOnlyList<StackDeploymentConfig> stackConfigs,
        IReadOnlyDictionary<string, string> sharedVariables)
    {
        // Validate all arguments via SelfAssertArgumentNotNull/NotEmpty
        // Create ProductStackDeployment child entities from stackConfigs
        // Set Status = Deploying
        // Raise ProductDeploymentInitiated event
        // Record phase: "Initiating deployment of {N} stacks"
    }

    /// <summary>
    /// Initiates a product upgrade. Matches existing stacks to target version.
    /// </summary>
    public static ProductDeployment InitiateUpgrade(
        ProductDeploymentId id,
        EnvironmentId environmentId,
        string productGroupId,
        string productId,
        string productName,
        string productDisplayName,
        string targetVersion,
        UserId deployedBy,
        ProductDeployment existingDeployment,
        IReadOnlyList<StackDeploymentConfig> targetStackConfigs,
        IReadOnlyDictionary<string, string> sharedVariables)
    {
        // Create new aggregate in Upgrading status
        // Copy stack deployment references from existing
        // Match to target stacks, mark new stacks as Pending
        // Store PreviousVersion from existing
        // Raise ProductUpgradeInitiated event
    }

    // ═══════════════════════════════════════════════════════════════════
    // Stack Lifecycle Methods
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Marks a stack as deploying. Called when the orchestrator starts deploying this stack.
    /// </summary>
    public void StartStack(string stackName, DeploymentId deploymentId)
    {
        // Precondition: IsInProgress
        // Find stack by name, set Status = Deploying, set DeploymentId
        // Record phase: "Deploying stack '{stackName}' ({index}/{total})"
        // Raise ProductStackDeploymentStarted event
    }

    /// <summary>
    /// Marks a stack as successfully deployed/upgraded.
    /// </summary>
    public void CompleteStack(string stackName)
    {
        // Precondition: Stack.Status == Deploying
        // Set Stack.Status = Running, Stack.CompletedAt = now
        // Record phase: "Stack '{stackName}' completed ({completed}/{total})"
        // If ALL stacks Running → CompleteDeployment()
        // Raise ProductStackDeploymentCompleted event
    }

    /// <summary>
    /// Marks a stack as failed.
    /// </summary>
    public void FailStack(string stackName, string errorMessage)
    {
        // Precondition: Stack.Status == Deploying
        // Set Stack.Status = Failed, Stack.ErrorMessage = errorMessage
        // Record phase: "Stack '{stackName}' failed: {errorMessage}"
        // Raise ProductStackDeploymentFailed event
        // NOTE: Does NOT fail the whole product — orchestrator decides
    }

    // ═══════════════════════════════════════════════════════════════════
    // Product Lifecycle Methods
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Marks the entire product deployment as complete (all stacks running).
    /// Called automatically by CompleteStack when last stack succeeds.
    /// </summary>
    private void CompleteDeployment()
    {
        // Precondition: All stacks Running
        // Set Status = Running, CompletedAt = now
        // Clear ErrorMessage
        // Raise ProductDeploymentCompleted event
    }

    /// <summary>
    /// Marks the product as partially running (some stacks failed, some running).
    /// Called by the orchestrator when it decides to stop after a failure.
    /// </summary>
    public void MarkAsPartiallyRunning(string reason)
    {
        // Precondition: IsInProgress
        // Precondition: At least one stack Running, at least one Failed or Pending
        // Set Status = PartiallyRunning, CompletedAt = now, ErrorMessage = reason
        // Raise ProductDeploymentPartiallyCompleted event
    }

    /// <summary>
    /// Marks the entire product as failed (no stacks succeeded or critical failure).
    /// </summary>
    public void MarkAsFailed(string errorMessage)
    {
        // Precondition: IsInProgress
        // Set Status = Failed, CompletedAt = now, ErrorMessage
        // Raise ProductDeploymentFailed event
    }

    /// <summary>
    /// Initiates removal of all stacks in reverse order.
    /// </summary>
    public void StartRemoval()
    {
        // Precondition: IsOperational or Status == Failed
        // Set Status = Removing
        // Set all Running/Failed stacks to Pending (for removal)
        // Raise ProductRemovalInitiated event
    }

    /// <summary>
    /// Marks a stack as removed during the removal process.
    /// </summary>
    public void MarkStackRemoved(string stackName)
    {
        // Find stack, set Status = Removed
        // If ALL stacks Removed → MarkAsRemoved()
    }

    /// <summary>
    /// Marks the entire product as removed (terminal state).
    /// </summary>
    private void MarkAsRemoved()
    {
        // Set Status = Removed
        // Raise ProductDeploymentRemoved event
    }

    // ═══════════════════════════════════════════════════════════════════
    // Query Methods
    // ═══════════════════════════════════════════════════════════════════

    public ProductStackDeployment? GetStack(string stackName)
        => _stacks.FirstOrDefault(s => s.StackName == stackName);

    public bool CanUpgrade => IsOperational;
    public bool CanRemove => IsOperational || Status == ProductDeploymentStatus.Failed;
    public bool CanRollback => Status == ProductDeploymentStatus.Failed
                               && PreviousVersion is not null;

    /// <summary>
    /// Returns stacks in deployment order (as defined in manifest).
    /// </summary>
    public IReadOnlyList<ProductStackDeployment> GetStacksInDeployOrder()
        => _stacks.OrderBy(s => s.Order).ToList();

    /// <summary>
    /// Returns stacks in reverse order (for removal).
    /// </summary>
    public IReadOnlyList<ProductStackDeployment> GetStacksInRemoveOrder()
        => _stacks.OrderByDescending(s => s.Order).ToList();
}
```

### ProductStackDeployment (Child Entity)

```csharp
namespace ReadyStackGo.Domain.Deployment.ProductDeployments;

/// <summary>
/// Represents one stack within a product deployment.
/// Links to the actual Deployment aggregate for container-level operations.
/// </summary>
public class ProductStackDeployment
{
    // ── Identity ───────────────────────────────────────────────────────

    public int Id { get; private set; }                       // Auto-generated PK (EF Core)
    public string StackName { get; private set; }             // e.g., "infrastructure"
    public string StackDisplayName { get; private set; }      // e.g., "Infrastructure"
    public string StackId { get; private set; }               // Full catalog ref

    // ── Relationship ───────────────────────────────────────────────────

    public DeploymentId? DeploymentId { get; private set; }   // FK to individual Deployment
    public string? DeploymentStackName { get; private set; }  // User-facing name (e.g., "ams-project-infrastructure")

    // ── Status ─────────────────────────────────────────────────────────

    public StackDeploymentStatus Status { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? ErrorMessage { get; private set; }

    // ── Configuration ──────────────────────────────────────────────────

    public int Order { get; private set; }                    // Manifest order (0-based)
    public int ServiceCount { get; private set; }             // Number of services in stack
    public IReadOnlyDictionary<string, string> Variables { get; private set; }
        = new Dictionary<string, string>();                    // Stack-specific variable snapshot

    // ── Flags ──────────────────────────────────────────────────────────

    public bool IsNewInUpgrade { get; private set; }          // True if stack didn't exist in previous version

    // ── Methods ────────────────────────────────────────────────────────

    public static ProductStackDeployment Create(
        string stackName, string stackDisplayName, string stackId,
        string deploymentStackName, int order, int serviceCount,
        IReadOnlyDictionary<string, string> variables)
    {
        // Validate, set Status = Pending
    }

    internal void MarkAsDeploying(DeploymentId deploymentId)
    {
        Status = StackDeploymentStatus.Deploying;
        DeploymentId = deploymentId;
        StartedAt = DateTime.UtcNow;
    }

    internal void MarkAsRunning()
    {
        Status = StackDeploymentStatus.Running;
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = null;
    }

    internal void MarkAsFailed(string errorMessage)
    {
        Status = StackDeploymentStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage;
    }

    internal void MarkAsRemoved()
    {
        Status = StackDeploymentStatus.Removed;
    }

    internal void MarkAsPendingForRemoval()
    {
        // Reset for removal process
        Status = StackDeploymentStatus.Pending;
        StartedAt = null;
        CompletedAt = null;
    }
}
```

### Enums

```csharp
public enum ProductDeploymentStatus
{
    Deploying,          // Initial deployment in progress
    Running,            // All stacks successfully deployed
    PartiallyRunning,   // Some stacks running, some failed
    Upgrading,          // Product upgrade in progress
    Failed,             // All stacks failed or critical failure
    Removing,           // Removal in progress
    Removed             // Terminal state
}

public enum StackDeploymentStatus
{
    Pending,            // Waiting to be deployed
    Deploying,          // Currently being deployed
    Running,            // Successfully deployed
    Failed,             // Deployment failed
    Removed             // Successfully removed
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

    Failed     ──→ Upgrading         (rollback = upgrade to previous version)
               ──→ Removing          (remove all)

    Removing   ──→ Removed           (all stacks removed, terminal)

    Removed    ──→ (no transitions, terminal)

StackDeploymentStatus:

    Pending    ──→ Deploying   (orchestrator starts this stack)
    Deploying  ──→ Running     (success)
               ──→ Failed      (error)
    Running    ──→ Pending     (reset for removal or upgrade)
               ──→ Removed     (during removal)
    Failed     ──→ Pending     (reset for retry)
               ──→ Removed     (during removal)
```

### Domain Events

```csharp
namespace ReadyStackGo.Domain.Deployment.ProductDeployments;

// ── Product-Level Events ───────────────────────────────────────────

public record ProductDeploymentInitiated(
    ProductDeploymentId ProductDeploymentId,
    EnvironmentId EnvironmentId,
    string ProductName,
    string ProductVersion,
    int TotalStacks) : IDomainEvent;

public record ProductDeploymentCompleted(
    ProductDeploymentId ProductDeploymentId,
    string ProductName,
    string ProductVersion,
    int TotalStacks,
    TimeSpan Duration) : IDomainEvent;

public record ProductDeploymentPartiallyCompleted(
    ProductDeploymentId ProductDeploymentId,
    string ProductName,
    int RunningStacks,
    int FailedStacks,
    string Reason) : IDomainEvent;

public record ProductDeploymentFailed(
    ProductDeploymentId ProductDeploymentId,
    string ProductName,
    string ErrorMessage,
    int CompletedStacks,
    int FailedStacks) : IDomainEvent;

public record ProductUpgradeInitiated(
    ProductDeploymentId ProductDeploymentId,
    string ProductName,
    string PreviousVersion,
    string TargetVersion,
    int TotalStacks) : IDomainEvent;

public record ProductRemovalInitiated(
    ProductDeploymentId ProductDeploymentId,
    string ProductName,
    int TotalStacks) : IDomainEvent;

public record ProductDeploymentRemoved(
    ProductDeploymentId ProductDeploymentId,
    string ProductName) : IDomainEvent;

// ── Stack-Level Events ─────────────────────────────────────────────

public record ProductStackDeploymentStarted(
    ProductDeploymentId ProductDeploymentId,
    string StackName,
    DeploymentId DeploymentId,
    int StackIndex,
    int TotalStacks) : IDomainEvent;

public record ProductStackDeploymentCompleted(
    ProductDeploymentId ProductDeploymentId,
    string StackName,
    DeploymentId DeploymentId,
    int CompletedStacks,
    int TotalStacks) : IDomainEvent;

public record ProductStackDeploymentFailed(
    ProductDeploymentId ProductDeploymentId,
    string StackName,
    string ErrorMessage,
    int CompletedStacks,
    int TotalStacks) : IDomainEvent;
```

### Value Objects

```csharp
public sealed record ProductDeploymentId
{
    public Guid Value { get; }

    private ProductDeploymentId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("ProductDeploymentId cannot be empty");
        Value = value;
    }

    public static ProductDeploymentId NewId() => new(Guid.NewGuid());
    public static ProductDeploymentId Create(Guid value) => new(value);
    public static ProductDeploymentId FromGuid(Guid value) => new(value);
}

public record StackDeploymentConfig(
    string StackName,
    string StackDisplayName,
    string StackId,
    string DeploymentStackName,
    int Order,
    int ServiceCount,
    IReadOnlyDictionary<string, string> Variables);

public record ProductDeploymentPhaseRecord(
    string Phase,
    string Message,
    DateTime Timestamp);
```

## Persistenz (EF Core)

### Tabellen-Schema

```sql
-- Neue Tabelle: ProductDeployments
CREATE TABLE ProductDeployments (
    Id                  TEXT PRIMARY KEY,    -- ProductDeploymentId (Guid)
    EnvironmentId       TEXT NOT NULL,       -- FK Guid
    ProductGroupId      TEXT NOT NULL,       -- e.g., "stacks:ams.project"
    ProductId           TEXT NOT NULL,       -- e.g., "stacks:ams.project:3.1.0"
    ProductName         TEXT NOT NULL,
    ProductDisplayName  TEXT NOT NULL,
    ProductVersion      TEXT NOT NULL,
    DeployedBy          TEXT NOT NULL,       -- UserId Guid
    Status              INTEGER NOT NULL,    -- ProductDeploymentStatus enum
    CreatedAt           TEXT NOT NULL,       -- DateTime UTC
    CompletedAt         TEXT,                -- nullable
    ErrorMessage        TEXT,                -- nullable, max 2000
    PreviousVersion     TEXT,                -- nullable
    LastUpgradedAt      TEXT,                -- nullable
    UpgradeCount        INTEGER NOT NULL DEFAULT 0,
    SharedVariablesJson TEXT NOT NULL,       -- JSON: Dictionary<string,string>
    PhaseHistoryJson    TEXT NOT NULL,       -- JSON: List<ProductDeploymentPhaseRecord>
    Version             INTEGER NOT NULL DEFAULT 0  -- Concurrency token
);

CREATE INDEX IX_ProductDeployments_EnvironmentId ON ProductDeployments(EnvironmentId);
CREATE INDEX IX_ProductDeployments_ProductGroupId ON ProductDeployments(ProductGroupId);
CREATE INDEX IX_ProductDeployments_Status ON ProductDeployments(Status);

-- Neue Tabelle: ProductStackDeployments (owned by ProductDeployment)
CREATE TABLE ProductStackDeployments (
    Id                   INTEGER PRIMARY KEY AUTOINCREMENT,
    ProductDeploymentId  TEXT NOT NULL,      -- FK to ProductDeployments
    StackName            TEXT NOT NULL,
    StackDisplayName     TEXT NOT NULL,
    StackId              TEXT NOT NULL,
    DeploymentId         TEXT,               -- nullable FK to Deployments
    DeploymentStackName  TEXT,
    Status               INTEGER NOT NULL,   -- StackDeploymentStatus enum
    StartedAt            TEXT,
    CompletedAt          TEXT,
    ErrorMessage         TEXT,
    "Order"              INTEGER NOT NULL,
    ServiceCount         INTEGER NOT NULL,
    VariablesJson        TEXT NOT NULL,       -- JSON: Dictionary<string,string>
    IsNewInUpgrade       INTEGER NOT NULL DEFAULT 0,

    FOREIGN KEY (ProductDeploymentId) REFERENCES ProductDeployments(Id) ON DELETE CASCADE
);

CREATE INDEX IX_ProductStackDeployments_ProductDeploymentId
    ON ProductStackDeployments(ProductDeploymentId);
CREATE INDEX IX_ProductStackDeployments_DeploymentId
    ON ProductStackDeployments(DeploymentId);
```

### EF Core Configuration

```csharp
// Neue Datei: Infrastructure.DataAccess/Configurations/ProductDeploymentConfiguration.cs

public class ProductDeploymentConfiguration : IEntityTypeConfiguration<ProductDeployment>
{
    public void Configure(EntityTypeBuilder<ProductDeployment> builder)
    {
        builder.ToTable("ProductDeployments");

        // Identity
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, v => ProductDeploymentId.FromGuid(v));

        // Value Object Conversions
        builder.Property(x => x.EnvironmentId)
            .HasConversion(id => id.Value, v => EnvironmentId.Create(v));
        builder.Property(x => x.DeployedBy)
            .HasConversion(id => id.Value, v => UserId.Create(v));

        // String constraints
        builder.Property(x => x.ProductGroupId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ProductId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ProductName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ProductDisplayName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ProductVersion).HasMaxLength(50).IsRequired();
        builder.Property(x => x.PreviousVersion).HasMaxLength(50);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);

        // JSON columns
        builder.Property(x => x.SharedVariables)
            .HasColumnName("SharedVariablesJson")
            .HasConversion(/* Dictionary<string,string> ↔ JSON */);

        builder.Property(x => x.PhaseHistory)
            .HasColumnName("PhaseHistoryJson")
            .HasConversion(/* List<ProductDeploymentPhaseRecord> ↔ JSON */);

        // Concurrency
        builder.Property(x => x.Version).IsConcurrencyToken();

        // Indexes
        builder.HasIndex(x => x.EnvironmentId);
        builder.HasIndex(x => x.ProductGroupId);
        builder.HasIndex(x => x.Status);

        // Owned Collection: ProductStackDeployments
        builder.OwnsMany(x => x.Stacks, stackBuilder =>
        {
            stackBuilder.ToTable("ProductStackDeployments");
            stackBuilder.WithOwner().HasForeignKey("ProductDeploymentId");
            stackBuilder.Property<int>("Id").ValueGeneratedOnAdd();
            stackBuilder.HasKey("Id");

            stackBuilder.Property(s => s.StackName).HasMaxLength(100).IsRequired();
            stackBuilder.Property(s => s.StackDisplayName).HasMaxLength(200).IsRequired();
            stackBuilder.Property(s => s.StackId).HasMaxLength(200).IsRequired();
            stackBuilder.Property(s => s.DeploymentStackName).HasMaxLength(100);
            stackBuilder.Property(s => s.ErrorMessage).HasMaxLength(2000);

            stackBuilder.Property(s => s.DeploymentId)
                .HasConversion(
                    id => id != null ? id.Value : (Guid?)null,
                    v => v.HasValue ? DeploymentId.FromGuid(v.Value) : null);

            stackBuilder.Property(s => s.Variables)
                .HasColumnName("VariablesJson")
                .HasConversion(/* Dictionary<string,string> ↔ JSON */);

            stackBuilder.HasIndex("DeploymentId");
        });
    }
}
```

### Repository

```csharp
// Neue Datei: Domain/Deployment/ProductDeployments/IProductDeploymentRepository.cs

public interface IProductDeploymentRepository
{
    ProductDeploymentId NextIdentity();

    void Add(ProductDeployment productDeployment);
    void Update(ProductDeployment productDeployment);

    ProductDeployment? Get(ProductDeploymentId id);
    ProductDeployment? GetByProductGroupId(EnvironmentId environmentId, string productGroupId);

    IEnumerable<ProductDeployment> GetByEnvironment(EnvironmentId environmentId);
    IEnumerable<ProductDeployment> GetAllActive();
    IEnumerable<ProductDeployment> GetByStatus(ProductDeploymentStatus status);

    /// <summary>
    /// Gets the active (non-removed) product deployment for a product in an environment.
    /// Returns null if the product is not deployed.
    /// </summary>
    ProductDeployment? GetActiveByProductGroupId(
        EnvironmentId environmentId, string productGroupId);

    void SaveChanges();
}
```

## Application Layer

### Commands & Handlers

```csharp
// ── Deploy Product ─────────────────────────────────────────────────

public record DeployProductCommand(
    Guid EnvironmentId,
    string ProductId,               // e.g., "stacks:ams.project:3.1.0"
    IReadOnlyList<StackDeployRequest> Stacks,
    string? SessionId) : IRequest<DeployProductResult>;

public record StackDeployRequest(
    string StackId,
    string StackName,               // User-facing name
    Dictionary<string, string> Variables);

public record DeployProductResult(
    bool Success,
    Guid? ProductDeploymentId,
    string? ErrorMessage,
    IReadOnlyList<StackDeployStatus> StackStatuses);

public record StackDeployStatus(
    string StackName,
    bool Success,
    Guid? DeploymentId,
    string? ErrorMessage);

// Handler orchestrates:
// 1. Create ProductDeployment aggregate
// 2. For each stack in order:
//    a. productDeployment.StartStack(stackName, deploymentId)
//    b. Dispatch DeployStackCommand via MediatR
//    c. On success: productDeployment.CompleteStack(stackName)
//    d. On failure: productDeployment.FailStack(stackName, error)
// 3. After all: MarkAsPartiallyRunning or CompleteDeployment
// 4. Persist ProductDeployment
// 5. Report progress via SignalR

// ── Upgrade Product ────────────────────────────────────────────────

public record UpgradeProductCommand(
    Guid EnvironmentId,
    string ProductGroupId,
    string TargetProductId,
    IReadOnlyList<StackUpgradeOverride>? StackOverrides,
    string? SessionId) : IRequest<UpgradeProductResult>;

public record StackUpgradeOverride(
    string StackName,
    Dictionary<string, string> Variables);

// Handler orchestrates:
// 1. Load existing ProductDeployment
// 2. Create new ProductDeployment via InitiateUpgrade
// 3. For each stack: match to existing deployment, upgrade
// 4. New stacks: deploy fresh
// 5. Persist

// ── Remove Product ─────────────────────────────────────────────────

public record RemoveProductCommand(
    Guid EnvironmentId,
    string ProductGroupId,
    string? SessionId) : IRequest<RemoveProductResult>;

// Handler orchestrates:
// 1. Load ProductDeployment
// 2. productDeployment.StartRemoval()
// 3. For each stack in REVERSE order:
//    a. Remove individual deployment
//    b. productDeployment.MarkStackRemoved(stackName)
// 4. Persist (Status = Removed)

// ── Get Product Status ─────────────────────────────────────────────

public record GetProductDeploymentStatusQuery(
    Guid EnvironmentId,
    string ProductGroupId) : IRequest<ProductDeploymentStatusResult?>;

public record ProductDeploymentStatusResult(
    Guid ProductDeploymentId,
    string ProductName,
    string ProductVersion,
    string Status,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    IReadOnlyList<StackStatusInfo> Stacks);

public record StackStatusInfo(
    string StackName,
    string StackDisplayName,
    string Status,
    Guid? DeploymentId,
    int ServiceCount,
    string? ErrorMessage);
```

### API Endpoints

| Method | Route | Permission | Description |
|--------|-------|-----------|-------------|
| POST | `/api/environments/{envId}/product-deployments` | Deployments.Create | Deploy entire product |
| GET | `/api/environments/{envId}/product-deployments` | Deployments.Read | List product deployments |
| GET | `/api/environments/{envId}/product-deployments/{id}` | Deployments.Read | Get product deployment detail |
| GET | `/api/environments/{envId}/product-deployments/by-product/{groupId}` | Deployments.Read | Get active deployment by product |
| POST | `/api/environments/{envId}/product-deployments/{id}/upgrade` | Deployments.Update | Upgrade product |
| DELETE | `/api/environments/{envId}/product-deployments/{id}` | Deployments.Delete | Remove product |

## Migration Path

### Phase 1: Coexistence

1. Bestehende N-Deployment-Orchestrierung bleibt bestehen
2. Neue ProductDeployment-Tabellen werden erstellt (`EnsureCreated()`)
3. **Neue Product-Deployments** erzeugen automatisch:
   - Ein ProductDeployment Aggregate (Product-Level Tracking)
   - N Deployment Aggregates (Container-Level, wie bisher)
4. ProductStackDeployment.DeploymentId verlinkt die beiden Ebenen

### Phase 2: Retroaktive Gruppierung

5. Migration-Script: Bestehende einzelne Deployments scannen
6. Wo StackId-Prefix matched → ProductDeployment-Aggregate retroaktiv erstellen
7. Status aus einzelnen Deployments aggregieren

### Phase 3: UI-Umstellung

8. ProductDetail zeigt ProductDeployment-Status (nicht mehr StackId-Prefix-Query)
9. Deployment-Liste gruppiert nach ProductDeployment
10. Einzelstack-Deployments weiterhin sichtbar als Detail-Drill-Down

## Interaktion zwischen Aggregates

```
┌─────────────────────────────────────────────────┐
│            ProductDeployment                     │
│  Status: Running                                 │
│  ProductVersion: 3.1.0                           │
│                                                  │
│  ┌──────────────────────────────────────────┐    │
│  │ ProductStackDeployment: infrastructure   │    │
│  │ Status: Running                          │────│──→ Deployment (AMS-Infra)
│  │ DeploymentId: abc-123                    │    │    Status: Running
│  │ Order: 0                                 │    │    StackId: stacks:ams:3.1:infra
│  └──────────────────────────────────────────┘    │
│                                                  │
│  ┌──────────────────────────────────────────┐    │
│  │ ProductStackDeployment: identity-access  │    │
│  │ Status: Running                          │────│──→ Deployment (AMS-Identity)
│  │ DeploymentId: def-456                    │    │    Status: Running
│  │ Order: 1                                 │    │    StackId: stacks:ams:3.1:identity
│  └──────────────────────────────────────────┘    │
│                                                  │
│  ┌──────────────────────────────────────────┐    │
│  │ ProductStackDeployment: business         │    │
│  │ Status: Running                          │────│──→ Deployment (AMS-Business)
│  │ DeploymentId: ghi-789                    │    │    Status: Running
│  │ Order: 2                                 │    │    StackId: stacks:ams:3.1:business
│  └──────────────────────────────────────────┘    │
└─────────────────────────────────────────────────┘
```

**Consistency Rule**: ProductDeployment ist die "Source of Truth" für Product-Level Status. Einzelne Deployments behalten ihren eigenen Status (für Container-Monitoring, Health-Checks etc.). Bei Inkonsistenzen gewinnt die Deployment-Ebene (z.B. wenn ein Container crasht, wird Deployment.Status = Failed, auch wenn ProductDeployment.Status = Running war).

**Eventual Consistency**: Ein Background-Service (`ProductDeploymentHealthSyncService`) gleicht periodisch ProductStackDeployment.Status mit dem tatsächlichen Deployment.Status ab und aktualisiert den Product-Level Status bei Bedarf.

## Features / Schritte

- [ ] **Feature 1: Domain-Modell** — ProductDeployment Aggregate + ProductStackDeployment + Events + Value Objects
  - Neue Dateien:
    - `Domain/Deployment/ProductDeployments/ProductDeployment.cs`
    - `Domain/Deployment/ProductDeployments/ProductStackDeployment.cs`
    - `Domain/Deployment/ProductDeployments/ProductDeploymentStatus.cs`
    - `Domain/Deployment/ProductDeployments/StackDeploymentStatus.cs`
    - `Domain/Deployment/ProductDeployments/ProductDeploymentId.cs`
    - `Domain/Deployment/ProductDeployments/ProductDeploymentEvents.cs`
    - `Domain/Deployment/ProductDeployments/IProductDeploymentRepository.cs`
  - Tests: State Machine (alle Transitions + ungültige), Factory Methods, Domain Events
  - Abhängig von: –

- [ ] **Feature 2: Persistenz** — EF Core Configuration + Repository
  - Neue Dateien:
    - `Infrastructure.DataAccess/Configurations/ProductDeploymentConfiguration.cs`
    - `Infrastructure.DataAccess/Repositories/ProductDeploymentRepository.cs`
  - Geänderte Dateien:
    - `Infrastructure.DataAccess/ReadyStackGoDbContext.cs` (DbSet hinzufügen)
  - Tests: Repository CRUD, Owned Entities, JSON-Serialisierung, Concurrency
  - Abhängig von: Feature 1

- [ ] **Feature 3: DeployProduct (Aggregate-basiert)** — Command/Handler umstellen
  - Geänderte Dateien:
    - `Application/UseCases/Deployments/DeployProduct/DeployProductHandler.cs`
  - Handler erzeugt jetzt ProductDeployment Aggregate statt nur N einzelne Deployments
  - Bestehende Tests erweitern: Aggregate-Erzeugung, Stack-Status-Updates
  - Abhängig von: Feature 1, 2

- [ ] **Feature 4: UpgradeProduct (Aggregate-basiert)** — Handler umstellen
  - Geänderte Dateien:
    - `Application/UseCases/Deployments/UpgradeProduct/UpgradeProductHandler.cs`
  - Lädt bestehendes ProductDeployment, erzeugt neues via InitiateUpgrade
  - Abhängig von: Feature 1, 2

- [ ] **Feature 5: RemoveProduct (Aggregate-basiert)** — Handler umstellen
  - Geänderte Dateien:
    - `Application/UseCases/Deployments/RemoveProduct/RemoveProductHandler.cs`
  - Ruft StartRemoval() + MarkStackRemoved() auf Aggregate
  - Abhängig von: Feature 1, 2

- [ ] **Feature 6: GetProductDeploymentStatus** — Query/Handler/Endpoint
  - Liest aus ProductDeployment statt StackId-Prefix-Query
  - Kann bestehenden Endpoint ersetzen oder parallel existieren
  - Abhängig von: Feature 2

- [ ] **Feature 7: Health-Sync Service** — Eventual Consistency
  - Neuer Background-Service: `ProductDeploymentHealthSyncService`
  - Periodisch (alle 60s): ProductDeployment.Stacks.Status ↔ Deployment.Status abgleichen
  - Bei Inkonsistenz: ProductStackDeployment.Status aktualisieren
  - Abhängig von: Feature 2

- [ ] **Feature 8: UI-Integration** — ProductDetail + Deployment-Liste
  - ProductDetail: Status aus ProductDeployment Aggregate lesen
  - Deployment-Liste: Product-Gruppierung über ProductDeployment
  - Abhängig von: Feature 6

- [ ] **Feature 9: Retroaktive Migration** — Bestehende Deployments gruppieren
  - Migration-Script: Scan aller aktiven Deployments, GroupId berechnen, ProductDeployments erstellen
  - Abhängig von: Feature 2

- [ ] **Feature 10: Tests** — Unit + Integration
  - Domain: State Machine (alle Pfade), Events, Validierung, Edge Cases
  - Repository: CRUD, Queries, JSON-Serialisierung
  - Handler: Orchestrierung, Partial Failure, Concurrency
  - Integration: Endpoint-Tests
  - Abhängig von: Feature 1-8

- [ ] **Dokumentation & Website**
  - Abhängig von: Feature 10

- [ ] **Phase abschließen** — Alle Tests grün, PR gegen main

## Test-Strategie

- **Domain Unit Tests** (Hauptfokus):
  - State Machine: Alle gültigen Transitions, alle ungültigen Transitions
  - Factory Methods: Validierung, Default-Werte, Events
  - CompleteStack → automatisches CompleteDeployment wenn letzter Stack
  - FailStack → PartiallyRunning vs Failed
  - RemoveOrder: Umgekehrte Reihenfolge
  - CanUpgrade/CanRemove/CanRollback für jeden Status
- **Repository Integration Tests**:
  - CRUD mit Owned Entities (Stacks laden korrekt)
  - JSON-Serialisierung (SharedVariables, PhaseHistory)
  - Concurrency Token (gleichzeitige Updates)
  - Queries: ByEnvironment, ByProductGroupId, ByStatus, Active
- **Handler Unit Tests**:
  - Orchestrierung: Korrekte Reihenfolge, Progress-Events
  - Partial Failure: 3/16 schlägt fehl → PartiallyRunning
  - Upgrade: Version-Match, neue Stacks, Variable-Merge
  - Remove: Umgekehrte Reihenfolge, alle Stacks entfernt

## Offene Punkte

- [ ] Soll ProductDeployment ein eigenes Bounded Context Subdirectory bekommen (`Domain/Deployment/ProductDeployments/`) oder in `Domain/Deployment/Deployments/` bleiben?
- [ ] Soll das Health-Sync-Interval konfigurierbar sein?
- [ ] Soll bei PartiallyRunning ein automatischer Retry möglich sein?

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Aggregate Boundary | A) ProductDeployment als Wrapper um Deployment, B) Eigenständig mit FK | **B** | Klare Aggregate-Grenzen, ProductDeployment referenziert Deployments nur via DeploymentId |
| Persistenz | A) Gleiche Tabelle wie Deployment, B) Eigene Tabellen | **B** | Separate Concerns, keine Schema-Migration nötig für bestehendes Deployment |
| Child Entity vs Value Object | A) StackDeployment als VO, B) als Entity | **B** | Hat eigene Identität (Id), eigenen Lifecycle (Status), wird direkt mutiert |
| Consistency | A) Strict (transaktional), B) Eventual | **B** | Health-Sync-Service gleicht periodisch ab, kein verteilter Transaktions-Overhead |
| Status bei Stack-Crash | A) ProductDeployment wird sofort Failed, B) Bleibt Running bis Sync | **B** | Deployment-Level Health-Monitoring reagiert zuerst, Product-Level folgt via Sync |
