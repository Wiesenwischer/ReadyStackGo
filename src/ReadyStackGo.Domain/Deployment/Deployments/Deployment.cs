namespace ReadyStackGo.Domain.Deployment.Deployments;

using ReadyStackGo.Domain.SharedKernel;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.Deployment.Observers;

/// <summary>
/// Aggregate root representing a stack deployment to an environment.
/// Rich domain model with state machine, progress tracking, and business rules.
/// </summary>
public class Deployment : AggregateRoot<DeploymentId>
{
    public EnvironmentId EnvironmentId { get; private set; } = null!;
    public string StackId { get; private set; } = null!;
    public string StackName { get; private set; } = null!;
    public string? StackVersion { get; private set; }
    public string ProjectName { get; private set; } = null!;
    public DeploymentStatus Status { get; private set; }
    public OperationMode OperationMode { get; private set; } = OperationMode.Normal;
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public UserId DeployedBy { get; private set; } = null!;

    // Progress tracking
    public DeploymentPhase CurrentPhase { get; private set; }
    public int ProgressPercentage { get; private set; }
    public string? ProgressMessage { get; private set; }

    // Cancellation support
    public bool IsCancellationRequested { get; private set; }
    public string? CancellationReason { get; private set; }

    // Deployment variables (resolved values used during deployment)
    private readonly Dictionary<string, string> _variables = new();
    public IReadOnlyDictionary<string, string> Variables => _variables;

    // Maintenance observer configuration (from product definition at deploy time)
    public MaintenanceObserverConfig? MaintenanceObserverConfig { get; private set; }

    // Health check configurations for services (from stack definition at deploy time)
    private readonly List<RuntimeConfig.ServiceHealthCheckConfig> _healthCheckConfigs = new();
    public IReadOnlyCollection<RuntimeConfig.ServiceHealthCheckConfig> HealthCheckConfigs => _healthCheckConfigs.AsReadOnly();

    private readonly List<DeployedService> _services = new();
    public IReadOnlyCollection<DeployedService> Services => _services.AsReadOnly();

    private readonly List<DeploymentPhaseRecord> _phaseHistory = new();
    public IReadOnlyCollection<DeploymentPhaseRecord> PhaseHistory => _phaseHistory.AsReadOnly();

    // Pending upgrade snapshot for rollback functionality
    // Only exists during an upgrade process, before Point of No Return (container start)
    public DeploymentSnapshot? PendingUpgradeSnapshot { get; private set; }

    // Upgrade tracking
    public DateTime? LastUpgradedAt { get; private set; }
    public string? PreviousVersion { get; private set; }
    public int UpgradeCount { get; private set; }

    // For EF Core
    protected Deployment() { }

    private Deployment(
        DeploymentId id,
        EnvironmentId environmentId,
        string stackId,
        string stackName,
        string projectName,
        UserId deployedBy)
    {
        SelfAssertArgumentNotNull(id, "DeploymentId is required.");
        SelfAssertArgumentNotNull(environmentId, "EnvironmentId is required.");
        SelfAssertArgumentNotEmpty(stackId, "Stack ID is required.");
        SelfAssertArgumentNotEmpty(stackName, "Stack name is required.");
        SelfAssertArgumentNotEmpty(projectName, "Project name is required.");
        SelfAssertArgumentNotNull(deployedBy, "DeployedBy is required.");

        Id = id;
        EnvironmentId = environmentId;
        StackId = stackId;
        StackName = stackName;
        ProjectName = projectName;
        DeployedBy = deployedBy;
        Status = DeploymentStatus.Pending;
        CurrentPhase = DeploymentPhase.Initializing;
        ProgressPercentage = 0;
        CreatedAt = SystemClock.UtcNow;

        RecordPhase(DeploymentPhase.Initializing, "Deployment initialized");
        AddDomainEvent(new DeploymentStarted(Id, EnvironmentId, StackName));
    }

    #region Factory Methods

