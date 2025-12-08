namespace ReadyStackGo.Infrastructure.Manifests;

/// <summary>
/// Release manifest as defined in the specification
/// Defines the complete deployment configuration for a stack release
/// </summary>
public class ReleaseManifest
{
    public required string ManifestVersion { get; set; } = "1.0.0";
    public required string StackVersion { get; set; }
    public int SchemaVersion { get; set; }
    public GatewayConfig? Gateway { get; set; }
    public Dictionary<string, ContextDefinition> Contexts { get; set; } = new();
    public Dictionary<string, FeatureDefault> Features { get; set; } = new();
    public ManifestMetadata? Metadata { get; set; }
}

public class GatewayConfig
{
    public required string Context { get; set; }
    public string Protocol { get; set; } = "https";
    public int PublicPort { get; set; } = 8443;
    public int InternalHttpPort { get; set; } = 8080;
}

public class ContextDefinition
{
    public required string Image { get; set; }
    public required string Version { get; set; }
    public required string ContainerName { get; set; }
    public bool Internal { get; set; } = true;
    public Dictionary<string, string>? Env { get; set; }
    public List<string>? Ports { get; set; }
    public Dictionary<string, string>? Volumes { get; set; }
    public List<string>? DependsOn { get; set; }

    /// <summary>
    /// HTTP health check configuration. If specified, RSGO will call this endpoint
    /// instead of relying on Docker HEALTHCHECK status.
    /// </summary>
    public HealthCheckConfig? Health { get; set; }
}

/// <summary>
/// Configuration for HTTP health checks.
/// Allows RSGO to directly call ASP.NET Core /hc endpoints or similar.
/// </summary>
public class HealthCheckConfig
{
    /// <summary>
    /// Health check type: "http" (default), "tcp", or "none" (disable).
    /// </summary>
    public string Type { get; set; } = "http";

    /// <summary>
    /// URL path to health endpoint (e.g., "/hc" or "/health").
    /// For http type, this is appended to the container's internal URL.
    /// </summary>
    public string Path { get; set; } = "/hc";

    /// <summary>
    /// Port to use for health checks. Defaults to first exposed port.
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Timeout for health check requests in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Interval between health checks in seconds (for background monitoring).
    /// </summary>
    public int IntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Number of consecutive failures before marking as unhealthy.
    /// </summary>
    public int UnhealthyThreshold { get; set; } = 3;

    /// <summary>
    /// Expected HTTP status codes for healthy response (default: 200).
    /// </summary>
    public List<int> HealthyStatusCodes { get; set; } = new() { 200 };
}

public class FeatureDefault
{
    public bool Default { get; set; }
    public string? Description { get; set; }
}

public class ManifestMetadata
{
    public string? ReleaseName { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public string? Description { get; set; }
    public List<string>? ChangeNotes { get; set; }
}
