using ReadyStackGo.Domain.Deployment.Observers;
using StackManagement = ReadyStackGo.Domain.StackManagement.Stacks;
using RuntimeConfig = ReadyStackGo.Domain.Deployment.RuntimeConfig;

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
    public required IReadOnlyList<StackManagement.ServiceTemplate> Services { get; set; }

    /// <summary>
    /// Named volumes for the stack.
    /// </summary>
    public IReadOnlyList<StackManagement.VolumeDefinition> Volumes { get; set; } = Array.Empty<StackManagement.VolumeDefinition>();

    /// <summary>
    /// Networks for the stack.
    /// </summary>
    public IReadOnlyList<StackManagement.NetworkDefinition> Networks { get; set; } = Array.Empty<StackManagement.NetworkDefinition>();

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

    /// <summary>
    /// Maintenance observer configuration (optional).
    /// Used to monitor external systems for maintenance mode.
    /// Uses the Deployment domain's value object.
    /// </summary>
    public MaintenanceObserverConfig? MaintenanceObserver { get; set; }

    /// <summary>
    /// Health check configurations for services (optional).
    /// Extracted from service healthCheck definitions in the stack manifest.
    /// </summary>
    public IReadOnlyList<RuntimeConfig.ServiceHealthCheckConfig>? HealthCheckConfigs { get; set; }
}
