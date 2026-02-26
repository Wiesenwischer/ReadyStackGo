namespace ReadyStackGo.Domain.Deployment.Health;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Health status of a single service/container within a stack.
/// </summary>
public sealed class ServiceHealth : ValueObject
{
    /// <summary>
    /// Name of the service (e.g., "ams-api").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Health status from container/health endpoint perspective.
    /// </summary>
    public HealthStatus Status { get; }

    /// <summary>
    /// Docker container ID if available.
    /// </summary>
    public string? ContainerId { get; }

    /// <summary>
    /// Docker container name if available.
    /// </summary>
    public string? ContainerName { get; }

    /// <summary>
    /// Reason for current status (e.g., "Restarting", "CrashLoop", "HealthCheckFailed").
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Number of container restarts. Only loaded for unhealthy containers.
    /// Null means "not loaded" (healthy containers don't need this info).
    /// </summary>
    public int? RestartCount { get; }

    /// <summary>
    /// Parsed health check entries from the HTTP health endpoint response.
    /// Only populated when the service exposes an ASP.NET Core HealthReport.
    /// </summary>
    public IReadOnlyList<HealthCheckEntry>? HealthCheckEntries { get; }

    /// <summary>
    /// Response time of the HTTP health check in milliseconds.
    /// Only populated when the service was checked via HTTP health endpoint.
    /// </summary>
    public int? ResponseTimeMs { get; }

    private ServiceHealth(
        string name,
        HealthStatus status,
        string? containerId,
        string? containerName,
        string? reason,
        int? restartCount,
        IReadOnlyList<HealthCheckEntry>? healthCheckEntries = null,
        int? responseTimeMs = null)
    {
        SelfAssertArgumentNotEmpty(name, "Service name cannot be empty.");

        Name = name;
        Status = status;
        ContainerId = containerId;
        ContainerName = containerName;
        Reason = reason;
        RestartCount = restartCount;
        HealthCheckEntries = healthCheckEntries;
        ResponseTimeMs = responseTimeMs;
    }

    public static ServiceHealth Create(
        string name,
        HealthStatus status,
        string? containerId = null,
        string? containerName = null,
        string? reason = null,
        int? restartCount = null,
        IReadOnlyList<HealthCheckEntry>? healthCheckEntries = null,
        int? responseTimeMs = null)
    {
        return new ServiceHealth(name, status, containerId, containerName, reason, restartCount, healthCheckEntries, responseTimeMs);
    }

    public static ServiceHealth Healthy(string name, string? containerId = null, string? containerName = null)
    {
        return new ServiceHealth(name, HealthStatus.Healthy, containerId, containerName, null, restartCount: null);
    }

    public static ServiceHealth Unhealthy(string name, string reason, string? containerId = null, int? restartCount = null)
    {
        return new ServiceHealth(name, HealthStatus.Unhealthy, containerId, null, reason, restartCount);
    }

    public static ServiceHealth Unknown(string name)
    {
        return new ServiceHealth(name, HealthStatus.Unknown, null, null, "No data available", restartCount: null);
    }

    public static ServiceHealth NotFound(string name)
    {
        return new ServiceHealth(name, HealthStatus.NotFound, null, null, "Container not found", restartCount: null);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
        yield return Status;
        yield return ContainerId;
        yield return ContainerName;
        yield return Reason;
        yield return RestartCount;
        yield return ResponseTimeMs;
    }
}
