using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Infrastructure.Parsing;

namespace ReadyStackGo.Infrastructure.Services.Deployment;

/// <summary>
/// Callback delegate for reporting deployment progress.
/// </summary>
/// <param name="phase">Current deployment phase (e.g., "Pulling", "Starting", "Configuring")</param>
/// <param name="message">Human-readable progress message</param>
/// <param name="progressPercent">Progress percentage (0-100)</param>
/// <param name="currentService">Currently processing service name</param>
/// <param name="totalServices">Total number of services to deploy</param>
/// <param name="completedServices">Number of services already deployed</param>
public delegate Task DeploymentProgressCallback(
    string phase,
    string message,
    int progressPercent,
    string? currentService,
    int totalServices,
    int completedServices);

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
    /// Execute a deployment plan with progress reporting
    /// </summary>
    Task<DeploymentResult> ExecuteDeploymentAsync(
        DeploymentPlan plan,
        DeploymentProgressCallback? progressCallback,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deploy a stack from a manifest (combines plan generation and execution)
    /// </summary>
    Task<DeploymentResult> DeployStackAsync(ReleaseManifest manifest);

    /// <summary>
    /// Remove all containers from a deployed stack
    /// </summary>
    Task<DeploymentResult> RemoveStackAsync(string environmentId, string stackVersion);
}
