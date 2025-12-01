namespace ReadyStackGo.Domain.Deployment.Events;

using ReadyStackGo.Domain.Common;
using ReadyStackGo.Domain.Deployment.ValueObjects;

/// <summary>
/// Event raised when a deployment is completed (running or failed).
/// </summary>
public sealed class DeploymentCompleted : DomainEvent
{
    public DeploymentId DeploymentId { get; }
    public DeploymentStatus Status { get; }
    public string? ErrorMessage { get; }

    public DeploymentCompleted(DeploymentId deploymentId, DeploymentStatus status, string? errorMessage = null)
    {
        DeploymentId = deploymentId;
        Status = status;
        ErrorMessage = errorMessage;
    }
}
