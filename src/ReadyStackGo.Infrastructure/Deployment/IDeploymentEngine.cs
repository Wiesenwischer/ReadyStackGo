using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Manifests;

namespace ReadyStackGo.Infrastructure.Deployment;

/// <summary>
/// Service for deploying stacks based on manifests
/// </summary>
public interface IDeploymentEngine
{
    /// <summary>
    /// Generate a deployment plan from a manifest
    /// </summary>
    Task<DeploymentPlan> GenerateDeploymentPlanAsync(ReleaseManifest manifest);

    /// <summary>
    /// Execute a deployment plan
    /// </summary>
    Task<DeploymentResult> ExecuteDeploymentAsync(DeploymentPlan plan);

    /// <summary>
    /// Deploy a stack from a manifest (combines plan generation and execution)
    /// </summary>
    Task<DeploymentResult> DeployStackAsync(ReleaseManifest manifest);

    /// <summary>
    /// Remove all containers from a deployed stack
    /// </summary>
    Task<DeploymentResult> RemoveStackAsync(string stackVersion);
}
