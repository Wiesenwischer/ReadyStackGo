using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Deployments.ListDeployments;

public class ListDeploymentsHandler : IRequestHandler<ListDeploymentsQuery, ListDeploymentsResponse>
{
    private readonly IDeploymentService _deploymentService;

    public ListDeploymentsHandler(IDeploymentService deploymentService)
    {
        _deploymentService = deploymentService;
    }

    public async Task<ListDeploymentsResponse> Handle(ListDeploymentsQuery request, CancellationToken cancellationToken)
    {
        return await _deploymentService.ListDeploymentsAsync(request.EnvironmentId);
    }
}
