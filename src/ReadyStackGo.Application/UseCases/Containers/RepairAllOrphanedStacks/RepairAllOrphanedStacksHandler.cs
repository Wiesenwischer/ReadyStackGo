using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Containers.RepairOrphanedStack;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Application.UseCases.Containers.RepairAllOrphanedStacks;

public class RepairAllOrphanedStacksHandler
    : IRequestHandler<RepairAllOrphanedStacksCommand, RepairAllOrphanedStacksResult>
{
    private readonly IDockerService _dockerService;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<RepairAllOrphanedStacksHandler> _logger;

    public RepairAllOrphanedStacksHandler(
        IDockerService dockerService,
        IDeploymentRepository deploymentRepository,
        IMediator mediator,
        ILogger<RepairAllOrphanedStacksHandler> logger)
    {
        _dockerService = dockerService;
        _deploymentRepository = deploymentRepository;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<RepairAllOrphanedStacksResult> Handle(
        RepairAllOrphanedStacksCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!Guid.TryParse(request.EnvironmentId, out var envGuid))
                return new RepairAllOrphanedStacksResult(false, 0, 0, "Invalid environment ID format.");

            var environmentId = new EnvironmentId(envGuid);

            // Find all orphaned stack names
            var containers = await _dockerService.ListContainersAsync(request.EnvironmentId, cancellationToken);
            var stackNames = containers
                .Where(c => c.Labels.ContainsKey("rsgo.stack"))
                .Select(c => c.Labels["rsgo.stack"])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var orphanedStackNames = stackNames
                .Where(name => _deploymentRepository.GetByStackName(environmentId, name) == null)
                .ToList();

            if (orphanedStackNames.Count == 0)
                return new RepairAllOrphanedStacksResult(true, 0, 0);

            var repairedCount = 0;
            var failedCount = 0;

            foreach (var stackName in orphanedStackNames)
            {
                var result = await _mediator.Send(
                    new RepairOrphanedStackCommand(request.EnvironmentId, stackName, request.UserId),
                    cancellationToken);

                if (result.Success)
                {
                    repairedCount++;
                }
                else
                {
                    failedCount++;
                    _logger.LogWarning(
                        "Failed to repair orphaned stack '{StackName}': {Error}",
                        stackName, result.ErrorMessage);
                }
            }

            return new RepairAllOrphanedStacksResult(true, repairedCount, failedCount);
        }
        catch (InvalidOperationException ex)
        {
            return new RepairAllOrphanedStacksResult(false, 0, 0, ex.Message);
        }
    }
}
