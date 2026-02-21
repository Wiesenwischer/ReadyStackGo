namespace ReadyStackGo.Domain.Deployment.ProductDeployments;

using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Aggregate root representing a product-level deployment.
/// Orchestrates the deployment of multiple stacks as a single unit.
///
/// State machine:
///   Deploying        → Running | PartiallyRunning | Failed
///   Running          → Upgrading | Removing
///   PartiallyRunning → Upgrading | Removing
///   Upgrading        → Running | PartiallyRunning | Failed
///   Failed           → Upgrading | Removing
///   Removing         → Removed (terminal)
/// </summary>
public class ProductDeployment : AggregateRoot<ProductDeploymentId>
{
    // ── Identity & References ──────────────────────────────────────────
    public EnvironmentId EnvironmentId { get; private set; } = null!;
    public string ProductGroupId { get; private set; } = null!;
    public string ProductId { get; private set; } = null!;
    public string ProductName { get; private set; } = null!;
    public string ProductDisplayName { get; private set; } = null!;
    public string ProductVersion { get; private set; } = null!;
    public UserId DeployedBy { get; private set; } = null!;

    // ── Status & Lifecycle ───────────────────────────────────────────
    public ProductDeploymentStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool ContinueOnError { get; private set; }

    public bool IsTerminal => Status == ProductDeploymentStatus.Removed;
    public bool IsInProgress => Status is ProductDeploymentStatus.Deploying
                                       or ProductDeploymentStatus.Upgrading
                                       or ProductDeploymentStatus.Removing;
    public bool IsOperational => Status is ProductDeploymentStatus.Running
                                        or ProductDeploymentStatus.PartiallyRunning;

    // ── Stacks (Child Entities) ──────────────────────────────────────
    private readonly List<ProductStackDeployment> _stacks = new();
    public IReadOnlyList<ProductStackDeployment> Stacks => _stacks.AsReadOnly();

    public int TotalStacks => _stacks.Count;
    public int CompletedStacks => _stacks.Count(s => s.Status == StackDeploymentStatus.Running);
    public int FailedStacks => _stacks.Count(s => s.Status == StackDeploymentStatus.Failed);
    public int RemovedStacks => _stacks.Count(s => s.Status == StackDeploymentStatus.Removed);

    // ── Shared Variables ─────────────────────────────────────────────
    private readonly Dictionary<string, string> _sharedVariables = new();
    public IReadOnlyDictionary<string, string> SharedVariables => _sharedVariables;

    // ── Upgrade Tracking ─────────────────────────────────────────────
    public string? PreviousVersion { get; private set; }
    public DateTime? LastUpgradedAt { get; private set; }
    public int UpgradeCount { get; private set; }

    // ── Phase History ────────────────────────────────────────────────
    private readonly List<ProductDeploymentPhaseRecord> _phaseHistory = new();
    public IReadOnlyCollection<ProductDeploymentPhaseRecord> PhaseHistory => _phaseHistory.AsReadOnly();

    // ── Valid Transitions ────────────────────────────────────────────
    private static readonly Dictionary<ProductDeploymentStatus, ProductDeploymentStatus[]> ValidTransitions = new()
    {
        { ProductDeploymentStatus.Deploying, new[] { ProductDeploymentStatus.Running, ProductDeploymentStatus.PartiallyRunning, ProductDeploymentStatus.Failed } },
        { ProductDeploymentStatus.Running, new[] { ProductDeploymentStatus.Upgrading, ProductDeploymentStatus.Removing } },
        { ProductDeploymentStatus.PartiallyRunning, new[] { ProductDeploymentStatus.Upgrading, ProductDeploymentStatus.Removing } },
        { ProductDeploymentStatus.Upgrading, new[] { ProductDeploymentStatus.Running, ProductDeploymentStatus.PartiallyRunning, ProductDeploymentStatus.Failed } },
        { ProductDeploymentStatus.Failed, new[] { ProductDeploymentStatus.Upgrading, ProductDeploymentStatus.Removing } },
        { ProductDeploymentStatus.Removing, new[] { ProductDeploymentStatus.Removed } },
        { ProductDeploymentStatus.Removed, Array.Empty<ProductDeploymentStatus>() }
    };

