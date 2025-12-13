namespace ReadyStackGo.Domain.StackManagement.Stacks;

/// <summary>
/// Template for a service in a stack definition.
/// Contains all the configuration needed to create a container.
/// This is a Value Object - compared by value, not identity.
/// </summary>
public record ServiceTemplate
{
    /// <summary>
    /// Name of the service.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Docker image to use (e.g., "nginx:latest").
    /// </summary>
    public required string Image { get; init; }

    /// <summary>
    /// Container name override (optional).
    /// </summary>
    public string? ContainerName { get; init; }

    /// <summary>
    /// Port mappings (host:container format).
    /// </summary>
    public IReadOnlyList<PortMapping> Ports { get; init; } = Array.Empty<PortMapping>();

    /// <summary>
    /// Volume mappings.
    /// </summary>
    public IReadOnlyList<VolumeMapping> Volumes { get; init; } = Array.Empty<VolumeMapping>();

    /// <summary>
    /// Environment variables.
    /// </summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Labels to apply to the container.
    /// </summary>
    public IReadOnlyDictionary<string, string> Labels { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Networks this service connects to.
    /// </summary>
    public IReadOnlyList<string> Networks { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Services this service depends on.
    /// </summary>
    public IReadOnlyList<string> DependsOn { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Restart policy (e.g., "always", "unless-stopped", "on-failure").
    /// </summary>
    public string? RestartPolicy { get; init; }

    /// <summary>
    /// Command to run in the container.
    /// </summary>
    public string? Command { get; init; }

    /// <summary>
    /// Entrypoint override.
    /// </summary>
    public string? Entrypoint { get; init; }

    /// <summary>
    /// Working directory inside the container.
    /// </summary>
    public string? WorkingDir { get; init; }

    /// <summary>
    /// User to run the container as.
    /// </summary>
    public string? User { get; init; }

    /// <summary>
    /// Health check configuration.
    /// </summary>
    public ServiceHealthCheck? HealthCheck { get; init; }
}

/// <summary>
/// Port mapping for a service.
/// </summary>
public record PortMapping
{
    /// <summary>
    /// Host port (can be a range like "8080-8090").
    /// </summary>
    public string HostPort { get; init; } = string.Empty;

    /// <summary>
    /// Container port.
    /// </summary>
    public string ContainerPort { get; init; } = string.Empty;

    /// <summary>
    /// Protocol (tcp/udp). Defaults to tcp.
    /// </summary>
    public string Protocol { get; init; } = "tcp";

    /// <summary>
    /// Host IP to bind to (optional).
    /// </summary>
    public string? HostIp { get; init; }

    /// <summary>
    /// Parses a port string like "8080:80", "8080:80/udp", "127.0.0.1:8080:80".
    /// </summary>
    public static PortMapping Parse(string portString)
    {
        var protocol = "tcp";
        if (portString.Contains('/'))
        {
            var parts = portString.Split('/');
            portString = parts[0];
            protocol = parts[1];
        }

        var segments = portString.Split(':');
        return segments.Length switch
        {
            1 => new PortMapping { ContainerPort = segments[0], HostPort = segments[0], Protocol = protocol },
            2 => new PortMapping { HostPort = segments[0], ContainerPort = segments[1], Protocol = protocol },
            3 => new PortMapping { HostIp = segments[0], HostPort = segments[1], ContainerPort = segments[2], Protocol = protocol },
            _ => throw new ArgumentException($"Invalid port mapping: {portString}")
        };
    }

    public override string ToString()
    {
        var result = string.IsNullOrEmpty(HostIp)
            ? $"{HostPort}:{ContainerPort}"
            : $"{HostIp}:{HostPort}:{ContainerPort}";

        return Protocol != "tcp" ? $"{result}/{Protocol}" : result;
    }
}

/// <summary>
/// Volume mapping for a service.
/// </summary>
public record VolumeMapping
{
    /// <summary>
    /// Source path or volume name.
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// Target path inside the container.
    /// </summary>
    public string Target { get; init; } = string.Empty;

    /// <summary>
    /// Whether the volume is read-only.
    /// </summary>
    public bool ReadOnly { get; init; }

    /// <summary>
    /// Volume type (bind, volume, tmpfs).
    /// </summary>
    public string Type { get; init; } = "volume";

    /// <summary>
    /// Parses a volume string like "/host/path:/container/path:ro".
    /// </summary>
    public static VolumeMapping Parse(string volumeString)
    {
        var readOnly = false;
        if (volumeString.EndsWith(":ro"))
        {
            readOnly = true;
            volumeString = volumeString[..^3];
        }
        else if (volumeString.EndsWith(":rw"))
        {
            volumeString = volumeString[..^3];
        }

        var parts = volumeString.Split(':');
        if (parts.Length < 2)
        {
            // Anonymous volume
            return new VolumeMapping { Target = parts[0], Type = "volume" };
        }

        var source = parts[0];
        var target = parts[1];
        var type = source.StartsWith('/') || source.StartsWith('.') || source.Contains('\\')
            ? "bind"
            : "volume";

        return new VolumeMapping
        {
            Source = source,
            Target = target,
            ReadOnly = readOnly,
            Type = type
        };
    }

    public override string ToString()
    {
        if (string.IsNullOrEmpty(Source))
            return Target;

        var result = $"{Source}:{Target}";
        return ReadOnly ? $"{result}:ro" : result;
    }
}

/// <summary>
/// Health check configuration for a service.
/// </summary>
public record ServiceHealthCheck
{
    /// <summary>
    /// Test command to run.
    /// </summary>
    public IReadOnlyList<string> Test { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Interval between health checks.
    /// </summary>
    public TimeSpan? Interval { get; init; }

    /// <summary>
    /// Timeout for each health check.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Number of retries before marking unhealthy.
    /// </summary>
    public int? Retries { get; init; }

    /// <summary>
    /// Start period before health checks begin.
    /// </summary>
    public TimeSpan? StartPeriod { get; init; }
}
