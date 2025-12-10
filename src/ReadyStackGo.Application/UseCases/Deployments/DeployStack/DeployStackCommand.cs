using MediatR;

namespace ReadyStackGo.Application.UseCases.Deployments.DeployStack;

/// <summary>
/// Command to deploy a stack from the catalog.
/// Uses stackId to reference a pre-loaded StackDefinition instead of raw YAML.
/// </summary>
public record DeployStackCommand(
    string EnvironmentId,
    string StackId,
    string StackName,
    Dictionary<string, string> Variables,
    string? SessionId = null
) : IRequest<DeployStackResponse>;
