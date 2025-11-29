namespace ReadyStackGo.Domain.Manifests;

/// <summary>
/// Represents a parsed Docker Compose file structure.
/// Supports Docker Compose file format version 3.x
/// </summary>
public class DockerComposeDefinition
{
    /// <summary>
    /// Compose file version (e.g., "3.8", "3.9")
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Service definitions keyed by service name
    /// </summary>
    public Dictionary<string, ComposeServiceDefinition> Services { get; set; } = new();

    /// <summary>
    /// Named volumes (optional)
    /// </summary>
    public Dictionary<string, ComposeVolumeDefinition>? Volumes { get; set; }

    /// <summary>
    /// Custom networks (optional)
    /// </summary>
    public Dictionary<string, ComposeNetworkDefinition>? Networks { get; set; }
}

/// <summary>
/// Represents a service definition in a Docker Compose file
/// </summary>
public class ComposeServiceDefinition
{
    /// <summary>
    /// Container image to use
    /// </summary>
    public string? Image { get; set; }

    /// <summary>
    /// Build context path (mutually exclusive with Image for deployment)
    /// </summary>
    public string? Build { get; set; }

    /// <summary>
    /// Custom container name
    /// </summary>
    public string? ContainerName { get; set; }

    /// <summary>
    /// Port mappings (e.g., "8080:80", "127.0.0.1:5432:5432")
    /// </summary>
    public List<string>? Ports { get; set; }

    /// <summary>
    /// Internal ports to expose to other containers
    /// </summary>
    public List<string>? Expose { get; set; }

    /// <summary>
    /// Environment variables (key-value pairs)
    /// </summary>
    public Dictionary<string, string>? Environment { get; set; }

    /// <summary>
    /// Environment file paths
    /// </summary>
    public List<string>? EnvFile { get; set; }

    /// <summary>
    /// Volume mounts (host:container or named:container)
    /// </summary>
    public List<string>? Volumes { get; set; }

    /// <summary>
    /// Service dependencies
    /// </summary>
    public List<string>? DependsOn { get; set; }

    /// <summary>
    /// Restart policy (no, always, on-failure, unless-stopped)
    /// </summary>
    public string? Restart { get; set; }

    /// <summary>
    /// Container labels
    /// </summary>
    public Dictionary<string, string>? Labels { get; set; }

    /// <summary>
    /// Networks to connect to
    /// </summary>
    public List<string>? Networks { get; set; }

    /// <summary>
    /// Command override
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    /// Entrypoint override
    /// </summary>
    public string? Entrypoint { get; set; }

    /// <summary>
    /// Working directory inside the container
    /// </summary>
    public string? WorkingDir { get; set; }

    /// <summary>
    /// Run container in privileged mode
    /// </summary>
    public bool? Privileged { get; set; }

    /// <summary>
    /// User to run as
    /// </summary>
    public string? User { get; set; }

    /// <summary>
    /// Health check configuration
    /// </summary>
    public ComposeHealthCheck? HealthCheck { get; set; }
}

/// <summary>
/// Health check configuration for a service
/// </summary>
public class ComposeHealthCheck
{
    /// <summary>
    /// Health check command (e.g., ["CMD", "curl", "-f", "http://localhost/"])
    /// </summary>
    public List<string>? Test { get; set; }

    /// <summary>
    /// Interval between health checks (e.g., "30s")
    /// </summary>
    public string? Interval { get; set; }

    /// <summary>
    /// Timeout for each health check (e.g., "10s")
    /// </summary>
    public string? Timeout { get; set; }

    /// <summary>
    /// Number of retries before marking unhealthy
    /// </summary>
    public int? Retries { get; set; }

    /// <summary>
    /// Start period for the container to initialize (e.g., "5s")
    /// </summary>
    public string? StartPeriod { get; set; }
}

/// <summary>
/// Named volume definition
/// </summary>
public class ComposeVolumeDefinition
{
    /// <summary>
    /// Volume driver (e.g., "local", "nfs")
    /// </summary>
    public string? Driver { get; set; }

    /// <summary>
    /// Driver-specific options
    /// </summary>
    public Dictionary<string, string>? DriverOpts { get; set; }

    /// <summary>
    /// Whether the volume is external (created outside compose)
    /// </summary>
    public bool? External { get; set; }
}

/// <summary>
/// Network definition
/// </summary>
public class ComposeNetworkDefinition
{
    /// <summary>
    /// Network driver (e.g., "bridge", "overlay")
    /// </summary>
    public string? Driver { get; set; }

    /// <summary>
    /// Driver-specific options
    /// </summary>
    public Dictionary<string, string>? DriverOpts { get; set; }

    /// <summary>
    /// Whether the network is external (created outside compose)
    /// </summary>
    public bool? External { get; set; }
}

/// <summary>
/// Represents an environment variable reference found in a compose file
/// </summary>
public class EnvironmentVariableDefinition
{
    /// <summary>
    /// Variable name (e.g., "DATABASE_URL")
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Default value if specified (from ${VAR:-default} syntax)
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Whether the variable is required (no default value)
    /// </summary>
    public bool IsRequired => DefaultValue == null;

    /// <summary>
    /// Services that use this variable
    /// </summary>
    public List<string> UsedInServices { get; set; } = new();

    /// <summary>
    /// Optional description for UI display
    /// </summary>
    public string? Description { get; set; }
}
