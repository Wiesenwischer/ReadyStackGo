using System.Collections.Concurrent;

namespace ReadyStackGo.Application.Services.Edge;

/// <summary>
/// Singleton in-memory cache of the last Caddy config successfully pushed to each product
/// edge. Lets the (scoped, per-cycle) reconciler detect "no change" across reconcile cycles
/// so it only reloads the edge when the desired config actually changes — keeping reloads
/// connection-preserving and the logs quiet.
/// </summary>
public interface IEdgeConfigCache
{
    /// <summary>Returns the last pushed config for a deployment, or null if none recorded.</summary>
    string? Get(Guid productDeploymentId);

    /// <summary>Records the config most recently pushed successfully for a deployment.</summary>
    void Set(Guid productDeploymentId, string configJson);
}

/// <summary>Default <see cref="IEdgeConfigCache"/> backed by a concurrent dictionary.</summary>
public class EdgeConfigCache : IEdgeConfigCache
{
    private readonly ConcurrentDictionary<Guid, string> _lastPushed = new();

    public string? Get(Guid productDeploymentId)
        => _lastPushed.TryGetValue(productDeploymentId, out var v) ? v : null;

    public void Set(Guid productDeploymentId, string configJson)
        => _lastPushed[productDeploymentId] = configJson;
}
