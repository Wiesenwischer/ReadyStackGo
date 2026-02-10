using ReadyStackGo.Application.Services;
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
/// <param name="totalServices">Total number of regular services to deploy</param>
/// <param name="completedServices">Number of regular services already deployed</param>
/// <param name="totalInitContainers">Total number of init containers</param>
/// <param name="completedInitContainers">Number of init containers already completed</param>
public delegate Task DeploymentProgressCallback(
    string phase,
    string message,
    int progressPercent,
    string? currentService,
    int totalServices,
    int completedServices,
    int totalInitContainers,
    int completedInitContainers);

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
        InitContainerLogCallback? logCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deploy a stack from a manifest (combines plan generation and execution)
    /// </summary>
    Task<DeploymentResult> DeployStackAsync(ReleaseManifest manifest);

    /// <summary>
    /// Remove all containers from a deployed stack
    /// </summary>
    Task<DeploymentResult> RemoveStackAsync(string environmentId, string stackVersion);

    /// <summary>
    /// Remove all containers from a deployed stack with progress reporting
    /// </summary>
    Task<DeploymentResult> RemoveStackAsync(string environmentId, string stackVersion, DeploymentProgressCallback? progressCallback);
}
