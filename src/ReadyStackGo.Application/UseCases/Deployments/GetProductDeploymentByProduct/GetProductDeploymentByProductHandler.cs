using MediatR;
using ReadyStackGo.Application.UseCases.Deployments.GetProductDeployment;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.UseCases.Deployments.GetProductDeploymentByProduct;

/// <summary>
/// Handles retrieving the active product deployment by product group ID.
/// </summary>
public class GetProductDeploymentByProductHandler
    : IRequestHandler<GetProductDeploymentByProductQuery, GetProductDeploymentResponse?>
{
    private readonly IProductDeploymentRepository _repository;

    public GetProductDeploymentByProductHandler(IProductDeploymentRepository repository)
    {
        _repository = repository;
    }

    public Task<GetProductDeploymentResponse?> Handle(
        GetProductDeploymentByProductQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.EnvironmentId, out var envGuid))
        {
            return Task.FromResult<GetProductDeploymentResponse?>(null);
        }

        var productDeployment = _repository.GetActiveByProductGroupId(
            new EnvironmentId(envGuid), request.ProductGroupId);

        if (productDeployment == null)
        {
            return Task.FromResult<GetProductDeploymentResponse?>(null);
        }

        return Task.FromResult<GetProductDeploymentResponse?>(
            GetProductDeploymentHandler.MapToResponse(productDeployment));
    }
}
