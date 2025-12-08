namespace ReadyStackGo.Domain.Deployment.Health;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Health status of all services/containers controlled by a stack.
/// </summary>
public sealed class SelfHealth : ValueObject
{
    private readonly List<ServiceHealth> _services;

    /// <summary>
    /// Health status of individual services.
    /// </summary>
    public IReadOnlyList<ServiceHealth> Services => _services.AsReadOnly();

    /// <summary>
    /// Aggregated health status based on all services.
    /// </summary>
    public HealthStatus Status { get; }

    /// <summary>
    /// Number of healthy services.
    /// </summary>
    public int HealthyCount => _services.Count(s => s.Status == HealthStatus.Healthy);

    /// <summary>
    /// Total number of services.
    /// </summary>
    public int TotalCount => _services.Count;

    private SelfHealth(IEnumerable<ServiceHealth> services)
    {
        _services = services.ToList();
        Status = CalculateAggregatedStatus();
    }

    public static SelfHealth Create(IEnumerable<ServiceHealth> services)
    {
        return new SelfHealth(services);
    }

    public static SelfHealth Empty()
    {
        return new SelfHealth(Enumerable.Empty<ServiceHealth>());
    }

    private HealthStatus CalculateAggregatedStatus()
    {
        if (!_services.Any())
            return HealthStatus.Unknown;

        if (_services.All(s => s.Status == HealthStatus.Healthy))
            return HealthStatus.Healthy;

        if (_services.Any(s => s.Status == HealthStatus.Unhealthy))
            return HealthStatus.Unhealthy;

        if (_services.Any(s => s.Status == HealthStatus.Degraded))
            return HealthStatus.Degraded;

        return HealthStatus.Unknown;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Status;
        foreach (var service in _services.OrderBy(s => s.Name))
        {
            yield return service;
        }
    }
}
