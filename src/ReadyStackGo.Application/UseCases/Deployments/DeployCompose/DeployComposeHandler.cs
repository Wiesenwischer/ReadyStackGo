using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Deployments.DeployCompose;

public class DeployComposeHandler : IRequestHandler<DeployComposeCommand, DeployComposeResponse>
{
    private readonly IDeploymentService _deploymentService;

    public DeployComposeHandler(IDeploymentService deploymentService)
    {
        _deploymentService = deploymentService;
    }

    public async Task<DeployComposeResponse> Handle(DeployComposeCommand request, CancellationToken cancellationToken)
    {
        var deployRequest = new DeployComposeRequest
        {
            StackName = request.StackName,
            YamlContent = request.YamlContent,
            Variables = request.Variables
        };

        return await _deploymentService.DeployComposeAsync(request.EnvironmentId, deployRequest);
    }
}
