using MediatR;

namespace ReadyStackGo.Application.UseCases.Deployments.UpgradeProduct;

/// <summary>
/// Command to upgrade an entire product deployment to a new version.
/// Upgrades existing stacks and deploys new stacks added in the target version.
/// </summary>
public record UpgradeProductCommand(
    string EnvironmentId,
    string ProductDeploymentId,
    string TargetProductId,
    List<UpgradeProductStackConfig> StackConfigs,
    Dictionary<string, string> SharedVariables,
    string? SessionId = null,
    bool ContinueOnError = true,
    string? UserId = null
) : IRequest<UpgradeProductResponse>;

/// <summary>
/// Per-stack configuration for the upgrade. Allows overriding variables per stack.
/// </summary>
public record UpgradeProductStackConfig(
    string StackId,
    string DeploymentStackName,
    Dictionary<string, string> Variables);
