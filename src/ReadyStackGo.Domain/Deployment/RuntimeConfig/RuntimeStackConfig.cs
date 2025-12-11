namespace ReadyStackGo.Domain.Deployment.RuntimeConfig;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Entity representing runtime configuration for a deployed stack.
/// Contains maintenance observer settings, health check configurations, etc.
/// that are applied when a stack is deployed to an environment.
/// </summary>
public class RuntimeStackConfig : Entity<RuntimeStackConfigId>
{
    /// <summary>
    /// The stack ID this configuration applies to (format: sourceId:stackName).
    /// </summary>
    public string StackId { get; private set; } = null!;

    /// <summary>
    /// Maintenance observer configuration.
    /// Null if no maintenance observer is defined.
    /// </summary>
    public MaintenanceObserverConfig? MaintenanceObserver { get; private set; }

    /// <summary>
    /// Health check configurations for services.
    /// </summary>
    public IReadOnlyList<ServiceHealthCheckConfig> HealthChecks { get; private set; } = Array.Empty<ServiceHealthCheckConfig>();

    /// <summary>
    /// When this configuration was created.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// When this configuration was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    // For EF Core
    protected RuntimeStackConfig() { }

    private RuntimeStackConfig(RuntimeStackConfigId id, string stackId)
    {
        SelfAssertArgumentNotNull(id, "RuntimeStackConfigId is required.");
        SelfAssertArgumentNotEmpty(stackId, "StackId is required.");

        Id = id;
        StackId = stackId;
        CreatedAt = SystemClock.UtcNow;
        UpdatedAt = CreatedAt;
    }

    /// <summary>
    /// Creates a new runtime stack configuration.
    /// </summary>
    public static RuntimeStackConfig Create(
        RuntimeStackConfigId id,
        string stackId,
        MaintenanceObserverConfig? maintenanceObserver = null,
        IEnumerable<ServiceHealthCheckConfig>? healthChecks = null)
    {
        var config = new RuntimeStackConfig(id, stackId)
        {
            MaintenanceObserver = maintenanceObserver,
            HealthChecks = (healthChecks?.ToList() ?? new List<ServiceHealthCheckConfig>()).AsReadOnly()
        };

        return config;
    }

    /// <summary>
    /// Updates the maintenance observer configuration.
    /// </summary>
    public void UpdateMaintenanceObserver(MaintenanceObserverConfig? observer)
    {
        MaintenanceObserver = observer;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Updates the health check configurations.
    /// </summary>
    public void UpdateHealthChecks(IEnumerable<ServiceHealthCheckConfig> healthChecks)
    {
        HealthChecks = (healthChecks?.ToList() ?? new List<ServiceHealthCheckConfig>()).AsReadOnly();
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Gets the health check configuration for a specific service.
    /// </summary>
    public ServiceHealthCheckConfig? GetHealthCheckForService(string serviceName)
    {
        return HealthChecks.FirstOrDefault(hc =>
            hc.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns true if this configuration has a maintenance observer.
    /// </summary>
    public bool HasMaintenanceObserver => MaintenanceObserver != null;

    /// <summary>
    /// Returns true if this configuration has any health checks defined.
    /// </summary>
    public bool HasHealthChecks => HealthChecks.Count > 0;

    public override string ToString() =>
        $"RuntimeStackConfig [id={Id}, stackId={StackId}, hasObserver={HasMaintenanceObserver}, healthChecks={HealthChecks.Count}]";
}
