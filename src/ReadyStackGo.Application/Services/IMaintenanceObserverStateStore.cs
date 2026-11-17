using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Cross-scope state of the maintenance observers: the observer instances, their last result and
/// the time of their last check.
///
/// This state has to outlive a single check cycle. <see cref="IMaintenanceObserverService"/> is
/// scoped — it depends on repositories and the mediator — and the background service creates a
/// fresh scope per cycle, so state kept on the service itself is discarded every time. That made the
/// configured pollingInterval ineffective (every cycle looked like a first check) and left the
/// observer-status endpoint permanently empty. Mirrors IHealthChangeTracker and IEdgeConfigCache.
/// </summary>
public interface IMaintenanceObserverStateStore
{
    /// <summary>
    /// Returns the cached observer for the product, creating it via <paramref name="create"/> when
    /// absent. A configuration that differs from the cached one replaces the observer, so manifest
    /// changes picked up by a redeploy take effect without restarting RSGO.
    /// </summary>
    IMaintenanceObserver GetOrCreateObserver(
        ProductDeploymentId productDeploymentId,
        MaintenanceObserverConfig config,
        Func<MaintenanceObserverConfig, IMaintenanceObserver> create);

    /// <summary>The configuration the cached observer was created from, if any.</summary>
    MaintenanceObserverConfig? GetConfig(ProductDeploymentId productDeploymentId);

    /// <summary>Whether <paramref name="interval"/> has elapsed since the last recorded check.</summary>
    bool ShouldCheck(ProductDeploymentId productDeploymentId, TimeSpan interval);

    /// <summary>Records a completed check and its result.</summary>
    void RecordResult(ProductDeploymentId productDeploymentId, ObserverResult result);

    /// <summary>The last recorded result, or null if the product was never checked.</summary>
    ObserverResult? GetLastResult(ProductDeploymentId productDeploymentId);

    /// <summary>Drops all state for a product (no longer deployed, or no longer observed).</summary>
    void Forget(ProductDeploymentId productDeploymentId);
}
