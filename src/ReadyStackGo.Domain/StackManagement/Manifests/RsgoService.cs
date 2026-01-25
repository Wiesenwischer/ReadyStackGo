namespace ReadyStackGo.Domain.StackManagement.Manifests;

/// <summary>
/// Service definition (Docker container).
/// </summary>
public class RsgoService
{
    /// <summary>
    /// Docker image to use.
    /// </summary>
    public required string Image { get; set; }

    /// <summary>
    /// Container name (defaults to stack_servicename).
    /// </summary>
    public string? ContainerName { get; set; }

    /// <summary>
    /// Service lifecycle type (defaults to Service).
    /// Use "init" for run-once containers like database migrators.
    /// Init containers run before regular services and do not restart (fail fast).
    /// If an init container fails, re-deploy the stack to retry.
    /// </summary>
    public ServiceLifecycle Lifecycle { get; set; } = ServiceLifecycle.Service;

    /// <summary>
    /// Environment variables for this service.
    /// Values can reference variables using ${VAR_NAME} syntax.
    /// </summary>
    public Dictionary<string, string>? Environment { get; set; }

    /// <summary>
    /// Port mappings (host:container format).
    /// </summary>
    public List<string>? Ports { get; set; }

    /// <summary>
    /// Volume mappings.
    /// </summary>
    public List<string>? Volumes { get; set; }

    /// <summary>
    /// Networks to connect to.
    /// </summary>
    public List<string>? Networks { get; set; }

    /// <summary>
    /// Service dependencies (other service names).
    /// </summary>
    public List<string>? DependsOn { get; set; }

    /// <summary>
    /// Restart policy.
    /// </summary>
    public string? Restart { get; set; }

    /// <summary>
    /// Container command override.
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    /// Container entrypoint override.
    /// </summary>
    public string? Entrypoint { get; set; }

    /// <summary>
    /// Working directory in the container.
    /// </summary>
    public string? WorkingDir { get; set; }

    /// <summary>
    /// User to run as.
    /// </summary>
    public string? User { get; set; }

    /// <summary>
    /// Container labels.
    /// </summary>
    public Dictionary<string, string>? Labels { get; set; }

    /// <summary>
    /// Health check configuration.
    /// </summary>
    public RsgoHealthCheck? HealthCheck { get; set; }
}
