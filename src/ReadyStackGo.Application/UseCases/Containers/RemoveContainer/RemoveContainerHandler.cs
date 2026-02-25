using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Application.UseCases.Containers.RemoveContainer;

public class RemoveContainerHandler : IRequestHandler<RemoveContainerCommand, RemoveContainerResult>
{
    private readonly IDockerService _dockerService;
    private readonly IDeploymentRepository _deploymentRepository;

    public RemoveContainerHandler(IDockerService dockerService, IDeploymentRepository deploymentRepository)
    {
        _dockerService = dockerService;
        _deploymentRepository = deploymentRepository;
    }

    public async Task<RemoveContainerResult> Handle(RemoveContainerCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var containers = await _dockerService.ListContainersAsync(request.EnvironmentId, cancellationToken);
            var container = containers.FirstOrDefault(c => c.Id == request.ContainerId);

            if (container == null)
                return new RemoveContainerResult(false, "Container not found.");

            // Block removal of containers managed by a deployment
            if (container.Labels.TryGetValue("rsgo.stack", out var stackName)
                && Guid.TryParse(request.EnvironmentId, out var envGuid))
            {
                var deployment = _deploymentRepository.GetByStackName(new EnvironmentId(envGuid), stackName);
                if (deployment != null)
                    return new RemoveContainerResult(false,
                        $"Container belongs to managed stack '{stackName}'. Remove the deployment via the Deployments page instead.");
            }

            if (!request.Force && container.State.Equals("running", StringComparison.OrdinalIgnoreCase))
                return new RemoveContainerResult(false, "Cannot remove a running container. Stop it first or use force.");

            await _dockerService.RemoveContainerAsync(request.EnvironmentId, request.ContainerId, request.Force, cancellationToken);
            return new RemoveContainerResult(true);
        }
        catch (InvalidOperationException ex)
        {
            return new RemoveContainerResult(false, ex.Message);
        }
    }
}
