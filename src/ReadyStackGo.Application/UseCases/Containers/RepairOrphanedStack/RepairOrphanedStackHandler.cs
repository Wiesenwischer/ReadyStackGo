using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Application.UseCases.Containers.RepairOrphanedStack;

public class RepairOrphanedStackHandler
    : IRequestHandler<RepairOrphanedStackCommand, RepairOrphanedStackResult>
{
    private readonly IDockerService _dockerService;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IProductCache _productCache;
    private readonly ILogger<RepairOrphanedStackHandler> _logger;

    public RepairOrphanedStackHandler(
        IDockerService dockerService,
        IDeploymentRepository deploymentRepository,
        IProductCache productCache,
        ILogger<RepairOrphanedStackHandler> logger)
    {
        _dockerService = dockerService;
        _deploymentRepository = deploymentRepository;
        _productCache = productCache;
        _logger = logger;
    }

    public async Task<RepairOrphanedStackResult> Handle(
        RepairOrphanedStackCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!Guid.TryParse(request.EnvironmentId, out var envGuid))
                return new RepairOrphanedStackResult(false, ErrorMessage: "Invalid environment ID format.");

            if (!Guid.TryParse(request.UserId, out var userGuid))
                return new RepairOrphanedStackResult(false, ErrorMessage: "Invalid user ID format.");

            var environmentId = new EnvironmentId(envGuid);
            var userId = UserId.FromGuid(userGuid);

            // Verify the stack is actually orphaned
            var existing = _deploymentRepository.GetByStackName(environmentId, request.StackName);
            if (existing != null)
                return new RepairOrphanedStackResult(false,
                    ErrorMessage: "Stack is not orphaned — a deployment record already exists.");

            // Find service containers (exclude init containers)
            var containers = await _dockerService.ListContainersAsync(request.EnvironmentId, cancellationToken);
            var serviceContainers = containers
                .Where(c => c.Labels.TryGetValue("rsgo.stack", out var stack)
                             && stack.Equals(request.StackName, StringComparison.OrdinalIgnoreCase))
                .Where(c => !c.Labels.TryGetValue("rsgo.lifecycle", out var lifecycle)
                             || !lifecycle.Equals("init", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (serviceContainers.Count == 0)
                return new RepairOrphanedStackResult(false,
                    ErrorMessage: "No service containers found for this stack.");

            // Try to find a matching stack in the catalog
            var (stackId, catalogMatched) = ResolveStackId(request.StackName);

            // Create a Deployment in Installing state, then immediately mark as Running
            var deploymentId = _deploymentRepository.NextIdentity();
            var deployment = Deployment.StartInstallation(
                deploymentId,
                environmentId,
                stackId,
                request.StackName,
                request.StackName,
                userId);

            // Add each running service container
            foreach (var container in serviceContainers)
            {
                var serviceName = container.Labels.TryGetValue("rsgo.context", out var ctx)
                    ? ctx
                    : container.Name;
                deployment.AddService(serviceName, container.Image, container.State);
            }

            // Transition to Running — fires DeploymentCompleted domain event
            // which triggers DeploymentCompletedHandler to auto-create ProductDeployment
            deployment.MarkAsRunning();

            _deploymentRepository.Add(deployment);
            _deploymentRepository.SaveChanges();

            _logger.LogInformation(
                "Repaired orphaned stack '{StackName}' with {ServiceCount} services. " +
                "DeploymentId: {DeploymentId}, CatalogMatched: {CatalogMatched}",
                request.StackName, serviceContainers.Count, deploymentId, catalogMatched);

            return new RepairOrphanedStackResult(true, deploymentId.ToString(), catalogMatched);
        }
        catch (InvalidOperationException ex)
        {
            return new RepairOrphanedStackResult(false, ErrorMessage: ex.Message);
        }
    }

    private (string stackId, bool catalogMatched) ResolveStackId(string stackName)
    {
        // Try to find a catalog stack that produced containers with this rsgo.stack label.
        // The label value is the Deployment's StackName/ProjectName, which for product
        // deployments follows the pattern: {deploymentName}-{stackDefName}
        var allStacks = _productCache.GetAllStacks().ToList();

        // Exact match: stack definition whose Id ends with the stack name
        foreach (var stackDef in allStacks)
        {
            // The rsgo.stack label value during deployment is set to the "projectName"
            // which equals the Deployment.StackName. For product deployments this is
            // derived via DeriveStackDeploymentName(deploymentName, stackDef.Name).
            // We can't reverse-engineer the deploymentName, but we can check if the
            // stack name ends with the stack definition's name.
            if (stackName.EndsWith($"-{stackDef.Name}", StringComparison.OrdinalIgnoreCase)
                || stackName.Equals(stackDef.Name, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Matched orphaned stack '{StackName}' to catalog stack '{StackId}'",
                    stackName, stackDef.Id.Value);
                return (stackDef.Id.Value, true);
            }
        }

        // No match — use synthetic ID
        _logger.LogDebug("No catalog match found for orphaned stack '{StackName}', using synthetic ID",
            stackName);
        return ($"orphan:{stackName}", false);
    }
}