    /// <summary>
    /// Starts a new deployment.
    /// </summary>
    public static Deployment Start(
        DeploymentId id,
        EnvironmentId environmentId,
        string stackId,
        string stackName,
        string projectName,
        UserId deployedBy)
    {
        return new Deployment(id, environmentId, stackId, stackName, projectName, deployedBy);
    }

    #endregion

    #region State Machine

    /// <summary>
    /// Valid state transitions:
    /// Pending -> Running, Failed
    /// Running -> Stopped, Failed
    /// Stopped -> Running (restart), Removed
    /// Failed -> (terminal)
    /// Removed -> (terminal)
    /// </summary>
    private static readonly Dictionary<DeploymentStatus, DeploymentStatus[]> ValidTransitions = new()
    {
        { DeploymentStatus.Pending, new[] { DeploymentStatus.Running, DeploymentStatus.Failed } },
        { DeploymentStatus.Running, new[] { DeploymentStatus.Stopped, DeploymentStatus.Failed } },
        { DeploymentStatus.Stopped, new[] { DeploymentStatus.Running, DeploymentStatus.Removed } },
        { DeploymentStatus.Failed, Array.Empty<DeploymentStatus>() },
        { DeploymentStatus.Removed, Array.Empty<DeploymentStatus>() }
    };

    /// <summary>
    /// Checks if a transition to the target status is valid.
    /// </summary>
    public bool CanTransitionTo(DeploymentStatus targetStatus)
    {
        return ValidTransitions.TryGetValue(Status, out var validTargets)
            && validTargets.Contains(targetStatus);
    }

    /// <summary>
    /// Gets all valid next states from current status.
    /// </summary>
    public IEnumerable<DeploymentStatus> GetValidNextStates()
    {
        return ValidTransitions.TryGetValue(Status, out var validTargets)
            ? validTargets
            : Array.Empty<DeploymentStatus>();
    }

    /// <summary>
    /// Checks if this deployment is in a terminal state.
    /// </summary>
    public bool IsTerminal => Status is DeploymentStatus.Failed or DeploymentStatus.Removed;

    /// <summary>
    /// Checks if this deployment is active (can be interacted with).
    /// </summary>
    public bool IsActive => Status is DeploymentStatus.Pending or DeploymentStatus.Running;

    #endregion

    #region Progress Tracking

    /// <summary>
    /// Sets the stack version.
    /// </summary>
    public void SetStackVersion(string version)
    {
        StackVersion = version;
    }

