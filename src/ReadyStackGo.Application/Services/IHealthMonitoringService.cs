using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Service for monitoring health of deployed stacks.
/// </summary>
public interface IHealthMonitoringService
{
    /// <summary>
    /// Captures a health snapshot for a deployment by collecting
    /// container status, health checks, and other metrics.
    /// </summary>
    /// <param name="organizationId">Organization ID</param>
    /// <param name="environmentId">Environment ID</param>
    /// <param name="deploymentId">Deployment ID</param>
    /// <param name="stackName">Stack name for container filtering</param>
    /// <param name="currentVersion">Current stack version</param>
    /// <param name="serviceHealthConfigs">Optional HTTP health check configs per service name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<HealthSnapshot> CaptureHealthSnapshotAsync(
        OrganizationId organizationId,
        EnvironmentId environmentId,
        DeploymentId deploymentId,
        string stackName,
        string? currentVersion = null,
        IReadOnlyDictionary<string, ServiceHealthCheckConfig>? serviceHealthConfigs = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest health snapshot for a deployment.
    /// </summary>
    Task<HealthSnapshot?> GetLatestHealthSnapshotAsync(
        DeploymentId deploymentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets health summaries for all deployments in an environment.
    /// </summary>
    Task<IEnumerable<HealthSnapshot>> GetEnvironmentHealthSummaryAsync(
        EnvironmentId environmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets health history for a deployment.
    /// </summary>
    Task<IEnumerable<HealthSnapshot>> GetHealthHistoryAsync(
        DeploymentId deploymentId,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the operation mode for a deployment.
    /// </summary>
    Task UpdateOperationModeAsync(
        DeploymentId deploymentId,
        OperationMode newMode,
        string? targetVersion = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO for health summary in lists.
/// </summary>
public record HealthSummaryDto
{
    public required string DeploymentId { get; init; }
    public required string StackName { get; init; }
    public required string OverallStatus { get; init; }
    public required string OperationMode { get; init; }
    public required string? CurrentVersion { get; init; }
    public required string? TargetVersion { get; init; }
    public required int HealthyServices { get; init; }
    public required int TotalServices { get; init; }
    public required string StatusMessage { get; init; }
    public required DateTime CapturedAtUtc { get; init; }

    // UI helper properties from domain
    public required string OverallStatusColor { get; init; }
    public required string OverallStatusIcon { get; init; }
    public required string OperationModeColor { get; init; }
    public required string OperationModeIcon { get; init; }
    public required bool RequiresAttention { get; init; }
}

/// <summary>
/// Configuration for HTTP health checks per service.
/// Used to enable RSGO to call ASP.NET Core /hc endpoints directly.
/// </summary>
public record ServiceHealthCheckConfig
{
    /// <summary>
    /// Health check type: "docker" (default), "http", "tcp", or "none".
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// HTTP path for health endpoint (e.g., "/hc").
    /// </summary>
    public string Path { get; init; } = "/hc";

    /// <summary>
    /// Port for HTTP/TCP health checks. If null, uses first exposed port.
    /// </summary>
    public int? Port { get; init; }

    /// <summary>
    /// Timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 5;

    /// <summary>
    /// Whether to use HTTPS.
    /// </summary>
    public bool UseHttps { get; init; } = false;

    /// <summary>
    /// Expected HTTP status codes for healthy response.
    /// </summary>
    public IReadOnlyList<int> ExpectedStatusCodes { get; init; } = new[] { 200 };

    /// <summary>
    /// Creates a config for HTTP health checks.
    /// </summary>
    public static ServiceHealthCheckConfig Http(
        string path = "/hc",
        int? port = null,
        int timeoutSeconds = 5,
        bool useHttps = false) =>
        new()
        {
            Type = "http",
            Path = path,
            Port = port,
            TimeoutSeconds = timeoutSeconds,
            UseHttps = useHttps
        };

    /// <summary>
    /// Creates a config for Docker HEALTHCHECK (default behavior).
    /// </summary>
    public static ServiceHealthCheckConfig Docker() =>
        new() { Type = "docker" };

    /// <summary>
    /// Creates a config to disable health checks.
    /// </summary>
    public static ServiceHealthCheckConfig None() =>
        new() { Type = "none" };

    /// <summary>
    /// Returns true if this is an HTTP health check.
    /// </summary>
    public bool IsHttp => Type.Equals("http", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true if health checks are disabled.
    /// </summary>
    public bool IsDisabled => Type.Equals("none", StringComparison.OrdinalIgnoreCase);
}
