using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Manifests;

namespace ReadyStackGo.Application.Manifests;

/// <summary>
/// Parses Docker Compose YAML files and converts them to deployment plans.
/// v0.4: First format supported is Docker Compose (Portainer-style).
/// </summary>
public interface IDockerComposeParser
{
    /// <summary>
    /// Parse a Docker Compose YAML string into a structured definition.
    /// </summary>
    /// <param name="yamlContent">The YAML content of the compose file</param>
    /// <returns>Parsed compose definition</returns>
    Task<DockerComposeDefinition> ParseAsync(string yamlContent);

    /// <summary>
    /// Detect all environment variable references in the compose file.
    /// Variables are in the format ${VAR} or ${VAR:-default}.
    /// </summary>
    /// <param name="yamlContent">The YAML content of the compose file</param>
    /// <returns>List of detected environment variables with their defaults</returns>
    Task<List<EnvironmentVariableDefinition>> DetectVariablesAsync(string yamlContent);

    /// <summary>
    /// Convert a Docker Compose definition to a deployment plan.
    /// Environment variables are resolved using the provided values.
    /// </summary>
    /// <param name="compose">Parsed compose definition</param>
    /// <param name="resolvedVariables">Resolved environment variable values</param>
    /// <param name="stackName">Name for the deployed stack</param>
    /// <returns>Deployment plan compatible with the deployment engine</returns>
    Task<DeploymentPlan> ConvertToDeploymentPlanAsync(
        DockerComposeDefinition compose,
        Dictionary<string, string> resolvedVariables,
        string stackName);

    /// <summary>
    /// Validate a Docker Compose YAML file.
    /// </summary>
    /// <param name="yamlContent">The YAML content of the compose file</param>
    /// <returns>Validation result with any errors or warnings</returns>
    Task<DockerComposeValidationResult> ValidateAsync(string yamlContent);
}

/// <summary>
/// Result of Docker Compose file validation
/// </summary>
public class DockerComposeValidationResult
{
    /// <summary>
    /// Whether the compose file is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of validation errors
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// List of validation warnings (non-blocking)
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}
