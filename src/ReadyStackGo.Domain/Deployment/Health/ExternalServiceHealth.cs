namespace ReadyStackGo.Domain.Deployment.Health;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Health status of an external service dependency.
/// </summary>
public sealed class ExternalServiceHealth : ValueObject
{
    /// <summary>
    /// Service identifier (e.g., "smtp", "redis").
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Health status of the external service.
    /// </summary>
    public HealthStatus Status { get; }

    /// <summary>
    /// Error message if connection failed.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Response time in milliseconds.
    /// </summary>
    public int? ResponseTimeMs { get; }

    private ExternalServiceHealth(string id, HealthStatus status, string? error, int? responseTimeMs)
    {
        SelfAssertArgumentNotEmpty(id, "Service ID cannot be empty.");

        Id = id;
        Status = status;
        Error = error;
        ResponseTimeMs = responseTimeMs;
    }

    public static ExternalServiceHealth Create(string id, HealthStatus status, string? error = null, int? responseTimeMs = null)
    {
        return new ExternalServiceHealth(id, status, error, responseTimeMs);
    }

    public static ExternalServiceHealth Healthy(string id, int responseTimeMs)
    {
        return new ExternalServiceHealth(id, HealthStatus.Healthy, null, responseTimeMs);
    }

    public static ExternalServiceHealth Unhealthy(string id, string error)
    {
        return new ExternalServiceHealth(id, HealthStatus.Unhealthy, error, null);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Id;
        yield return Status;
        yield return Error;
        yield return ResponseTimeMs;
    }
}
