namespace ReadyStackGo.Domain.Deployment.Deployments;

using ReadyStackGo.Domain.SharedKernel;
using ReadyStackGo.Domain.Deployment.Health;

/// <summary>
/// Event raised when deployment progress is updated.
/// </summary>
public sealed class DeploymentProgressUpdated : DomainEvent
{
    public DeploymentId DeploymentId { get; }
    public DeploymentPhase Phase { get; }
    public int ProgressPercentage { get; }
    public string Message { get; }

    public DeploymentProgressUpdated(
        DeploymentId deploymentId,
        DeploymentPhase phase,
        int progressPercentage,
        string message)
    {
        DeploymentId = deploymentId;
        Phase = phase;
        ProgressPercentage = progressPercentage;
        Message = message;
    }
}

/// <summary>
/// Event raised when a deployment is stopped.
/// </summary>
public sealed class DeploymentStopped : DomainEvent
{
    public DeploymentId DeploymentId { get; }

    public DeploymentStopped(DeploymentId deploymentId)
    {
        DeploymentId = deploymentId;
    }
}

/// <summary>
/// Event raised when a deployment is restarted.
/// </summary>
public sealed class DeploymentRestarted : DomainEvent
{
    public DeploymentId DeploymentId { get; }

    public DeploymentRestarted(DeploymentId deploymentId)
    {
        DeploymentId = deploymentId;
    }
}

/// <summary>
/// Event raised when a deployment is removed.
/// </summary>
public sealed class DeploymentRemoved : DomainEvent
{
    public DeploymentId DeploymentId { get; }

    public DeploymentRemoved(DeploymentId deploymentId)
    {
        DeploymentId = deploymentId;
    }
}

/// <summary>
/// Event raised when cancellation is requested for a deployment.
/// </summary>
public sealed class DeploymentCancellationRequested : DomainEvent
{
    public DeploymentId DeploymentId { get; }
    public string Reason { get; }

    public DeploymentCancellationRequested(DeploymentId deploymentId, string reason)
    {
        DeploymentId = deploymentId;
        Reason = reason;
    }
}

/// <summary>
/// Event raised when a service's status changes within a deployment.
/// </summary>
public sealed class ServiceStatusChanged : DomainEvent
{
    public DeploymentId DeploymentId { get; }
    public string ServiceName { get; }
    public string PreviousStatus { get; }
    public string NewStatus { get; }

    public ServiceStatusChanged(
        DeploymentId deploymentId,
        string serviceName,
        string previousStatus,
        string newStatus)
    {
        DeploymentId = deploymentId;
        ServiceName = serviceName;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
    }
}

/// <summary>
/// Event raised when the operation mode of a deployment changes.
/// </summary>
public sealed class OperationModeChanged : DomainEvent
{
    public DeploymentId DeploymentId { get; }
    public OperationMode NewMode { get; }
    public string? Reason { get; }

    public OperationModeChanged(
        DeploymentId deploymentId,
        OperationMode newMode,
        string? reason = null)
    {
        DeploymentId = deploymentId;
        NewMode = newMode;
        Reason = reason;
    }
}

/// <summary>
/// Event raised when a deployment rollback is initiated.
/// </summary>
public sealed class DeploymentRollbackInitiated : DomainEvent
{
    public DeploymentId DeploymentId { get; }
    public DeploymentSnapshotId SnapshotId { get; }
    public string TargetVersion { get; }

    public DeploymentRollbackInitiated(
        DeploymentId deploymentId,
        DeploymentSnapshotId snapshotId,
        string targetVersion)
    {
        DeploymentId = deploymentId;
        SnapshotId = snapshotId;
        TargetVersion = targetVersion;
    }
}

/// <summary>
/// Event raised when a deployment snapshot is created.
/// </summary>
public sealed class DeploymentSnapshotCreated : DomainEvent
{
    public DeploymentId DeploymentId { get; }
    public DeploymentSnapshotId SnapshotId { get; }
    public string StackVersion { get; }

    public DeploymentSnapshotCreated(
        DeploymentId deploymentId,
        DeploymentSnapshotId snapshotId,
        string stackVersion)
    {
        DeploymentId = deploymentId;
        SnapshotId = snapshotId;
        StackVersion = stackVersion;
    }
}
