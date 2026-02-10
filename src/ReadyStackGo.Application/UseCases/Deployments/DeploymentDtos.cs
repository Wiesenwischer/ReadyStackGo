using ReadyStackGo.Domain.StackManagement.Manifests;

namespace ReadyStackGo.Application.UseCases.Deployments;

// ============================================================================
// Deployment Plan DTOs
// ============================================================================

/// <summary>
/// Represents a deployment plan generated from a manifest
/// </summary>
public class DeploymentPlan
{
    public required string StackVersion { get; set; }

    /// <summary>
    /// The environment ID where this plan will be deployed.
    /// v0.4: Required for environment-scoped deployments.
    /// </summary>
    public string? EnvironmentId { get; set; }

    /// <summary>
    /// The stack name for identification.
    /// </summary>
    public string? StackName { get; set; }

    /// <summary>
    /// Network definitions from the compose file.
    /// Key is the network name as defined in compose, value indicates if it's external.
    /// Non-external networks will be prefixed with stack name for isolation.
    /// </summary>
    public Dictionary<string, NetworkDefinition> Networks { get; set; } = new();

    public List<DeploymentStep> Steps { get; set; } = new();
    public Dictionary<string, string> GlobalEnvVars { get; set; } = new();
}

public class DeploymentStep
{
    public required string ContextName { get; set; }
    public required string Image { get; set; }
    public required string Version { get; set; }
    public required string ContainerName { get; set; }
    public bool Internal { get; set; } = true;
    public Dictionary<string, string> EnvVars { get; set; } = new();
    public List<string> Ports { get; set; } = new();
    public Dictionary<string, string> Volumes { get; set; } = new();
    public List<string> DependsOn { get; set; } = new();
    public int Order { get; set; } // Deployment order based on dependencies

    /// <summary>
    /// Networks this service should be connected to.
    /// These are the resolved network names (already prefixed with stack name if not external).
    /// </summary>
    public List<string> Networks { get; set; } = new();

    /// <summary>
    /// Service lifecycle type (Service or Init).
    /// Init containers run once before regular services and only restart on failure.
    /// </summary>
    public ServiceLifecycle Lifecycle { get; set; } = ServiceLifecycle.Service;
}

/// <summary>
/// Represents a network definition from compose file.
/// </summary>
public class NetworkDefinition
{
    /// <summary>
    /// Whether this network is external (pre-existing, not managed by the stack).
    /// External networks won't be prefixed with stack name.
    /// </summary>
    public bool External { get; set; }

    /// <summary>
    /// The resolved name to use when creating/connecting to this network.
    /// </summary>
    public string ResolvedName { get; set; } = string.Empty;
}

public class DeploymentResult
{
    public bool Success { get; set; }
    public string? StackVersion { get; set; }
    public List<string> DeployedContexts { get; set; } = new();
    public List<DeployedContainerInfo> DeployedContainers { get; set; } = new();
    public List<InitContainerResultInfo> InitContainerResults { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public DateTime DeploymentTime { get; set; }
}

/// <summary>
/// Result of an init container execution (returned by DeploymentEngine).
/// </summary>
public class InitContainerResultInfo
{
    public required string ServiceName { get; set; }
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public List<string> LogLines { get; set; } = new();
}

/// <summary>
/// Detailed information about a deployed container.
/// </summary>
public class DeployedContainerInfo
{
    public required string ServiceName { get; set; }
    public required string ContainerId { get; set; }
    public required string ContainerName { get; set; }
    public required string Image { get; set; }
    public required string Status { get; set; }
}

// ============================================================================
// Docker Compose Definition DTOs
// ============================================================================

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

// ============================================================================
// Parse/Deploy Request/Response DTOs
// ============================================================================

/// <summary>
/// Request to parse a Docker Compose file and detect required variables
/// </summary>
public class ParseComposeRequest
{
    /// <summary>
    /// The YAML content of the docker-compose file
    /// </summary>
    public required string YamlContent { get; set; }

