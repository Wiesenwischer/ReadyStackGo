namespace ReadyStackGo.Domain.Deployment;

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
    /// The network name to use for all containers.
    /// </summary>
    public string NetworkName { get; set; } = "rsgo-network";

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
}

public class DeploymentResult
{
    public bool Success { get; set; }
    public string? StackVersion { get; set; }
    public List<string> DeployedContexts { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public DateTime DeploymentTime { get; set; }
}
