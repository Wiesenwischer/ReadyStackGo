using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Deployments.RemoveDeployment;

public class RemoveDeploymentHandler : IRequestHandler<RemoveDeploymentCommand, DeployComposeResponse>
{
    private readonly IDeploymentService _deploymentService;

    public RemoveDeploymentHandler(IDeploymentService deploymentService)
    {
        _deploymentService = deploymentService;
    }

    public async Task<DeployComposeResponse> Handle(RemoveDeploymentCommand request, CancellationToken cancellationToken)
    {
        return await _deploymentService.RemoveDeploymentAsync(request.EnvironmentId, request.StackName);
    }
}

public class RemoveDeploymentByIdHandler : IRequestHandler<RemoveDeploymentByIdCommand, DeployComposeResponse>
{
    private readonly IDeploymentService _deploymentService;

    public RemoveDeploymentByIdHandler(IDeploymentService deploymentService)
    {
        _deploymentService = deploymentService;
    }

    public async Task<DeployComposeResponse> Handle(RemoveDeploymentByIdCommand request, CancellationToken cancellationToken)
    {
        return await _deploymentService.RemoveDeploymentByIdAsync(request.EnvironmentId, request.DeploymentId);
    }
}
