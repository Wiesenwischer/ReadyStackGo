using ReadyStackGo.Domain.Deployment.Edge;

namespace ReadyStackGo.Application.Services.Edge;

/// <summary>
/// Manages the lifecycle of a product's managed edge (Caddy) container.
/// Implemented in the infrastructure layer (Docker).
/// </summary>
public interface IEdgeProvisioner
{
    /// <summary>
    /// Idempotently ensures the edge container for the given product deployment exists and
    /// is running: ensures the shared edge network and management network exist, pulls the
    /// Caddy image if needed, and creates the container with the edge survival labels and
    /// an admin-API-reachable bootstrap config. If a healthy edge container already exists,
    /// it is reused unchanged.
    /// </summary>
    /// <returns>The Caddy admin API base URL the reconciler can push config to.</returns>
    Task<string> EnsureEdgeAsync(
        string environmentId,
        string deploymentName,
        string productGroupId,
        EdgeConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Idempotently ensures the optional shared host-level SNI passthrough router exists and is
    /// running (Phase 4). Survivor-scoped, attached to the management network, host-binds the
    /// configured listen port, and boots with an admin-reachable empty Layer-4 config that the
    /// reconciler then fills in.
    /// </summary>
    /// <returns>The router's Caddy admin API base URL.</returns>
    Task<string> EnsureSniRouterAsync(
        string environmentId,
        SniRouterOptions options,
        CancellationToken cancellationToken = default);
}
