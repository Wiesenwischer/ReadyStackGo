using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Application.UseCases.Deployments.DeployStack;

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
    int completedServices,
    int totalInitContainers,
    int completedInitContainers);

/// <summary>
/// Callback delegate for streaming init container log lines.
/// </summary>
/// <param name="containerName">Name of the init container producing the log.</param>
/// <param name="logLine">A single log line from the container.</param>
public delegate Task InitContainerLogCallback(string containerName, string logLine);

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
        InitContainerLogCallback? logCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get deployment details for a stack by stack name.
    /// </summary>
    Task<GetDeploymentResponse> GetDeploymentAsync(string environmentId, string stackName);

    /// <summary>
    /// Get deployment details by deployment ID.
    /// </summary>
    Task<GetDeploymentResponse> GetDeploymentByIdAsync(string environmentId, string deploymentId);

    /// <summary>
    /// List all deployments in an environment.
    /// </summary>
    Task<ListDeploymentsResponse> ListDeploymentsAsync(string environmentId);

    /// <summary>
    /// Remove a deployed stack by stack name.
    /// </summary>
    Task<DeployComposeResponse> RemoveDeploymentAsync(string environmentId, string stackName);

    /// <summary>
    /// Remove a deployed stack by deployment ID.
    /// </summary>
    Task<DeployComposeResponse> RemoveDeploymentByIdAsync(string environmentId, string deploymentId);

    /// <summary>
    /// Remove a deployed stack by deployment ID with progress reporting.
    /// </summary>
    Task<DeployComposeResponse> RemoveDeploymentByIdAsync(
        string environmentId,
        string deploymentId,
        DeploymentServiceProgressCallback? progressCallback,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deploy a stack from a catalog definition with progress reporting.
    /// The stack definition data (Services, Volumes, Networks) is passed as structured DTOs.
    /// No YAML parsing is needed - data is already in structured format from catalog.
    /// </summary>
    Task<DeployStackResponse> DeployStackAsync(
        string? environmentId,
        DeployStackRequest request,
        DeploymentServiceProgressCallback? progressCallback,
        InitContainerLogCallback? logCallback = null,
        CancellationToken cancellationToken = default);
}
