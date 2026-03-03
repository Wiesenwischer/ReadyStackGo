using MediatR;
using ReadyStackGo.Application.UseCases.Deployments.DeployProduct;

namespace ReadyStackGo.Application.UseCases.Deployments.RedeployProduct;

/// <summary>
/// Command to redeploy all or selected stacks of a running product deployment.
/// Same version, fresh image pull. Supports optional variable overrides.
/// </summary>
public record RedeployProductCommand(
    string EnvironmentId,
    string ProductDeploymentId,
    List<string>? StackNames = null,
    Dictionary<string, string>? VariableOverrides = null,
    string? SessionId = null,
    bool ContinueOnError = true,
    string? UserId = null
) : IRequest<DeployProductResponse>;
