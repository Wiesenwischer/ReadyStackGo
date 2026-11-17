namespace ReadyStackGo.Application.Services.Edge;

/// <summary>
/// Keeps every product edge in sync with RSGO's authoritative deploy state and the
/// maintenance flag. Mirrors the maintenance-observer service: a background service drives
/// <see cref="ReconcileAllAsync"/> on a fixed interval.
///
/// For each active product deployment that has an <see cref="Domain.Deployment.Edge.EdgeConfig"/>:
/// idempotently ensures the edge container exists, computes the desired state, and pushes a
/// fresh Caddy config via the admin API — but only when the config actually changed
/// (connection-preserving, no needless reloads).
/// </summary>
public interface IEdgeReconciler
{
    /// <summary>Reconciles all active product deployments that have an edge configured.</summary>
    Task ReconcileAllAsync(CancellationToken cancellationToken = default);
}
