using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.Services.Edge;

/// <summary>
/// Applies edge settings that can only be set when the edge container is <em>created</em>
/// (currently the client-facing MSS tuning: namespaced sysctls / the edge network MTU).
///
/// Called from the redeploy and upgrade use cases — the two operations where the operator
/// explicitly asks RSGO to re-apply the product's manifest — because recreating the container
/// briefly takes the product's front door down. The background edge reconciler deliberately
/// leaves an existing container alone.
/// </summary>
public interface IEdgeSettingsReconciler
{
    /// <summary>
    /// Recreates the product's edge container when its live create-time settings drifted from
    /// <see cref="ProductDeployment.EdgeConfig"/>, and invalidates the cached Caddy config so
    /// the reconciler pushes the live config to the fresh container.
    ///
    /// A no-op when the product has no edge configured. Never throws: a failure to re-apply a
    /// tuning value must not fail the surrounding redeploy/upgrade.
    /// </summary>
    /// <returns><c>true</c> when the edge container was recreated.</returns>
    Task<bool> ApplyCreateTimeSettingsAsync(
        ProductDeployment productDeployment,
        CancellationToken cancellationToken = default);
}
