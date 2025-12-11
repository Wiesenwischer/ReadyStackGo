namespace ReadyStackGo.Domain.Deployment.RuntimeConfig;

/// <summary>
/// Configuration for service health checks.
/// Represents runtime health check settings applied when a stack is deployed.
/// </summary>
public record ServiceHealthCheckConfig
{
    /// <summary>
    /// Name of the service this health check applies to.
    /// </summary>
    public string ServiceName { get; init; } = null!;

    /// <summary>
    /// Health check type: "docker", "http", "tcp", or "none".
    /// </summary>
    public string Type { get; init; } = "docker";

    /// <summary>
    /// HTTP path for health endpoint (e.g., "/hc").
    /// Only used when Type = "http".
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Port for HTTP/TCP health checks.
    /// </summary>
    public int? Port { get; init; }

    /// <summary>
    /// Expected HTTP status codes for healthy response.
    /// </summary>
    public IReadOnlyList<int>? ExpectedStatusCodes { get; init; }

    /// <summary>
    /// Whether to use HTTPS for HTTP health checks.
    /// </summary>
    public bool Https { get; init; }

    /// <summary>
    /// Interval between checks (e.g., "30s").
    /// </summary>
    public string? Interval { get; init; }

    /// <summary>
    /// Timeout for each check (e.g., "10s").
    /// </summary>
    public string? Timeout { get; init; }

    /// <summary>
    /// Number of retries before marking unhealthy.
    /// </summary>
    public int? Retries { get; init; }

    /// <summary>
    /// Returns true if this is an HTTP health check.
    /// </summary>
    public bool IsHttpHealthCheck => Type.Equals("http", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true if this is a TCP health check.
    /// </summary>
    public bool IsTcpHealthCheck => Type.Equals("tcp", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true if health checks are disabled.
    /// </summary>
    public bool IsDisabled => Type.Equals("none", StringComparison.OrdinalIgnoreCase);

    public ServiceHealthCheckConfig() { }

    public ServiceHealthCheckConfig(string serviceName, string type = "docker")
    {
        ServiceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
        Type = type;
    }
}
