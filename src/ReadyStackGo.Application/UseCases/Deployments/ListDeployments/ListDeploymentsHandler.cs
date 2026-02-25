using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.UseCases.Deployments.ListDeployments;

public class ListDeploymentsHandler : IRequestHandler<ListDeploymentsQuery, ListDeploymentsResponse>
{
    private readonly IDeploymentService _deploymentService;
    private readonly IProductDeploymentRepository _productDeploymentRepository;

    public ListDeploymentsHandler(
        IDeploymentService deploymentService,
        IProductDeploymentRepository productDeploymentRepository)
    {
        _deploymentService = deploymentService;
        _productDeploymentRepository = productDeploymentRepository;
    }

    public async Task<ListDeploymentsResponse> Handle(ListDeploymentsQuery request, CancellationToken cancellationToken)
    {
        var result = await _deploymentService.ListDeploymentsAsync(request.EnvironmentId);

        if (!result.Success || !Guid.TryParse(request.EnvironmentId, out var envGuid))
            return result;

        // Filter out stacks that belong to an active product deployment —
        // those are shown in the "Product Deployments" section instead.
        var productDeployments = _productDeploymentRepository.GetByEnvironment(new EnvironmentId(envGuid));
        var productOwnedDeploymentIds = productDeployments
            .Where(pd => !pd.IsTerminal)
            .SelectMany(pd => pd.Stacks)
            .Where(s => s.DeploymentId != null)
            .Select(s => s.DeploymentId!.Value.ToString())
            .ToHashSet();

        result.Deployments = result.Deployments
            .Where(d => d.DeploymentId == null || !productOwnedDeploymentIds.Contains(d.DeploymentId))
            .ToList();

        return result;
    }
}
