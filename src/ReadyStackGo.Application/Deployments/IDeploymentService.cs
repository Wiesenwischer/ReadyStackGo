namespace ReadyStackGo.Application.Deployments;

/// <summary>
/// Service for managing Docker Compose stack deployments.
/// v0.4: Supports deploying stacks to specific environments.
/// </summary>
public interface IDeploymentService
{
    /// <summary>
    /// Parse a Docker Compose file and detect required variables.
    /// </summary>
    Task<ParseComposeResponse> ParseComposeAsync(ParseComposeRequest request);

    /// <summary>
    /// Deploy a Docker Compose stack to an environment.
    /// </summary>
    Task<DeployComposeResponse> DeployComposeAsync(string environmentId, DeployComposeRequest request);

    /// <summary>
    /// Get deployment details for a stack.
    /// </summary>
    Task<GetDeploymentResponse> GetDeploymentAsync(string environmentId, string stackName);

    /// <summary>
    /// List all deployments in an environment.
    /// </summary>
    Task<ListDeploymentsResponse> ListDeploymentsAsync(string environmentId);

    /// <summary>
    /// Remove a deployed stack.
    /// </summary>
    Task<DeployComposeResponse> RemoveDeploymentAsync(string environmentId, string stackName);
}
