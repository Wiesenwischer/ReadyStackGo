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

public class GetDeploymentByIdHandler : IRequestHandler<GetDeploymentByIdQuery, GetDeploymentResponse>
{
    private readonly IDeploymentService _deploymentService;

    public GetDeploymentByIdHandler(IDeploymentService deploymentService)
    {
        _deploymentService = deploymentService;
    }

    public async Task<GetDeploymentResponse> Handle(GetDeploymentByIdQuery request, CancellationToken cancellationToken)
    {
        return await _deploymentService.GetDeploymentByIdAsync(request.EnvironmentId, request.DeploymentId);
    }
}
