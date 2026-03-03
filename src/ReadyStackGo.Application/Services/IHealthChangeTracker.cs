namespace ReadyStackGo.Application.Services;

/// <summary>
/// Tracks health status changes across collection cycles and creates
/// in-app notifications with throttling to prevent spam from flapping services.
/// </summary>
public interface IHealthChangeTracker
{
    /// <summary>
    /// Processes a health update for a deployment's services.
    /// Compares current status against previous status and creates notifications for changes.
    /// </summary>
    Task ProcessHealthUpdateAsync(
        string deploymentId,
        string stackName,
        IReadOnlyList<ServiceHealthUpdate> serviceStatuses,
        CancellationToken ct = default);
}

/// <summary>
/// Represents a service's current health status for change detection.
/// </summary>
public record ServiceHealthUpdate(string ServiceName, string Status);
