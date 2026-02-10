namespace ReadyStackGo.Domain.Deployment.Deployments;

using ReadyStackGo.Domain.SharedKernel;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.Deployment.Observers;

/// <summary>
/// Aggregate root representing a stack deployment to an environment.
/// Rich domain model with state machine, progress tracking, and business rules.
///
/// State machine:
/// - Installing -> Running, Failed
/// - Upgrading -> Running, Failed
/// - Running -> Upgrading, Removed (OperationMode: Normal or Maintenance)
/// - Failed -> Upgrading (retry/rollback), Removed
/// - Removed -> (terminal)
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

    // Init container execution results (persisted as JSON)
    private readonly List<InitContainerResult> _initContainerResults = new();
    public IReadOnlyCollection<InitContainerResult> InitContainerResults => _initContainerResults.AsReadOnly();

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
        UserId deployedBy,
        DeploymentStatus initialStatus)
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
        Status = initialStatus;
        CurrentPhase = DeploymentPhase.Initializing;
        ProgressPercentage = 0;
        CreatedAt = SystemClock.UtcNow;

        RecordPhase(DeploymentPhase.Initializing, $"{initialStatus} initialized");
        AddDomainEvent(new DeploymentStarted(Id, EnvironmentId, StackName));
    }

    #region Factory Methods

    /// <summary>
    /// Starts a new installation deployment.
    /// Creates a deployment in Installing status.
    /// </summary>
    public static Deployment StartInstallation(
        DeploymentId id,
        EnvironmentId environmentId,
        string stackId,
        string stackName,
        string projectName,
        UserId deployedBy)
    {
        return new Deployment(id, environmentId, stackId, stackName, projectName, deployedBy, DeploymentStatus.Installing);
    }

    /// <summary>
    /// Starts an upgrade deployment.
    /// Creates a deployment in Upgrading status.
    /// </summary>
    public static Deployment StartUpgrade(
        DeploymentId id,
        EnvironmentId environmentId,
        string stackId,
        string stackName,
        string projectName,
        UserId deployedBy,
        string? previousVersion = null)
    {
        var deployment = new Deployment(id, environmentId, stackId, stackName, projectName, deployedBy, DeploymentStatus.Upgrading);
        deployment.PreviousVersion = previousVersion;
        return deployment;
    }

    #endregion

    #region State Machine

    /// <summary>
    /// Valid state transitions:
    /// Installing -> Running, Failed
    /// Upgrading -> Running, Failed
    /// Running -> Upgrading, Removed
    /// Failed -> Upgrading (retry/rollback), Removed
    /// Removed -> (terminal)
    /// </summary>
    private static readonly Dictionary<DeploymentStatus, DeploymentStatus[]> ValidTransitions = new()
    {
        { DeploymentStatus.Installing, new[] { DeploymentStatus.Running, DeploymentStatus.Failed } },
        { DeploymentStatus.Upgrading, new[] { DeploymentStatus.Running, DeploymentStatus.Failed } },
        { DeploymentStatus.Running, new[] { DeploymentStatus.Upgrading, DeploymentStatus.Removed } },
        { DeploymentStatus.Failed, new[] { DeploymentStatus.Upgrading, DeploymentStatus.Removed } },
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
    public bool IsTerminal => Status == DeploymentStatus.Removed;

    /// <summary>
    /// Checks if this deployment is in progress (installing or upgrading).
    /// </summary>
    public bool IsInProgress => Status is DeploymentStatus.Installing or DeploymentStatus.Upgrading;

    /// <summary>
    /// Checks if this deployment is operational.
    /// </summary>
    public bool IsOperational => Status == DeploymentStatus.Running;

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
    /// Sets the stack ID (catalog reference). Used during upgrades when the
    /// catalog stack ID changes (e.g., upgrading to a new version).
    /// </summary>
    public void SetStackId(string stackId)
    {
        StackId = stackId;
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
        SelfAssertArgumentTrue(IsInProgress,
            "Cannot update progress on a non-in-progress deployment.");

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
    /// Marks the deployment as running.
    /// Called when installation or upgrade completes successfully.
    /// Services should already be added via AddService/SetServiceContainerInfo.
    /// </summary>
    public void MarkAsRunning()
    {
        EnsureValidTransition(DeploymentStatus.Running);

        var wasUpgrade = Status == DeploymentStatus.Upgrading;

        Status = DeploymentStatus.Running;
        OperationMode = OperationMode.Normal;
        CurrentPhase = DeploymentPhase.Completed;
        ProgressPercentage = 100;
        CompletedAt = SystemClock.UtcNow;
        ErrorMessage = null;

        var message = wasUpgrade
            ? $"Upgrade to version {StackVersion} completed successfully"
            : "Deployment completed successfully";

        RecordPhase(DeploymentPhase.Completed, message);
        AddDomainEvent(new DeploymentCompleted(Id, Status));
    }

    /// <summary>
    /// Marks the deployment as failed.
    /// </summary>
    public void MarkAsFailed(string errorMessage)
    {
        SelfAssertArgumentTrue(IsInProgress,
            "Can only fail an in-progress deployment.");

        Status = DeploymentStatus.Failed;
        CurrentPhase = DeploymentPhase.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = SystemClock.UtcNow;

        RecordPhase(DeploymentPhase.Failed, errorMessage);
        AddDomainEvent(new DeploymentCompleted(Id, Status, errorMessage));
    }

    /// <summary>
    /// Starts an upgrade from a running deployment.
    /// Transitions from Running to Upgrading.
    /// </summary>
    public void StartUpgradeProcess(string targetVersion)
    {
        SelfAssertArgumentNotEmpty(targetVersion, "Target version is required.");
        SelfAssertArgumentTrue(Status == DeploymentStatus.Running,
            "Can only upgrade a running deployment.");

        PreviousVersion = StackVersion;
        Status = DeploymentStatus.Upgrading;
        CurrentPhase = DeploymentPhase.Initializing;
        ProgressPercentage = 0;
        CompletedAt = null;

        RecordPhase(DeploymentPhase.Initializing, $"Upgrading to version {targetVersion}");
        AddDomainEvent(new DeploymentStarted(Id, EnvironmentId, StackName));
    }

    /// <summary>
    /// Starts a rollback/retry from a failed deployment.
    /// Transitions from Failed to Upgrading.
    /// </summary>
    public void StartRollbackProcess(string targetVersion)
    {
        SelfAssertArgumentNotEmpty(targetVersion, "Target version is required.");
        SelfAssertArgumentTrue(Status == DeploymentStatus.Failed,
            "Can only rollback a failed deployment.");

        Status = DeploymentStatus.Upgrading;
        CurrentPhase = DeploymentPhase.Initializing;
        ProgressPercentage = 0;
        ErrorMessage = null;
        CompletedAt = null;

        RecordPhase(DeploymentPhase.Initializing, $"Rolling back to version {targetVersion}");
        AddDomainEvent(new DeploymentStarted(Id, EnvironmentId, StackName));
    }

    /// <summary>
    /// Marks the deployment as removed.
    /// </summary>
    public void MarkAsRemoved()
    {
        SelfAssertArgumentTrue(Status != DeploymentStatus.Removed,
            "Deployment is already removed.");
        SelfAssertArgumentTrue(Status == DeploymentStatus.Running || Status == DeploymentStatus.Failed,
            "Can only remove a running or failed deployment.");

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

    #region Operation Mode (only valid when Running)

    /// <summary>
    /// Puts the deployment into maintenance mode.
    /// Only valid when deployment is Running.
    /// </summary>
    public void EnterMaintenance(string? reason = null)
    {
        SelfAssertArgumentTrue(Status == DeploymentStatus.Running,
            "Can only enter maintenance on a running deployment.");
        SelfAssertArgumentTrue(OperationMode == OperationMode.Normal,
            "Deployment is already in maintenance mode.");

        OperationMode = OperationMode.Maintenance;
        AddDomainEvent(new OperationModeChanged(Id, OperationMode.Maintenance, reason));
    }

    /// <summary>
    /// Exits maintenance mode and returns to normal operation.
    /// </summary>
    public void ExitMaintenance()
    {
        SelfAssertArgumentTrue(Status == DeploymentStatus.Running,
            "Can only exit maintenance on a running deployment.");
        SelfAssertArgumentTrue(OperationMode == OperationMode.Maintenance,
            "Deployment is not in maintenance mode.");

        OperationMode = OperationMode.Normal;
        AddDomainEvent(new OperationModeChanged(Id, OperationMode.Normal, "Exited maintenance mode"));
    }

    #endregion

    #region Cancellation

    /// <summary>
    /// Requests cancellation of an in-progress deployment.
    /// </summary>
    public void RequestCancellation(string reason)
    {
        SelfAssertArgumentTrue(IsInProgress,
            "Can only cancel an in-progress deployment.");
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

    #region Upgrade & Rollback

    /// <summary>
    /// Gets the data needed to redeploy this deployment (for rollback after failed upgrade).
    /// The deployment already contains all necessary information: StackId, StackVersion, Variables.
    /// </summary>
    public (string StackId, string? StackVersion, IReadOnlyDictionary<string, string> Variables) GetRedeploymentData()
    {
        return (StackId, StackVersion, Variables);
    }

    /// <summary>
    /// Checks if rollback is possible.
    /// Rollback is available when the deployment is in Failed status (upgrade failed).
    /// Rollback simply redeploys the current version using existing deployment data.
    /// </summary>
    public bool CanRollback()
    {
        return Status == DeploymentStatus.Failed && !string.IsNullOrEmpty(StackVersion);
    }

    /// <summary>
    /// Gets the version that would be restored on rollback.
    /// This is the current StackVersion (which wasn't changed since upgrade failed before completion).
    /// </summary>
    public string? GetRollbackTargetVersion()
    {
        return CanRollback() ? StackVersion : null;
    }

    /// <summary>
    /// Records a successful upgrade event, storing the previous version for reference.
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

    #region Init Container Results

    /// <summary>
    /// Records the result of an init container execution.
    /// Called after each init container completes (success or failure).
    /// </summary>
    public void RecordInitContainerResult(string serviceName, bool success, int exitCode, string? logOutput = null)
    {
        SelfAssertArgumentNotEmpty(serviceName, "Service name is required.");

        _initContainerResults.Add(new InitContainerResult(serviceName, success, exitCode, SystemClock.UtcNow, logOutput));
    }

    /// <summary>
    /// Clears init container results (used during upgrade/rollback to replace old results).
    /// </summary>
    public void ClearInitContainerResults()
    {
        _initContainerResults.Clear();
    }

    #endregion

    #region Service Management

    /// <summary>
    /// Adds a service to the deployment.
    /// Called when a container is being created.
    /// </summary>
    public void AddService(string serviceName, string? image, string status)
    {
        SelfAssertArgumentNotEmpty(serviceName, "Service name is required.");
        SelfAssertArgumentNotEmpty(status, "Status is required.");

        var service = new DeployedService(serviceName, null, null, image, status);
        _services.Add(service);
    }

    /// <summary>
    /// Sets container info for a service after the container has been created.
    /// </summary>
    public void SetServiceContainerInfo(string serviceName, string containerId, string containerName, string status)
    {
        var service = _services.FirstOrDefault(s => s.ServiceName == serviceName);
        SelfAssertArgumentTrue(service != null, $"Service '{serviceName}' not found.");

        service!.UpdateContainerInfo(containerId, containerName);
        service.UpdateStatus(status);
    }

    /// <summary>
    /// Removes a service from the deployment.
    /// Called after a container has been stopped and removed.
    /// </summary>
    public void RemoveService(string serviceName)
    {
        var service = _services.FirstOrDefault(s => s.ServiceName == serviceName);
        if (service != null)
        {
            _services.Remove(service);
        }
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

    /// <summary>
    /// Marks all services as removed (containers no longer exist).
    /// Called when upgrade fails after containers have been removed.
    /// </summary>
    public void MarkAllServicesAsRemoved()
    {
        foreach (var service in _services)
        {
            var previousStatus = service.Status;
            service.UpdateStatus("removed");

            if (previousStatus != "removed")
            {
                AddDomainEvent(new ServiceStatusChanged(Id, service.ServiceName, previousStatus, "removed"));
            }
        }
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
        return IsInProgress && GetElapsedTime() > expectedDuration;
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
