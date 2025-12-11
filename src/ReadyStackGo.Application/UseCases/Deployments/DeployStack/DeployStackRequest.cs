using CatalogStacks = ReadyStackGo.Domain.Catalog.Stacks;

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
    /// Service templates to deploy.
    /// Contains structured service definitions (no YAML).
    /// </summary>
    public required IReadOnlyList<CatalogStacks.ServiceTemplate> Services { get; set; }

    /// <summary>
    /// Named volumes for the stack.
    /// </summary>
    public IReadOnlyList<CatalogStacks.VolumeDefinition> Volumes { get; set; } = Array.Empty<CatalogStacks.VolumeDefinition>();

    /// <summary>
    /// Networks for the stack.
    /// </summary>
    public IReadOnlyList<CatalogStacks.NetworkDefinition> Networks { get; set; } = Array.Empty<CatalogStacks.NetworkDefinition>();

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
