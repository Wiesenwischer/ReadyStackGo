namespace ReadyStackGo.Domain.StackManagement.Manifests;

/// <summary>
/// Health check configuration.
/// Supports both Docker HEALTHCHECK and RSGO HTTP health checks.
/// </summary>
public class RsgoHealthCheck
{
    #region Docker HEALTHCHECK (for container-level checks)

    /// <summary>
    /// Test command to run (Docker HEALTHCHECK).
    /// </summary>
    public List<string>? Test { get; set; }

    /// <summary>
    /// Interval between checks (e.g., "30s").
    /// </summary>
    public string? Interval { get; set; }

    /// <summary>
    /// Timeout for each check (e.g., "10s").
    /// </summary>
    public string? Timeout { get; set; }

    /// <summary>
    /// Number of retries before marking unhealthy.
    /// </summary>
    public int? Retries { get; set; }

    /// <summary>
    /// Start period before checks begin (e.g., "5s").
    /// </summary>
    public string? StartPeriod { get; set; }

    #endregion

    #region RSGO HTTP Health Check (for ASP.NET Core /hc endpoints)

    /// <summary>
    /// Health check type: "docker" (default), "http", "tcp", or "none".
    /// - docker: Use Docker HEALTHCHECK (Test command)
    /// - http: RSGO calls HTTP endpoint directly
    /// - tcp: RSGO checks TCP port connectivity
    /// - none: Skip health checks for this service
    /// </summary>
    public string Type { get; set; } = "docker";

    /// <summary>
    /// HTTP path for health endpoint (e.g., "/hc" or "/health").
    /// Only used when Type = "http".
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Port for HTTP/TCP health checks. Defaults to first exposed port.
    /// Only used when Type = "http" or "tcp".
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Expected HTTP status codes for healthy response.
    /// Only used when Type = "http". Defaults to [200].
    /// </summary>
    public List<int>? ExpectedStatusCodes { get; set; }

    /// <summary>
    /// Whether to use HTTPS for HTTP health checks.
    /// Only used when Type = "http". Defaults to false.
    /// </summary>
    public bool Https { get; set; } = false;

    #endregion

    /// <summary>
    /// Returns true if this is an RSGO HTTP health check (not Docker HEALTHCHECK).
    /// </summary>
    public bool IsHttpHealthCheck => Type.Equals("http", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true if this is an RSGO TCP health check.
    /// </summary>
    public bool IsTcpHealthCheck => Type.Equals("tcp", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true if health checks are disabled.
    /// </summary>
    public bool IsDisabled => Type.Equals("none", StringComparison.OrdinalIgnoreCase);
}
