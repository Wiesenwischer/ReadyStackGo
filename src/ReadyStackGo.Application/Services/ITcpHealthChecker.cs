namespace ReadyStackGo.Application.Services;

/// <summary>
/// Service for checking TCP port connectivity of containers.
/// Used when containers don't expose HTTP endpoints (e.g., Redis, PostgreSQL).
/// </summary>
public interface ITcpHealthChecker
{
    /// <summary>
    /// Checks the health of a container by attempting a TCP connection to the specified port.
    /// </summary>
    /// <param name="containerAddress">The container's network address (hostname or IP)</param>
    /// <param name="config">TCP health check configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the TCP health check</returns>
    Task<TcpHealthCheckResult> CheckHealthAsync(
        string containerAddress,
        TcpHealthCheckConfig config,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration for TCP health checks (passed from manifest).
/// </summary>
public record TcpHealthCheckConfig
{
    /// <summary>
    /// Port to connect to for the health check.
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// Timeout for TCP connection attempts.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Result of a TCP health check.
/// </summary>
public record TcpHealthCheckResult
{
    /// <summary>
    /// Whether the TCP connection succeeded.
    /// </summary>
    public required bool IsHealthy { get; init; }

    /// <summary>
    /// Connection time in milliseconds.
    /// </summary>
    public int? ResponseTimeMs { get; init; }

    /// <summary>
    /// Error message if the check failed.
    /// </summary>
    public string? Error { get; init; }

    public static TcpHealthCheckResult Healthy(int responseTimeMs) =>
        new()
        {
            IsHealthy = true,
            ResponseTimeMs = responseTimeMs
        };

    public static TcpHealthCheckResult Unhealthy(string error, int? responseTimeMs = null) =>
        new()
        {
            IsHealthy = false,
            ResponseTimeMs = responseTimeMs,
            Error = error
        };

    public static TcpHealthCheckResult ConnectionFailed(string error) =>
        new()
        {
            IsHealthy = false,
            Error = error
        };
}
