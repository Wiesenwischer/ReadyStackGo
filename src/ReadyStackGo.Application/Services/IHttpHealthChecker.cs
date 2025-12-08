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
    public Dictionary<string, string>? Details { get; init; }

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
