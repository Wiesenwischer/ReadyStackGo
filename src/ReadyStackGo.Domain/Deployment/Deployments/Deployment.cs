namespace ReadyStackGo.Domain.Deployment.Deployments;

using ReadyStackGo.Domain.SharedKernel;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.Deployment.Environments;

/// <summary>
/// Aggregate root representing a stack deployment to an environment.
/// Rich domain model with state machine, progress tracking, and business rules.
/// </summary>
public class Deployment : AggregateRoot<DeploymentId>
{
    public EnvironmentId EnvironmentId { get; private set; } = null!;
    public string StackName { get; private set; } = null!;
    public string? StackVersion { get; private set; }
    public string ProjectName { get; private set; } = null!;
    public DeploymentStatus Status { get; private set; }
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

    private readonly List<DeployedService> _services = new();
    public IReadOnlyCollection<DeployedService> Services => _services.AsReadOnly();

    private readonly List<DeploymentPhaseRecord> _phaseHistory = new();
    public IReadOnlyCollection<DeploymentPhaseRecord> PhaseHistory => _phaseHistory.AsReadOnly();

    // For EF Core
    protected Deployment() { }

    private Deployment(
        DeploymentId id,
        EnvironmentId environmentId,
        string stackName,
        string projectName,
        UserId deployedBy)
    {
        SelfAssertArgumentNotNull(id, "DeploymentId is required.");
        SelfAssertArgumentNotNull(environmentId, "EnvironmentId is required.");
        SelfAssertArgumentNotEmpty(stackName, "Stack name is required.");
        SelfAssertArgumentNotEmpty(projectName, "Project name is required.");
        SelfAssertArgumentNotNull(deployedBy, "DeployedBy is required.");

        Id = id;
        EnvironmentId = environmentId;
        StackName = stackName;
        ProjectName = projectName;
        DeployedBy = deployedBy;
        Status = DeploymentStatus.Pending;
        CurrentPhase = DeploymentPhase.Initializing;
        ProgressPercentage = 0;
        CreatedAt = DateTime.UtcNow;

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
        string stackName,
        string projectName,
        UserId deployedBy)
    {
        return new Deployment(id, environmentId, stackName, projectName, deployedBy);
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
        _phaseHistory.Add(new DeploymentPhaseRecord(phase, message, DateTime.UtcNow));
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
        CompletedAt = DateTime.UtcNow;

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
        ErrorMessage = errorMessage;
        CompletedAt = DateTime.UtcNow;

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
        SelfAssertArgumentTrue(Status is DeploymentStatus.Running or DeploymentStatus.Stopped,
            "Can only remove a running or stopped deployment.");

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

    #region Service Management

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
        return (CompletedAt ?? DateTime.UtcNow) - CreatedAt;
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
