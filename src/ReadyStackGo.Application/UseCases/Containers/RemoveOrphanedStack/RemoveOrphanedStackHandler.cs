using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Application.UseCases.Containers.RemoveOrphanedStack;

public class RemoveOrphanedStackHandler
    : IRequestHandler<RemoveOrphanedStackCommand, RemoveOrphanedStackResult>
{
    private readonly IDockerService _dockerService;
    private readonly IDeploymentRepository _deploymentRepository;

    public RemoveOrphanedStackHandler(
        IDockerService dockerService,
        IDeploymentRepository deploymentRepository)
    {
        _dockerService = dockerService;
        _deploymentRepository = deploymentRepository;
    }

    public async Task<RemoveOrphanedStackResult> Handle(
        RemoveOrphanedStackCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!Guid.TryParse(request.EnvironmentId, out var envGuid))
                return new RemoveOrphanedStackResult(false, 0, "Invalid environment ID format.");

            var environmentId = new EnvironmentId(envGuid);

            // Verify the stack is actually orphaned (no deployment record)
            var deployment = _deploymentRepository.GetByStackName(environmentId, request.StackName);
            if (deployment != null)
                return new RemoveOrphanedStackResult(false, 0, "Stack is not orphaned — a deployment record exists.");

            // Find all containers belonging to this stack
            var containers = await _dockerService.ListContainersAsync(request.EnvironmentId, cancellationToken);
            var stackContainers = containers
                .Where(c => c.Labels.TryGetValue("rsgo.stack", out var stack)
                             && stack.Equals(request.StackName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (stackContainers.Count == 0)
                return new RemoveOrphanedStackResult(true, 0);

            // Remove all containers (force = true to handle running containers)
            foreach (var container in stackContainers)
            {
                await _dockerService.RemoveContainerAsync(
                    request.EnvironmentId, container.Id, force: true, cancellationToken);
            }

            return new RemoveOrphanedStackResult(true, stackContainers.Count);
        }
        catch (InvalidOperationException ex)
        {
            return new RemoveOrphanedStackResult(false, 0, ex.Message);
        }
    }
}
