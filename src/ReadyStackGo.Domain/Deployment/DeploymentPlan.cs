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
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public DateTime DeploymentTime { get; set; }
}
