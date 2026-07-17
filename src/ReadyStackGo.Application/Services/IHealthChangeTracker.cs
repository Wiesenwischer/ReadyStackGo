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
    /// Processes an aggregated health update for a whole product deployment. Compares the
    /// product's overall status against the previous cycle and emits a single product-level
    /// notification per transition (e.g. Healthy → Degraded, and later Degraded → Healthy).
    /// Throttling is direction-aware so a recovery is never swallowed by a preceding
    /// degradation. When <paramref name="suppressNotifications"/> is true the baseline still
    /// advances but nothing is emitted — used while the product is deploying/upgrading/
    /// removing/redeploying or in maintenance, where health churn is expected by design.
    /// </summary>
    Task ProcessProductHealthUpdateAsync(
        string productDeploymentId,
        string productDisplayName,
        string overallStatus,
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
