using System.Collections.Concurrent;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.Services.Impl;

/// <summary>
/// In-memory implementation of <see cref="IMaintenanceObserverStateStore"/>. Registered as a
/// singleton; every member is safe to call from concurrent check cycles.
/// </summary>
public sealed class MaintenanceObserverStateStore : IMaintenanceObserverStateStore
{
    private sealed class Entry
    {
        public required MaintenanceObserverConfig Config { get; init; }
        public required IMaintenanceObserver Observer { get; init; }
        public ObserverResult? LastResult { get; set; }
        public DateTimeOffset? LastCheckedAt { get; set; }
    }

    private readonly ConcurrentDictionary<Guid, Entry> _entries = new();

    public IMaintenanceObserver GetOrCreateObserver(
        ProductDeploymentId productDeploymentId,
        MaintenanceObserverConfig config,
        Func<MaintenanceObserverConfig, IMaintenanceObserver> create)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(create);

        var key = productDeploymentId.Value;

        if (_entries.TryGetValue(key, out var existing) && existing.Config.Equals(config))
        {
            return existing.Observer;
        }

        // Either first use, or the configuration changed (redeploy/upgrade with an edited manifest).
        // A changed config invalidates the cached observer *and* the last result, which was produced
        // by the previous configuration — but not the check timestamp, so a config change cannot be
        // used to poll a product database more often than its interval allows.
        var replacement = new Entry
        {
            Config = config,
            Observer = create(config),
            LastCheckedAt = existing?.LastCheckedAt
        };

        _entries[key] = replacement;
        return replacement.Observer;
    }

    public MaintenanceObserverConfig? GetConfig(ProductDeploymentId productDeploymentId)
        => _entries.TryGetValue(productDeploymentId.Value, out var entry) ? entry.Config : null;

    public bool ShouldCheck(ProductDeploymentId productDeploymentId, TimeSpan interval)
    {
        if (!_entries.TryGetValue(productDeploymentId.Value, out var entry) || entry.LastCheckedAt == null)
        {
            return true;
        }

        return DateTimeOffset.UtcNow - entry.LastCheckedAt.Value >= interval;
    }

    public void RecordResult(ProductDeploymentId productDeploymentId, ObserverResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!_entries.TryGetValue(productDeploymentId.Value, out var entry))
        {
            // No observer instance for this product — nothing to attach the result to. Callers only
            // record results for observers they obtained from this store, so this is unreachable in
            // practice; ignoring it keeps the store from resurrecting forgotten products.
            return;
        }

        entry.LastResult = result;
        entry.LastCheckedAt = DateTimeOffset.UtcNow;
    }

    public ObserverResult? GetLastResult(ProductDeploymentId productDeploymentId)
        => _entries.TryGetValue(productDeploymentId.Value, out var entry) ? entry.LastResult : null;

    public void Forget(ProductDeploymentId productDeploymentId)
        => _entries.TryRemove(productDeploymentId.Value, out _);
}
