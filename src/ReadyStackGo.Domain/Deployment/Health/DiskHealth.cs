namespace ReadyStackGo.Domain.Deployment.Health;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Health status of a disk/volume mount.
/// </summary>
public sealed class DiskHealth : ValueObject
{
    /// <summary>
    /// Mount point (e.g., "/", "/data").
    /// </summary>
    public string Mount { get; }

    /// <summary>
    /// Health status based on available space.
    /// </summary>
    public HealthStatus Status { get; }

    /// <summary>
    /// Percentage of free disk space.
    /// </summary>
    public double? FreePercent { get; }

    /// <summary>
    /// Error message if check failed.
    /// </summary>
    public string? Error { get; }

    private DiskHealth(string mount, HealthStatus status, double? freePercent, string? error)
    {
        SelfAssertArgumentNotEmpty(mount, "Mount point cannot be empty.");

        Mount = mount;
        Status = status;
        FreePercent = freePercent;
        Error = error;
    }

    public static DiskHealth Create(string mount, HealthStatus status, double? freePercent = null, string? error = null)
    {
        return new DiskHealth(mount, status, freePercent, error);
    }

    public static DiskHealth Healthy(string mount, double freePercent)
    {
        return new DiskHealth(mount, HealthStatus.Healthy, freePercent, null);
    }

    public static DiskHealth Warning(string mount, double freePercent)
    {
        return new DiskHealth(mount, HealthStatus.Degraded, freePercent, $"Low disk space: {freePercent:F1}% free");
    }

    public static DiskHealth Critical(string mount, double freePercent)
    {
        return new DiskHealth(mount, HealthStatus.Unhealthy, freePercent, $"Critical disk space: {freePercent:F1}% free");
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Mount;
        yield return Status;
        yield return FreePercent;
        yield return Error;
    }
}
