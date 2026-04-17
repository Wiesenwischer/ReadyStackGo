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
    /// When <paramref name="suppressNotifications"/> is true, the baseline is still updated
    /// but no notifications are emitted — used while a deployment is installing/upgrading so
    /// only the final per-product result surfaces to the user.
    /// </summary>
    Task ProcessHealthUpdateAsync(
        string deploymentId,
        string stackName,
        IReadOnlyList<ServiceHealthUpdate> serviceStatuses,
        bool suppressNotifications = false,
        CancellationToken ct = default);

    /// <summary>
    /// Clears all tracked baselines (previous status + cooldown) for a deployment.
    /// Called when a deployment leaves the Running state (Installing/Upgrading/Failed/Removed)
    /// so the next post-recovery health cycle starts from a clean baseline and does not
    /// emit spurious pre-upgrade → post-upgrade "status changed" notifications.
    /// </summary>
    Task ResetBaselineAsync(string deploymentId, CancellationToken ct = default);
}

/// <summary>
/// Represents a service's current health status for change detection.
/// </summary>
public record ServiceHealthUpdate(string ServiceName, string Status);