    // For EF Core
    protected ProductDeployment() { }

    // ═══════════════════════════════════════════════════════════════════
    // Factory Methods
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Initiates a new product deployment with the given stack configurations.
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
        IReadOnlyDictionary<string, string> sharedVariables,
        bool continueOnError = true)
    {
        AssertArgumentNotNull(id, "ProductDeploymentId is required.");
        AssertArgumentNotNull(environmentId, "EnvironmentId is required.");
        AssertArgumentNotEmpty(productGroupId, "Product group ID is required.");
        AssertArgumentNotEmpty(productId, "Product ID is required.");
        AssertArgumentNotEmpty(productName, "Product name is required.");
        AssertArgumentNotEmpty(productDisplayName, "Product display name is required.");
        AssertArgumentNotEmpty(productVersion, "Product version is required.");
        AssertArgumentNotNull(deployedBy, "DeployedBy is required.");
        AssertArgumentNotNull(stackConfigs, "Stack configs are required.");
        AssertArgumentTrue(stackConfigs.Count > 0, "At least one stack config is required.");

        var deployment = new ProductDeployment
        {
            Id = id,
            EnvironmentId = environmentId,
            ProductGroupId = productGroupId,
            ProductId = productId,
            ProductName = productName,
            ProductDisplayName = productDisplayName,
            ProductVersion = productVersion,
            DeployedBy = deployedBy,
            Status = ProductDeploymentStatus.Deploying,
            CreatedAt = SystemClock.UtcNow,
            ContinueOnError = continueOnError
        };

        if (sharedVariables != null)
        {
            foreach (var kvp in sharedVariables)
                deployment._sharedVariables[kvp.Key] = kvp.Value;
        }

        for (var i = 0; i < stackConfigs.Count; i++)
        {
            var config = stackConfigs[i];
            deployment._stacks.Add(new ProductStackDeployment(
                config.StackName,
                config.StackDisplayName,
                config.StackId,
                i,
                config.ServiceCount,
                config.Variables));
        }

        deployment.RecordPhase("Deployment initiated");
        deployment.AddDomainEvent(new ProductDeploymentInitiated(
            id, environmentId, productName, productVersion, stackConfigs.Count));

        return deployment;
    }

    /// <summary>
    /// Initiates a product upgrade from the current version to a target version.
    /// Creates a new ProductDeployment in Upgrading status.
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
        IReadOnlyDictionary<string, string> sharedVariables,
        bool continueOnError = true)
    {
        AssertArgumentNotNull(id, "ProductDeploymentId is required.");
        AssertArgumentNotNull(environmentId, "EnvironmentId is required.");
        AssertArgumentNotEmpty(productGroupId, "Product group ID is required.");
        AssertArgumentNotEmpty(productId, "Product ID is required.");
        AssertArgumentNotEmpty(productName, "Product name is required.");
        AssertArgumentNotEmpty(productDisplayName, "Product display name is required.");
        AssertArgumentNotEmpty(targetVersion, "Target version is required.");
        AssertArgumentNotNull(deployedBy, "DeployedBy is required.");
        AssertArgumentNotNull(existingDeployment, "Existing deployment is required.");
        AssertArgumentTrue(existingDeployment.CanUpgrade, "Existing deployment cannot be upgraded.");
        AssertArgumentNotNull(targetStackConfigs, "Target stack configs are required.");
        AssertArgumentTrue(targetStackConfigs.Count > 0, "At least one target stack config is required.");

        var deployment = new ProductDeployment
        {
            Id = id,
            EnvironmentId = environmentId,
            ProductGroupId = productGroupId,
            ProductId = productId,
            ProductName = productName,
            ProductDisplayName = productDisplayName,
            ProductVersion = targetVersion,
            DeployedBy = deployedBy,
            Status = ProductDeploymentStatus.Upgrading,
            CreatedAt = SystemClock.UtcNow,
            PreviousVersion = existingDeployment.ProductVersion,
            UpgradeCount = existingDeployment.UpgradeCount + 1,
            ContinueOnError = continueOnError
        };

        if (sharedVariables != null)
        {
            foreach (var kvp in sharedVariables)
                deployment._sharedVariables[kvp.Key] = kvp.Value;
        }

        var existingStackNames = existingDeployment.Stacks
            .Select(s => s.StackName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < targetStackConfigs.Count; i++)
        {
            var config = targetStackConfigs[i];
            var isNew = !existingStackNames.Contains(config.StackName);
            deployment._stacks.Add(new ProductStackDeployment(
                config.StackName,
                config.StackDisplayName,
                config.StackId,
                i,
                config.ServiceCount,
                config.Variables,
                isNewInUpgrade: isNew));
        }

        deployment.RecordPhase($"Upgrade initiated from {existingDeployment.ProductVersion} to {targetVersion}");
        deployment.AddDomainEvent(new ProductUpgradeInitiated(
            id, productName, existingDeployment.ProductVersion, targetVersion, targetStackConfigs.Count));

        return deployment;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Stack Lifecycle (called by orchestrator)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Marks a stack as started with its Deployment reference.
    /// </summary>
    public void StartStack(string stackName, DeploymentId deploymentId, string deploymentStackName)
    {
        SelfAssertStateTrue(
            Status is ProductDeploymentStatus.Deploying or ProductDeploymentStatus.Upgrading,
            $"Cannot start stack when product status is {Status}.");

        var stack = FindStack(stackName);
        stack.Start(deploymentId, deploymentStackName);

        RecordPhase($"Stack '{stackName}' started");
        AddDomainEvent(new ProductStackDeploymentStarted(
            Id, stackName, deploymentId, stack.Order, TotalStacks));
    }

    /// <summary>
    /// Marks a stack as completed. If all stacks are running, completes the deployment.
    /// </summary>
    public void CompleteStack(string stackName)
    {
        SelfAssertStateTrue(
            Status is ProductDeploymentStatus.Deploying or ProductDeploymentStatus.Upgrading,
            $"Cannot complete stack when product status is {Status}.");

        var stack = FindStack(stackName);
        stack.Complete();

        RecordPhase($"Stack '{stackName}' completed");
        AddDomainEvent(new ProductStackDeploymentCompleted(
            Id, stackName, stack.DeploymentId!, CompletedStacks, TotalStacks));

        if (_stacks.All(s => s.Status == StackDeploymentStatus.Running))
        {
            CompleteDeployment();
        }
    }

    /// <summary>
    /// Marks a stack as failed.
    /// </summary>
    public void FailStack(string stackName, string errorMessage)
    {
        SelfAssertStateTrue(
            Status is ProductDeploymentStatus.Deploying or ProductDeploymentStatus.Upgrading,
            $"Cannot fail stack when product status is {Status}.");

        var stack = FindStack(stackName);
        stack.Fail(errorMessage);

        RecordPhase($"Stack '{stackName}' failed: {errorMessage}");
        AddDomainEvent(new ProductStackDeploymentFailed(
            Id, stackName, errorMessage, CompletedStacks, TotalStacks));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Product Lifecycle
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Marks the product deployment as partially running.
    /// Called when some stacks succeeded and some failed, and no more stacks will be attempted.
    /// </summary>
    public void MarkAsPartiallyRunning(string reason)
    {
        SelfAssertArgumentNotEmpty(reason, "Reason is required.");
        EnsureValidTransition(ProductDeploymentStatus.PartiallyRunning);
        SelfAssertStateTrue(CompletedStacks > 0,
            "Cannot be partially running with no completed stacks.");
        SelfAssertStateTrue(FailedStacks > 0 || _stacks.Any(s => s.Status == StackDeploymentStatus.Pending),
            "Cannot be partially running when all stacks succeeded.");

        Status = ProductDeploymentStatus.PartiallyRunning;
        CompletedAt = SystemClock.UtcNow;
        ErrorMessage = reason;

        RecordPhase($"Partially running: {reason}");
        AddDomainEvent(new ProductDeploymentPartiallyCompleted(
            Id, ProductName, CompletedStacks, FailedStacks, reason));
    }

    /// <summary>
    /// Marks the entire product deployment as failed.
    /// </summary>
    public void MarkAsFailed(string errorMessage)
    {
        SelfAssertArgumentNotEmpty(errorMessage, "Error message is required.");
        EnsureValidTransition(ProductDeploymentStatus.Failed);

        Status = ProductDeploymentStatus.Failed;
        CompletedAt = SystemClock.UtcNow;
        ErrorMessage = errorMessage;

        RecordPhase($"Failed: {errorMessage}");
        AddDomainEvent(new ProductDeploymentFailed(
            Id, ProductName, errorMessage, CompletedStacks, FailedStacks));
    }

    /// <summary>
    /// Starts the removal of all stacks.
    /// </summary>
    public void StartRemoval()
    {
        EnsureValidTransition(ProductDeploymentStatus.Removing);

        Status = ProductDeploymentStatus.Removing;
        CompletedAt = null;
        ErrorMessage = null;

        foreach (var stack in _stacks)
        {
            stack.ResetToPending();
        }

        RecordPhase("Removal initiated");
        AddDomainEvent(new ProductRemovalInitiated(Id, ProductName, TotalStacks));
    }

    /// <summary>
    /// Marks a stack as removed during the removal process.
    /// If all stacks are removed, the product deployment transitions to Removed.
    /// </summary>
    public void MarkStackRemoved(string stackName)
    {
        SelfAssertStateTrue(Status == ProductDeploymentStatus.Removing,
            $"Cannot mark stack as removed when product status is {Status}.");

        var stack = FindStack(stackName);
        stack.MarkRemoved();

        RecordPhase($"Stack '{stackName}' removed");

        if (_stacks.All(s => s.Status == StackDeploymentStatus.Removed))
        {
            Status = ProductDeploymentStatus.Removed;
            CompletedAt = SystemClock.UtcNow;

            RecordPhase("All stacks removed");
            AddDomainEvent(new ProductDeploymentRemoved(Id, ProductName));
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Health Sync (Eventual Consistency)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Synchronizes a stack's status from its underlying Deployment aggregate.
    /// Only effective when the product is in an operational state (Running/PartiallyRunning).
    /// Returns true if any status was changed.
    /// </summary>
    public bool SyncStackHealth(string stackName, StackDeploymentStatus actualStatus, string? errorMessage = null)
    {
        if (!IsOperational) return false;

        var stack = _stacks.FirstOrDefault(s =>
            s.StackName.Equals(stackName, StringComparison.OrdinalIgnoreCase));
        if (stack == null) return false;

        return stack.SyncStatus(actualStatus, errorMessage);
    }

    /// <summary>
    /// Recalculates the product-level status based on all stack statuses.
    /// Called after syncing individual stacks. Returns true if status changed.
    /// </summary>
    public bool RecalculateProductStatus()
    {
        if (!IsOperational) return false;

        var allRunning = _stacks.All(s => s.Status == StackDeploymentStatus.Running);
        var anyFailed = _stacks.Any(s => s.Status == StackDeploymentStatus.Failed);
        var anyRunning = _stacks.Any(s => s.Status == StackDeploymentStatus.Running);

        if (allRunning && Status != ProductDeploymentStatus.Running)
        {
            Status = ProductDeploymentStatus.Running;
            ErrorMessage = null;
            RecordPhase("Health sync: all stacks running");
            return true;
        }

        if (anyFailed && anyRunning && Status != ProductDeploymentStatus.PartiallyRunning)
        {
            Status = ProductDeploymentStatus.PartiallyRunning;
            ErrorMessage = $"{FailedStacks} of {TotalStacks} stacks failed";
            RecordPhase($"Health sync: partially running ({FailedStacks} failed)");
            return true;
        }

        return false;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Query Methods
    // ═══════════════════════════════════════════════════════════════════

    public bool CanUpgrade => IsOperational;
    public bool CanRemove => IsOperational || Status == ProductDeploymentStatus.Failed;
    public bool CanRollback => Status == ProductDeploymentStatus.Failed && PreviousVersion is not null;

    /// <summary>
    /// Gets stacks in manifest deployment order (ascending).
    /// </summary>
    public IReadOnlyList<ProductStackDeployment> GetStacksInDeployOrder()
    {
        return _stacks.OrderBy(s => s.Order).ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets stacks in reverse order for removal (descending).
    /// </summary>
    public IReadOnlyList<ProductStackDeployment> GetStacksInRemoveOrder()
    {
        return _stacks.OrderByDescending(s => s.Order).ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets the deployment duration.
    /// </summary>
    public TimeSpan? GetDuration()
    {
        return CompletedAt.HasValue ? CompletedAt.Value - CreatedAt : null;
    }

    /// <summary>
    /// Checks if a transition to the target status is valid.
    /// </summary>
    public bool CanTransitionTo(ProductDeploymentStatus targetStatus)
    {
        return ValidTransitions.TryGetValue(Status, out var validTargets)
            && validTargets.Contains(targetStatus);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Private Helpers
    // ═══════════════════════════════════════════════════════════════════

    private void CompleteDeployment()
    {
        var wasUpgrade = Status == ProductDeploymentStatus.Upgrading;

        Status = ProductDeploymentStatus.Running;
        CompletedAt = SystemClock.UtcNow;
        ErrorMessage = null;

        if (wasUpgrade)
        {
            LastUpgradedAt = CompletedAt;
        }

        var duration = GetDuration()!.Value;
        RecordPhase(wasUpgrade
            ? $"Upgrade to {ProductVersion} completed"
            : "Deployment completed");

        AddDomainEvent(new ProductDeploymentCompleted(
            Id, ProductName, ProductVersion, TotalStacks, duration));
    }

    private ProductStackDeployment FindStack(string stackName)
    {
        var stack = _stacks.FirstOrDefault(s =>
            s.StackName.Equals(stackName, StringComparison.OrdinalIgnoreCase));

        SelfAssertStateTrue(stack != null, $"Stack '{stackName}' not found in this product deployment.");
        return stack!;
    }

    private void EnsureValidTransition(ProductDeploymentStatus targetStatus)
    {
        SelfAssertStateTrue(CanTransitionTo(targetStatus),
            $"Invalid state transition from {Status} to {targetStatus}.");
    }

    private void RecordPhase(string message)
    {
        _phaseHistory.Add(new ProductDeploymentPhaseRecord(message, SystemClock.UtcNow));
    }

    public override string ToString() =>
        $"ProductDeployment [id={Id}, product={ProductName}, version={ProductVersion}, status={Status}]";
}

/// <summary>
/// Records a phase transition in the product deployment lifecycle.
/// </summary>
public record ProductDeploymentPhaseRecord(string Message, DateTime Timestamp);

/// <summary>
/// Configuration for a stack to be deployed as part of a product.
/// Used as input to the factory methods.
/// </summary>
public record StackDeploymentConfig(
    string StackName,
    string StackDisplayName,
    string StackId,
    int ServiceCount,
    IReadOnlyDictionary<string, string> Variables);
