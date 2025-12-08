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
    /// Number of container restarts.
    /// </summary>
    public int RestartCount { get; }

    private ServiceHealth(
        string name,
        HealthStatus status,
        string? containerId,
        string? containerName,
        string? reason,
        int restartCount)
    {
        SelfAssertArgumentNotEmpty(name, "Service name cannot be empty.");
        SelfAssertArgumentTrue(restartCount >= 0, "Restart count cannot be negative.");

        Name = name;
        Status = status;
        ContainerId = containerId;
        ContainerName = containerName;
        Reason = reason;
        RestartCount = restartCount;
    }

    public static ServiceHealth Create(
        string name,
        HealthStatus status,
        string? containerId = null,
        string? containerName = null,
        string? reason = null,
        int restartCount = 0)
    {
        return new ServiceHealth(name, status, containerId, containerName, reason, restartCount);
    }

    public static ServiceHealth Healthy(string name, string? containerId = null, string? containerName = null)
    {
        return new ServiceHealth(name, HealthStatus.Healthy, containerId, containerName, null, 0);
    }

    public static ServiceHealth Unhealthy(string name, string reason, string? containerId = null, int restartCount = 0)
    {
        return new ServiceHealth(name, HealthStatus.Unhealthy, containerId, null, reason, restartCount);
    }

    public static ServiceHealth Unknown(string name)
    {
        return new ServiceHealth(name, HealthStatus.Unknown, null, null, "No data available", 0);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
        yield return Status;
        yield return ContainerId;
        yield return ContainerName;
        yield return Reason;
        yield return RestartCount;
    }
}
