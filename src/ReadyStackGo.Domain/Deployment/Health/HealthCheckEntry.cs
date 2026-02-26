namespace ReadyStackGo.Domain.Deployment.Health;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// A single health check entry from an ASP.NET Core HealthReport.
/// Represents an individual check (e.g., "database", "redis", "disk") with its result.
/// </summary>
public sealed class HealthCheckEntry : ValueObject
{
    /// <summary>
    /// Name of the health check (e.g., "database", "redis", "disk").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Health status of this check.
    /// </summary>
    public HealthStatus Status { get; }

    /// <summary>
    /// Optional description from the health check result.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Duration of the health check in milliseconds.
    /// </summary>
    public double? DurationMs { get; }

    /// <summary>
    /// Additional key-value data reported by the health check.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Data { get; }

    /// <summary>
    /// Tags associated with the health check.
    /// </summary>
    public IReadOnlyList<string>? Tags { get; }

    /// <summary>
    /// Exception message if the health check threw an exception.
    /// </summary>
    public string? Exception { get; }

    private HealthCheckEntry(
        string name,
        HealthStatus status,
        string? description,
        double? durationMs,
        IReadOnlyDictionary<string, string>? data,
        IReadOnlyList<string>? tags,
        string? exception)
    {
        SelfAssertArgumentNotEmpty(name, "Health check entry name cannot be empty.");

        Name = name;
        Status = status;
        Description = description;
        DurationMs = durationMs;
        Data = data;
        Tags = tags;
        Exception = exception;
    }

    public static HealthCheckEntry Create(
        string name,
        HealthStatus status,
        string? description = null,
        double? durationMs = null,
        IReadOnlyDictionary<string, string>? data = null,
        IReadOnlyList<string>? tags = null,
        string? exception = null)
    {
        return new HealthCheckEntry(name, status, description, durationMs, data, tags, exception);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
        yield return Status;
        yield return Description;
        yield return DurationMs;
        yield return Exception;
    }
}
