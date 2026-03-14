using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Service responsible for running maintenance observers and updating operation modes.
/// Observers are configured at the ProductDeployment level, producing one check per product
/// instead of N duplicate checks per stack.
/// </summary>
public interface IMaintenanceObserverService
{
    /// <summary>
    /// Check all configured maintenance observers across all product deployments.
    /// Updates operation mode to Maintenance if observer indicates maintenance is required.
    /// </summary>
    Task CheckAllObserversAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check the maintenance observer for a specific product deployment.
    /// </summary>
    Task<ObserverResult?> CheckProductObserverAsync(ProductDeploymentId productDeploymentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the last observer result for a product deployment.
    /// </summary>
    Task<ObserverResult?> GetLastResultAsync(ProductDeploymentId productDeploymentId);

    /// <summary>
    /// Check the maintenance observer for a specific deployment (legacy, looks up parent product).
    /// </summary>
    Task<ObserverResult?> CheckDeploymentObserverAsync(DeploymentId deploymentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the last observer result for a deployment (legacy, looks up parent product).
    /// </summary>
    Task<ObserverResult?> GetLastResultAsync(DeploymentId deploymentId);
}
