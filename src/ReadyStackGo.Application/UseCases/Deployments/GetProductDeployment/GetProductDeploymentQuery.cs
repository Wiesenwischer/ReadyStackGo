using MediatR;

namespace ReadyStackGo.Application.UseCases.Deployments.GetProductDeployment;

/// <summary>
/// Query to get a product deployment by ID.
/// </summary>
public record GetProductDeploymentQuery(
    string EnvironmentId,
    string ProductDeploymentId
) : IRequest<GetProductDeploymentResponse?>;
