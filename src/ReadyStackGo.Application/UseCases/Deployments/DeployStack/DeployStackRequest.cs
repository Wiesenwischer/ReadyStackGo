namespace ReadyStackGo.Application.UseCases.Deployments.DeployStack;

/// <summary>
/// Request DTO for deploying a stack.
/// Contains all data needed for deployment, extracted from StackDefinition by the handler.
/// </summary>
public class DeployStackRequest
{
    /// <summary>
    /// Name for this deployment (how it will be identified).
    /// </summary>
    public required string StackName { get; set; }

    /// <summary>
    /// The YAML content of the manifest.
    /// </summary>
    public required string YamlContent { get; set; }

    /// <summary>
    /// Version of the stack (from product manifest metadata.productVersion).
    /// </summary>
    public string? StackVersion { get; set; }

    /// <summary>
    /// Resolved environment variable values.
    /// </summary>
    public Dictionary<string, string> Variables { get; set; } = new();

    /// <summary>
    /// Target environment ID.
    /// </summary>
    public string? EnvironmentId { get; set; }

    /// <summary>
    /// Original stack ID from catalog (format: sourceId:stackName).
    /// Stored for reference and audit.
    /// </summary>
    public string? CatalogStackId { get; set; }
}
