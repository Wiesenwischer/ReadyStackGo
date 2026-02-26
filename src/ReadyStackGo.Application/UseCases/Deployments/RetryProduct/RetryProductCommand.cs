using MediatR;
using ReadyStackGo.Application.UseCases.Deployments.DeployProduct;

namespace ReadyStackGo.Application.UseCases.Deployments.RetryProduct;

/// <summary>
/// Command to retry failed stacks of a product deployment.
/// Only deploys stacks that are in Pending or Failed status; Running stacks are skipped.
/// </summary>
public record RetryProductCommand(
    string EnvironmentId,
    string ProductDeploymentId,
    string? SessionId = null,
    bool ContinueOnError = true,
    string? UserId = null
) : IRequest<DeployProductResponse>;
