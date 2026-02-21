using MediatR;
using ReadyStackGo.Application.UseCases.Deployments.GetProductDeployment;

namespace ReadyStackGo.Application.UseCases.Deployments.GetProductDeploymentByProduct;

/// <summary>
/// Query to get the active product deployment by product group ID.
/// </summary>
public record GetProductDeploymentByProductQuery(
    string EnvironmentId,
    string ProductGroupId
) : IRequest<GetProductDeploymentResponse?>;
