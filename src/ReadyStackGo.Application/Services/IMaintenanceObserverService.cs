using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Observers;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Service responsible for running maintenance observers and updating deployment operation modes.
/// </summary>
public interface IMaintenanceObserverService
{
    /// <summary>
    /// Check all configured maintenance observers across all deployments.
    /// Updates operation mode to Maintenance if observer indicates maintenance is required.
    /// </summary>
    Task CheckAllObserversAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check the maintenance observer for a specific deployment.
    /// </summary>
    /// <param name="deploymentId">The deployment to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The observer result, or null if no observer is configured.</returns>
    Task<ObserverResult?> CheckDeploymentObserverAsync(DeploymentId deploymentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the last observer result for a deployment.
    /// </summary>
    Task<ObserverResult?> GetLastResultAsync(DeploymentId deploymentId);
}
