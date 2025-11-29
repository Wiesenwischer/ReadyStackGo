namespace ReadyStackGo.Domain.Configuration;

/// <summary>
/// Configuration for tracking deployments across environments.
/// Stored in rsgo.deployments.json
/// </summary>
public class DeploymentsConfig
{
    /// <summary>
    /// Deployments keyed by environment ID, then by stack name.
    /// </summary>
    public Dictionary<string, Dictionary<string, DeploymentRecord>> Deployments { get; set; } = new();

    /// <summary>
    /// Gets all deployments for an environment.
    /// </summary>
    public IEnumerable<DeploymentRecord> GetDeploymentsForEnvironment(string environmentId)
    {
        if (Deployments.TryGetValue(environmentId, out var envDeployments))
        {
            return envDeployments.Values;
        }
        return Enumerable.Empty<DeploymentRecord>();
    }

    /// <summary>
    /// Gets a specific deployment.
    /// </summary>
    public DeploymentRecord? GetDeployment(string environmentId, string stackName)
    {
        if (Deployments.TryGetValue(environmentId, out var envDeployments))
        {
            envDeployments.TryGetValue(stackName, out var deployment);
            return deployment;
        }
        return null;
    }

    /// <summary>
    /// Adds or updates a deployment record.
    /// </summary>
    public void SetDeployment(string environmentId, DeploymentRecord deployment)
    {
        if (!Deployments.ContainsKey(environmentId))
        {
            Deployments[environmentId] = new Dictionary<string, DeploymentRecord>();
        }
        Deployments[environmentId][deployment.StackName] = deployment;
    }

    /// <summary>
    /// Removes a deployment record.
    /// </summary>
    public bool RemoveDeployment(string environmentId, string stackName)
    {
        if (Deployments.TryGetValue(environmentId, out var envDeployments))
        {
            return envDeployments.Remove(stackName);
        }
        return false;
    }
}

/// <summary>
/// Record of a single deployment.
/// </summary>
public class DeploymentRecord
{
    public required string StackName { get; set; }
    public required string StackVersion { get; set; }
    public string? DeploymentId { get; set; }
    public DateTime DeployedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public string Status { get; set; } = "running";
    public List<DeployedService> Services { get; set; } = new();
}

/// <summary>
/// Record of a deployed service within a stack.
/// </summary>
public class DeployedService
{
    public required string ServiceName { get; set; }
    public required string ContainerName { get; set; }
    public required string Image { get; set; }
    public string Status { get; set; } = "running";
}
