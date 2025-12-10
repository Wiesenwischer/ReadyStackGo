using ReadyStackGo.Application.UseCases.Deployments;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Callback delegate for reporting deployment progress from the service layer.
/// </summary>
public delegate Task DeploymentServiceProgressCallback(
    string phase,
    string message,
    int progressPercent,
    string? currentService,
    int totalServices,
    int completedServices);

/// <summary>
/// Service for managing Docker Compose stack deployments.
/// v0.4: Supports deploying stacks to specific environments.
/// v0.6.1: Added progress callback support for real-time updates.
/// </summary>
public interface IDeploymentService
{
    /// <summary>
    /// Parse a Docker Compose file and detect required variables.
    /// </summary>
    Task<ParseComposeResponse> ParseComposeAsync(ParseComposeRequest request);

    /// <summary>
    /// Deploy a Docker Compose stack to an environment.
    /// </summary>
    Task<DeployComposeResponse> DeployComposeAsync(string environmentId, DeployComposeRequest request);

    /// <summary>
    /// Deploy a Docker Compose stack to an environment with progress reporting.
    /// </summary>
    Task<DeployComposeResponse> DeployComposeAsync(
        string environmentId,
        DeployComposeRequest request,
        DeploymentServiceProgressCallback? progressCallback,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get deployment details for a stack.
    /// </summary>
    Task<GetDeploymentResponse> GetDeploymentAsync(string environmentId, string stackName);

    /// <summary>
    /// List all deployments in an environment.
    /// </summary>
    Task<ListDeploymentsResponse> ListDeploymentsAsync(string environmentId);

    /// <summary>
    /// Remove a deployed stack.
    /// </summary>
    Task<DeployComposeResponse> RemoveDeploymentAsync(string environmentId, string stackName);
}
