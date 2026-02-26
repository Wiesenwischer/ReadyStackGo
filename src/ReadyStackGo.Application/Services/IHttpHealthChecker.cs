namespace ReadyStackGo.Application.Services;

/// <summary>
/// Service for checking HTTP health endpoints of containers.
/// Used when containers expose ASP.NET Core /hc endpoints instead of Docker HEALTHCHECK.
/// </summary>
public interface IHttpHealthChecker
{
    /// <summary>
    /// Checks the health of a container by calling its HTTP health endpoint.
    /// </summary>
    /// <param name="containerAddress">The container's network address (hostname or IP)</param>
    /// <param name="config">Health check configuration from the manifest</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the health check</returns>
    Task<HttpHealthCheckResult> CheckHealthAsync(
        string containerAddress,
        HttpHealthCheckConfig config,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration for HTTP health checks (passed from manifest).
/// </summary>
public record HttpHealthCheckConfig
{
    /// <summary>
    /// URL path to health endpoint (e.g., "/hc" or "/health").
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Port to use for health checks.
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// Timeout for health check requests.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Expected HTTP status codes for healthy response.
    /// </summary>
    public IReadOnlyList<int> HealthyStatusCodes { get; init; } = new[] { 200 };

    /// <summary>
    /// Whether to use HTTPS (default: false for internal container communication).
    /// </summary>
    public bool UseHttps { get; init; } = false;
}

/// <summary>
/// Result of an HTTP health check.
/// </summary>
public record HttpHealthCheckResult
{
    /// <summary>
    /// Whether the health check succeeded.
    /// </summary>
    public required bool IsHealthy { get; init; }

    /// <summary>
    /// HTTP status code returned (null if connection failed).
    /// </summary>
    public int? StatusCode { get; init; }

    /// <summary>
    /// Response time in milliseconds.
    /// </summary>
    public int? ResponseTimeMs { get; init; }

    /// <summary>
    /// Error message if the check failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Parsed health status from response body (if available).
    /// ASP.NET Core health endpoints return "Healthy", "Degraded", or "Unhealthy".
    /// </summary>
    public string? ReportedStatus { get; init; }

    /// <summary>
    /// Additional details from the health response (e.g., individual check results).
    /// </summary>
    [Obsolete("Use Entries instead. Will be removed in v1.0.")]
    public Dictionary<string, string>? Details { get; init; }

    /// <summary>
    /// Parsed health check entries from the response body.
    /// Each entry represents an individual health check (e.g., database, redis, disk).
    /// </summary>
    public List<HealthCheckEntryResult>? Entries { get; init; }

    public static HttpHealthCheckResult Healthy(int statusCode, int responseTimeMs, string? reportedStatus = null) =>
        new()
        {
            IsHealthy = true,
            StatusCode = statusCode,
            ResponseTimeMs = responseTimeMs,
            ReportedStatus = reportedStatus ?? "Healthy"
        };

    public static HttpHealthCheckResult Unhealthy(int? statusCode, string error, int? responseTimeMs = null, string? reportedStatus = null) =>
        new()
        {
            IsHealthy = false,
            StatusCode = statusCode,
            ResponseTimeMs = responseTimeMs,
            Error = error,
            ReportedStatus = reportedStatus ?? "Unhealthy"
        };

    public static HttpHealthCheckResult ConnectionFailed(string error) =>
        new()
        {
            IsHealthy = false,
            Error = error,
            ReportedStatus = "Unhealthy"
        };
}

/// <summary>
/// Parsed health check entry from an ASP.NET Core HealthReport response.
/// </summary>
public record HealthCheckEntryResult
{
    /// <summary>
    /// Name of the health check (e.g., "database", "redis", "disk").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Status string (e.g., "Healthy", "Degraded", "Unhealthy").
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Optional description from the health check.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Duration of the health check in milliseconds.
    /// </summary>
    public double? DurationMs { get; init; }

    /// <summary>
    /// Additional data reported by the health check (key-value pairs).
    /// </summary>
    public IReadOnlyDictionary<string, string>? Data { get; init; }

    /// <summary>
    /// Tags associated with the health check.
    /// </summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>
    /// Exception message if the health check threw an exception.
    /// </summary>
    public string? Exception { get; init; }
}
