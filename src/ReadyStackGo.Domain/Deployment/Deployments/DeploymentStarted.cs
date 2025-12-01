namespace ReadyStackGo.Domain.Deployment.Deployments;

using ReadyStackGo.Domain.SharedKernel;
using ReadyStackGo.Domain.Deployment.Environments;

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
