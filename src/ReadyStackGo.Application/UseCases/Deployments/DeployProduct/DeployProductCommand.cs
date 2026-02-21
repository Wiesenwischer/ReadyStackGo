using MediatR;

namespace ReadyStackGo.Application.UseCases.Deployments.DeployProduct;

/// <summary>
/// Command to deploy an entire product (all stacks) as a single unit.
/// </summary>
public record DeployProductCommand(
    string EnvironmentId,
    string ProductId,
    List<DeployProductStackConfig> StackConfigs,
    Dictionary<string, string> SharedVariables,
    string? SessionId = null,
    bool ContinueOnError = true,
    string? UserId = null
) : IRequest<DeployProductResponse>;

/// <summary>
/// Per-stack configuration provided by the client.
/// </summary>
public record DeployProductStackConfig(
    string StackId,
    string DeploymentStackName,
    Dictionary<string, string> Variables);
