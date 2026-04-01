using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.Application.UseCases.Deployments.Precheck;

/// <summary>
/// Context DTO passed to all precheck rules.
/// Contains all information needed to evaluate deployment readiness.
/// </summary>
public record PrecheckContext
{
    /// <summary>Environment ID where deployment will happen.</summary>
    public required string EnvironmentId { get; init; }

    /// <summary>Stack name for the deployment.</summary>
    public required string StackName { get; init; }

    /// <summary>Stack definition from the catalog.</summary>
    public required StackDefinition StackDefinition { get; init; }

    /// <summary>Resolved variable values provided by the user.</summary>
    public required IReadOnlyDictionary<string, string> Variables { get; init; }

    /// <summary>All containers currently running on the target environment.</summary>
    public required IReadOnlyList<ContainerDto> RunningContainers { get; init; }

    /// <summary>All volumes on the target environment.</summary>
    public required IReadOnlyList<DockerVolumeRaw> ExistingVolumes { get; init; }

    /// <summary>Existing deployment for the same stack name (if any).</summary>
    public Deployment? ExistingDeployment { get; init; }
}
