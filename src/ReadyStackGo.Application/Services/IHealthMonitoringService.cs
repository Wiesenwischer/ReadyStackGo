using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.IdentityAccess.Organizations;

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
    Task<HealthSnapshot> CaptureHealthSnapshotAsync(
        OrganizationId organizationId,
        EnvironmentId environmentId,
        DeploymentId deploymentId,
        string stackName,
        string? currentVersion = null,
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
