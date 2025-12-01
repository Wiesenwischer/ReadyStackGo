namespace ReadyStackGo.Domain.Deployment.Events;

using ReadyStackGo.Domain.Common;
using ReadyStackGo.Domain.Deployment.ValueObjects;

/// <summary>
/// Event raised when a deployment is started.
/// </summary>
public sealed class DeploymentStarted : DomainEvent
{
    public DeploymentId DeploymentId { get; }
    public EnvironmentId EnvironmentId { get; }
    public string StackName { get; }

    public DeploymentStarted(DeploymentId deploymentId, EnvironmentId environmentId, string stackName)
    {
        DeploymentId = deploymentId;
        EnvironmentId = environmentId;
        StackName = stackName;
    }
}
