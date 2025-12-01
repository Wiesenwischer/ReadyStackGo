using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Deployments.GetDeployment;

public class GetDeploymentHandler : IRequestHandler<GetDeploymentQuery, GetDeploymentResponse>
{
    private readonly IDeploymentService _deploymentService;

    public GetDeploymentHandler(IDeploymentService deploymentService)
    {
        _deploymentService = deploymentService;
    }

    public async Task<GetDeploymentResponse> Handle(GetDeploymentQuery request, CancellationToken cancellationToken)
    {
        return await _deploymentService.GetDeploymentAsync(request.EnvironmentId, request.StackName);
    }
}
