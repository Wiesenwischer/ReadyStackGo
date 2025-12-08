namespace ReadyStackGo.Domain.Deployment.Health;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Health status of a database connection.
/// </summary>
public sealed class DatabaseHealth : ValueObject
{
    /// <summary>
    /// Database identifier (e.g., "ams_project_db").
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Health status of the database connection.
    /// </summary>
    public HealthStatus Status { get; }

    /// <summary>
    /// Connection latency in milliseconds.
    /// </summary>
    public int? LatencyMs { get; }

    /// <summary>
    /// Error message if connection failed.
    /// </summary>
    public string? Error { get; }

    private DatabaseHealth(string id, HealthStatus status, int? latencyMs, string? error)
    {
        SelfAssertArgumentNotEmpty(id, "Database ID cannot be empty.");

        Id = id;
        Status = status;
        LatencyMs = latencyMs;
        Error = error;
    }

    public static DatabaseHealth Create(string id, HealthStatus status, int? latencyMs = null, string? error = null)
    {
        return new DatabaseHealth(id, status, latencyMs, error);
    }

    public static DatabaseHealth Healthy(string id, int latencyMs)
    {
        return new DatabaseHealth(id, HealthStatus.Healthy, latencyMs, null);
    }

    public static DatabaseHealth Unhealthy(string id, string error)
    {
        return new DatabaseHealth(id, HealthStatus.Unhealthy, null, error);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Id;
        yield return Status;
        yield return LatencyMs;
        yield return Error;
    }
}
