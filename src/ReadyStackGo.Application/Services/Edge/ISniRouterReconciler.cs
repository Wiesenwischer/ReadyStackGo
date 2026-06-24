namespace ReadyStackGo.Application.Services.Edge;

/// <summary>
/// Reconciles the optional shared host-level SNI passthrough router (Phase 4). When the feature
/// is disabled this is a no-op, so per-product edges are unaffected.
/// </summary>
public interface ISniRouterReconciler
{
    Task ReconcileAsync(CancellationToken cancellationToken = default);
}
