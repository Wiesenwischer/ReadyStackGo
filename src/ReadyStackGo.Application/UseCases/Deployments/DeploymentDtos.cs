using ReadyStackGo.Domain.Manifests;

namespace ReadyStackGo.Application.UseCases.Deployments;

/// <summary>
/// Request to parse a Docker Compose file and detect required variables
/// </summary>
public class ParseComposeRequest
{
    /// <summary>
    /// The YAML content of the docker-compose file
    /// </summary>
    public required string YamlContent { get; set; }
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
    /// Resolved environment variable values
    /// </summary>
    public Dictionary<string, string> Variables { get; set; } = new();
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
    public List<DeployedServiceInfo> Services { get; set; } = new();
    public Dictionary<string, string> Configuration { get; set; } = new();
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
}