    /// <summary>
    /// Sets the deployment variables (resolved values used during deployment).
    /// </summary>
    public void SetVariables(IDictionary<string, string> variables)
    {
        _variables.Clear();
        foreach (var kvp in variables)
        {
            _variables[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// Sets the maintenance observer configuration for this deployment.
    /// </summary>
    public void SetMaintenanceObserverConfig(MaintenanceObserverConfig? config)
    {
        MaintenanceObserverConfig = config;
    }

    /// <summary>
    /// Sets the health check configurations for this deployment's services.
    /// </summary>
    public void SetHealthCheckConfigs(IEnumerable<RuntimeConfig.ServiceHealthCheckConfig>? configs)
    {
        _healthCheckConfigs.Clear();
        if (configs != null)
        {
            _healthCheckConfigs.AddRange(configs);
        }
    }

    /// <summary>
    /// Gets the health check configuration for a specific service.
    /// </summary>
    public RuntimeConfig.ServiceHealthCheckConfig? GetHealthCheckConfig(string serviceName)
    {
        return _healthCheckConfigs.FirstOrDefault(c =>
            c.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Updates the deployment progress.
    /// </summary>
    public void UpdateProgress(DeploymentPhase phase, int percentage, string message)
    {
        SelfAssertArgumentTrue(percentage >= 0 && percentage <= 100,
            "Progress percentage must be between 0 and 100.");
        SelfAssertArgumentTrue(!IsTerminal,
            "Cannot update progress on a terminal deployment.");

        CurrentPhase = phase;
        ProgressPercentage = percentage;
        ProgressMessage = message;

        RecordPhase(phase, message);
        AddDomainEvent(new DeploymentProgressUpdated(Id, phase, percentage, message));
    }

    private void RecordPhase(DeploymentPhase phase, string message)
    {
        _phaseHistory.Add(new DeploymentPhaseRecord(phase, message, SystemClock.UtcNow));
    }

    #endregion

    #region State Transitions

    /// <summary>
    /// Marks the deployment as running with deployed services.
    /// </summary>
    public void MarkAsRunning(IEnumerable<DeployedService> services)
    {
        EnsureValidTransition(DeploymentStatus.Running);

        Status = DeploymentStatus.Running;
        CurrentPhase = DeploymentPhase.Completed;
        ProgressPercentage = 100;
        CompletedAt = SystemClock.UtcNow;

        _services.Clear();
        _services.AddRange(services);

        RecordPhase(DeploymentPhase.Completed, "Deployment completed successfully");
        AddDomainEvent(new DeploymentCompleted(Id, Status));
    }

    /// <summary>
    /// Marks the deployment as failed.
    /// </summary>
    public void MarkAsFailed(string errorMessage)
    {
        // Failed can be reached from any non-terminal state
        SelfAssertArgumentTrue(!IsTerminal,
            "Cannot fail an already terminal deployment.");

        Status = DeploymentStatus.Failed;
        CurrentPhase = DeploymentPhase.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = SystemClock.UtcNow;

        RecordPhase(DeploymentPhase.Failed, errorMessage);
        AddDomainEvent(new DeploymentCompleted(Id, Status, errorMessage));
    }

    /// <summary>
    /// Marks the deployment as stopped.
    /// </summary>
    public void MarkAsStopped()
    {
        EnsureValidTransition(DeploymentStatus.Stopped);

        Status = DeploymentStatus.Stopped;
        SetOperationModeStopped();

        foreach (var service in _services)
        {
            service.UpdateStatus("stopped");
        }

        AddDomainEvent(new DeploymentStopped(Id));
    }

    /// <summary>
    /// Restarts a stopped deployment.
    /// </summary>
    public void Restart()
    {
        EnsureValidTransition(DeploymentStatus.Running);
        SelfAssertArgumentTrue(Status == DeploymentStatus.Stopped,
            "Can only restart a stopped deployment.");

        Status = DeploymentStatus.Running;
        SetOperationModeNormal();
        CurrentPhase = DeploymentPhase.Starting;
        ProgressPercentage = 0;

        foreach (var service in _services)
        {
            service.UpdateStatus("starting");
        }

        RecordPhase(DeploymentPhase.Starting, "Deployment restarting");
        AddDomainEvent(new DeploymentRestarted(Id));
    }

    /// <summary>
    /// Marks the deployment as removed.
    /// </summary>
    public void MarkAsRemoved()
    {
        SelfAssertArgumentTrue(Status != DeploymentStatus.Removed,
            "Deployment is already removed.");

        Status = DeploymentStatus.Removed;

        foreach (var service in _services)
        {
            service.UpdateStatus("removed");
        }

        AddDomainEvent(new DeploymentRemoved(Id));
    }

    private void EnsureValidTransition(DeploymentStatus targetStatus)
    {
        SelfAssertArgumentTrue(CanTransitionTo(targetStatus),
            $"Invalid state transition from {Status} to {targetStatus}.");
    }

    #endregion

    #region Operation Mode

    /// <summary>
    /// Puts the deployment into maintenance mode.
    /// </summary>
    public void EnterMaintenance(string? reason = null)
    {
        SelfAssertArgumentTrue(Status == DeploymentStatus.Running,
            "Can only enter maintenance on a running deployment.");
        SelfAssertArgumentTrue(OperationMode.CanTransitionTo(OperationMode.Maintenance),
            $"Cannot transition from {OperationMode.Name} to Maintenance.");

        OperationMode = OperationMode.Maintenance;
        AddDomainEvent(new OperationModeChanged(Id, OperationMode.Maintenance, reason));
    }

    /// <summary>
    /// Exits maintenance mode and returns to normal operation.
    /// </summary>
    public void ExitMaintenance()
    {
        SelfAssertArgumentTrue(OperationMode == OperationMode.Maintenance,
            "Deployment is not in maintenance mode.");

        OperationMode = OperationMode.Normal;
        AddDomainEvent(new OperationModeChanged(Id, OperationMode.Normal, "Exited maintenance mode"));
    }

    /// <summary>
    /// Starts a migration/upgrade process.
    /// </summary>
    public void StartMigration(string targetVersion)
    {
        SelfAssertArgumentNotEmpty(targetVersion, "Target version is required.");
        SelfAssertArgumentTrue(OperationMode.CanTransitionTo(OperationMode.Migrating),
            $"Cannot transition from {OperationMode.Name} to Migrating.");

        OperationMode = OperationMode.Migrating;
        AddDomainEvent(new OperationModeChanged(Id, OperationMode.Migrating, $"Migrating to version {targetVersion}"));
    }

    /// <summary>
    /// Completes a migration successfully.
    /// </summary>
    public void CompleteMigration(string newVersion)
    {
        SelfAssertArgumentTrue(OperationMode == OperationMode.Migrating,
            "Deployment is not in migration mode.");

        StackVersion = newVersion;
        OperationMode = OperationMode.Normal;
        AddDomainEvent(new OperationModeChanged(Id, OperationMode.Normal, $"Migration to {newVersion} completed"));
    }

    /// <summary>
    /// Fails a migration.
    /// </summary>
    public void FailMigration(string errorMessage)
    {
        SelfAssertArgumentTrue(OperationMode == OperationMode.Migrating,
            "Deployment is not in migration mode.");

        OperationMode = OperationMode.Failed;
        ErrorMessage = errorMessage;
        AddDomainEvent(new OperationModeChanged(Id, OperationMode.Failed, errorMessage));
    }

    /// <summary>
    /// Recovers from a failed state by returning to normal operation.
    /// </summary>
    public void RecoverFromFailure()
    {
        SelfAssertArgumentTrue(OperationMode == OperationMode.Failed,
            "Deployment is not in failed state.");

        OperationMode = OperationMode.Normal;
        ErrorMessage = null;
        AddDomainEvent(new OperationModeChanged(Id, OperationMode.Normal, "Recovered from failure"));
    }

    /// <summary>
    /// Sets the operation mode to stopped when the deployment is stopped.
    /// Called internally when MarkAsStopped is invoked.
    /// </summary>
    private void SetOperationModeStopped()
    {
        if (OperationMode != OperationMode.Stopped)
        {
            OperationMode = OperationMode.Stopped;
            AddDomainEvent(new OperationModeChanged(Id, OperationMode.Stopped, "Deployment stopped"));
        }
    }

    /// <summary>
    /// Resets the operation mode to normal when the deployment is restarted.
    /// Called internally when Restart is invoked.
    /// </summary>
    private void SetOperationModeNormal()
    {
        if (OperationMode == OperationMode.Stopped)
        {
            OperationMode = OperationMode.Normal;
            AddDomainEvent(new OperationModeChanged(Id, OperationMode.Normal, "Deployment restarted"));
        }
    }

    #endregion

    #region Cancellation

    /// <summary>
    /// Requests cancellation of a pending deployment.
    /// </summary>
    public void RequestCancellation(string reason)
    {
        SelfAssertArgumentTrue(Status == DeploymentStatus.Pending,
            "Can only cancel a pending deployment.");
        SelfAssertArgumentNotEmpty(reason, "Cancellation reason is required.");

        IsCancellationRequested = true;
        CancellationReason = reason;

        AddDomainEvent(new DeploymentCancellationRequested(Id, reason));
    }

    /// <summary>
    /// Confirms cancellation and marks the deployment as failed.
    /// </summary>
    public void ConfirmCancellation()
    {
        SelfAssertArgumentTrue(IsCancellationRequested,
            "Cancellation was not requested.");

        MarkAsFailed($"Deployment cancelled: {CancellationReason}");
    }

    #endregion

    #region Snapshots & Rollback (Point of No Return Semantics)

    /// <summary>
    /// Creates a snapshot of the current deployment state before an upgrade.
    /// Only one pending snapshot is allowed at a time.
    /// The snapshot is cleared at Point of No Return (container start) or on successful upgrade.
    /// </summary>
    public DeploymentSnapshot? CreateSnapshot(string? description = null)
    {
        SelfAssertArgumentTrue(Status == DeploymentStatus.Running,
            "Can only create snapshot of a running deployment.");
        SelfAssertArgumentTrue(!string.IsNullOrEmpty(StackVersion),
            "Cannot create snapshot without a stack version.");
        SelfAssertArgumentTrue(PendingUpgradeSnapshot == null,
            "A pending upgrade snapshot already exists. Clear it before creating a new one.");

        var snapshotId = DeploymentSnapshotId.Create();
        var serviceSnapshots = _services.Select(s => new ServiceSnapshot(s.ServiceName, s.Image ?? "unknown")).ToList();

        PendingUpgradeSnapshot = DeploymentSnapshot.Create(
            snapshotId,
            Id,
            StackVersion!,
            _variables,
            serviceSnapshots,
            description);

        AddDomainEvent(new DeploymentSnapshotCreated(Id, snapshotId, StackVersion!));

        return PendingUpgradeSnapshot;
    }

    /// <summary>
    /// Clears the pending upgrade snapshot.
    /// Called at Point of No Return (when containers start) or after successful upgrade.
    /// After this, rollback is no longer possible.
    /// </summary>
    public void ClearSnapshot()
    {
        PendingUpgradeSnapshot = null;
    }

    /// <summary>
    /// Rolls back to the previous version using the pending upgrade snapshot.
    /// Only available after a failed upgrade (before Point of No Return).
    /// Consumes (clears) the snapshot.
    /// </summary>
    public void RollbackToPrevious()
    {
        SelfAssertArgumentTrue(PendingUpgradeSnapshot != null,
            "No snapshot available for rollback.");
        SelfAssertArgumentTrue(Status == DeploymentStatus.Failed,
            "Rollback only available after failed upgrade (before container start).");

        var snapshot = PendingUpgradeSnapshot!;
        var snapshotId = snapshot.Id;
        var targetVersion = snapshot.StackVersion;

        // Restore state from snapshot
        StackVersion = targetVersion;
        _variables.Clear();
        foreach (var (key, value) in snapshot.Variables)
        {
            _variables[key] = value;
        }

        // Clear the snapshot (consumed by rollback)
        PendingUpgradeSnapshot = null;

        // Reset status for re-deployment
        Status = DeploymentStatus.Pending;
        CurrentPhase = DeploymentPhase.Initializing;
        ProgressPercentage = 0;
        ProgressMessage = $"Rolling back to version {targetVersion}";
        ErrorMessage = null;
        IsCancellationRequested = false;
        CancellationReason = null;

        RecordPhase(DeploymentPhase.Initializing, $"Rollback to {targetVersion} initiated");
        AddDomainEvent(new DeploymentRolledBack(Id, snapshotId, targetVersion));
    }

    /// <summary>
    /// Checks if rollback is possible.
    /// Rollback is only available when:
    /// - A pending upgrade snapshot exists (upgrade was started)
    /// - The deployment is in Failed status (upgrade failed before container start)
    /// </summary>
    public bool CanRollback()
    {
        return PendingUpgradeSnapshot != null
            && Status == DeploymentStatus.Failed;
    }

    /// <summary>
    /// Gets the version that would be restored on rollback.
    /// Returns null if no rollback is available.
    /// </summary>
    public string? GetRollbackTargetVersion()
    {
        return PendingUpgradeSnapshot?.StackVersion;
    }

    /// <summary>
    /// Records a successful upgrade event, storing the previous version for reference.
    /// Called after successful container start (Point of No Return passed).
    /// </summary>
    public void RecordUpgrade(string previousVersion, string newVersion)
    {
        SelfAssertArgumentNotEmpty(previousVersion, "Previous version is required.");
        SelfAssertArgumentNotEmpty(newVersion, "New version is required.");

        PreviousVersion = previousVersion;
        LastUpgradedAt = SystemClock.UtcNow;
        UpgradeCount++;
        StackVersion = newVersion;

        AddDomainEvent(new DeploymentUpgraded(
            Id,
            previousVersion,
            newVersion,
            LastUpgradedAt.Value));
    }

    /// <summary>
    /// Checks if this deployment can be upgraded.
    /// Only running deployments can be upgraded.
    /// </summary>
    public bool CanUpgrade()
    {
        return Status == DeploymentStatus.Running;
    }

    #endregion

    #region Service Management

    /// <summary>
    /// Updates the services after an upgrade, replacing the current service list.
    /// Used during in-place upgrades to preserve the deployment aggregate with its snapshots.
    /// </summary>
    public void UpdateServicesAfterUpgrade(IEnumerable<DeployedService> newServices)
    {
        SelfAssertArgumentTrue(Status == DeploymentStatus.Running,
            "Can only update services on a running deployment.");

        _services.Clear();
        _services.AddRange(newServices);

        RecordPhase(DeploymentPhase.Completed, $"Upgraded to version {StackVersion}");
        AddDomainEvent(new DeploymentProgressUpdated(Id, DeploymentPhase.Completed, 100, $"Upgraded to version {StackVersion}"));
    }

    /// <summary>
    /// Updates the status of a specific service.
    /// </summary>
    public void UpdateServiceStatus(string serviceName, string status)
    {
        var service = _services.FirstOrDefault(s => s.ServiceName == serviceName);
        if (service != null)
        {
            var previousStatus = service.Status;
            service.UpdateStatus(status);

            if (previousStatus != status)
            {
                AddDomainEvent(new ServiceStatusChanged(Id, serviceName, previousStatus, status));
            }
        }
    }

    /// <summary>
    /// Checks if all services are healthy (running).
    /// </summary>
    public bool AreAllServicesHealthy()
    {
        return _services.All(s => s.Status == "running");
    }

    /// <summary>
    /// Gets services that are not in the expected state.
    /// </summary>
    public IEnumerable<DeployedService> GetUnhealthyServices()
    {
        return _services.Where(s => s.Status != "running");
    }

    /// <summary>
    /// Gets the count of running services.
    /// </summary>
    public int GetRunningServiceCount()
    {
        return _services.Count(s => s.Status == "running");
    }

    #endregion

    #region Duration & Metrics

    /// <summary>
    /// Gets the deployment duration (from creation to completion).
    /// </summary>
    public TimeSpan? GetDuration()
    {
        return CompletedAt.HasValue ? CompletedAt.Value - CreatedAt : null;
    }

    /// <summary>
    /// Gets the time elapsed since deployment started.
    /// </summary>
    public TimeSpan GetElapsedTime()
    {
        return (CompletedAt ?? SystemClock.UtcNow) - CreatedAt;
    }

    /// <summary>
    /// Checks if the deployment has exceeded the expected duration.
    /// </summary>
    public bool IsOverdue(TimeSpan expectedDuration)
    {
        return Status == DeploymentStatus.Pending && GetElapsedTime() > expectedDuration;
    }

    #endregion

    public override string ToString() =>
        $"Deployment [id={Id}, stack={StackName}, status={Status}, phase={CurrentPhase}]";
}

/// <summary>
/// Represents a phase in the deployment lifecycle.
/// </summary>
public enum DeploymentPhase
{
    Initializing,
    ValidatingPrerequisites,
    PullingImages,
    Starting,
    WaitingForHealthChecks,
    Completed,
    Failed
}

/// <summary>
/// Records a deployment phase transition.
/// </summary>
public record DeploymentPhaseRecord(
    DeploymentPhase Phase,
    string Message,
    DateTime Timestamp);