    // RBAC scope fields
    public string? OrganizationId { get; set; }
    public string? EnvironmentId { get; set; }
}

/// <summary>
/// Response from parsing a Docker Compose file
/// </summary>
public class ParseComposeResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }

    /// <summary>
    /// List of detected environment variables
    /// </summary>
    public List<EnvironmentVariableInfo> Variables { get; set; } = new();

    /// <summary>
    /// List of services defined in the compose file
    /// </summary>
    public List<string> Services { get; set; } = new();

    /// <summary>
    /// Validation errors (if any)
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Validation warnings (if any)
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Environment variable information for UI display
/// </summary>
public class EnvironmentVariableInfo
{
    public required string Name { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsRequired { get; set; }
    public List<string> UsedInServices { get; set; } = new();
}

/// <summary>
/// Request to deploy a Docker Compose stack
/// </summary>
public class DeployComposeRequest
{
    /// <summary>
    /// Name for this stack deployment
    /// </summary>
    public required string StackName { get; set; }

    /// <summary>
    /// The YAML content of the docker-compose file
    /// </summary>
    public required string YamlContent { get; set; }

    /// <summary>
    /// Version of the stack (from product manifest metadata.productVersion).
    /// </summary>
    public string? StackVersion { get; set; }

    /// <summary>
    /// Resolved environment variable values
    /// </summary>
    public Dictionary<string, string> Variables { get; set; } = new();

    /// <summary>
    /// Target environment ID (set from route parameter for RBAC).
    /// </summary>
    public string? EnvironmentId { get; set; }

    /// <summary>
    /// Client-generated session ID for real-time progress tracking via SignalR.
    /// If provided, clients can subscribe before calling this endpoint to receive all progress updates.
    /// </summary>
    public string? SessionId { get; set; }
}

/// <summary>
/// Response from deploying a Docker Compose stack
/// </summary>
public class DeployComposeResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? DeploymentId { get; set; }
    public string? StackName { get; set; }
    public List<DeployedServiceInfo> Services { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Session ID used for real-time progress tracking via SignalR.
    /// Clients can subscribe to deployment:{DeploymentSessionId} group.
    /// </summary>
    public string? DeploymentSessionId { get; set; }
}

/// <summary>
/// Information about a deployed service
/// </summary>
public class DeployedServiceInfo
{
    public required string ServiceName { get; set; }
    public string? ContainerId { get; set; }
    public string? Status { get; set; }
    public List<string> Ports { get; set; } = new();
}

/// <summary>
/// Response with deployment details
/// </summary>
public class GetDeploymentResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? DeploymentId { get; set; }
    public string? StackName { get; set; }
    public string? StackVersion { get; set; }
    public string? EnvironmentId { get; set; }
    public DateTime? DeployedAt { get; set; }
    public string? Status { get; set; }
    public string? OperationMode { get; set; }
    public List<DeployedServiceInfo> Services { get; set; } = new();
    public List<InitContainerResultDto> InitContainerResults { get; set; } = new();
    public Dictionary<string, string> Configuration { get; set; } = new();
}

/// <summary>
/// Init container result for API response.
/// </summary>
public class InitContainerResultDto
{
    public required string ServiceName { get; set; }
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public DateTime ExecutedAtUtc { get; set; }
    public string? LogOutput { get; set; }
}

/// <summary>
/// Response listing all deployments in an environment
/// </summary>
public class ListDeploymentsResponse
{
    public bool Success { get; set; }
    public List<DeploymentSummary> Deployments { get; set; } = new();
}

/// <summary>
/// Summary of a deployment for listing
/// </summary>
public class DeploymentSummary
{
    public string? DeploymentId { get; set; }
    public required string StackName { get; set; }
    public string? StackVersion { get; set; }
    public DateTime DeployedAt { get; set; }
    public int ServiceCount { get; set; }
    public string? Status { get; set; }
    public string? OperationMode { get; set; }
}
