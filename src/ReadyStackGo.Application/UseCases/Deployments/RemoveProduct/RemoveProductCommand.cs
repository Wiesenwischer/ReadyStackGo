using MediatR;

namespace ReadyStackGo.Application.UseCases.Deployments.RemoveProduct;

/// <summary>
/// Command to remove an entire product deployment (all stacks in reverse order).
/// </summary>
public record RemoveProductCommand(
    string EnvironmentId,
    string ProductDeploymentId,
    string? SessionId = null,
    string? UserId = null
) : IRequest<RemoveProductResponse>;
