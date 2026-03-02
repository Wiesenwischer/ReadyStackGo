using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Notifications;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.UseCases.Deployments.RestartProductContainers;

/// <summary>
/// Restarts all (or selected) containers of a product deployment.
/// For each stack: Stop containers, then Start containers (sequentially).
/// If Stop fails for a stack, Start is NOT attempted for that stack.
/// Does NOT change the ProductDeployment state machine.
/// </summary>
public class RestartProductContainersHandler : IRequestHandler<RestartProductContainersCommand, RestartProductContainersResponse>
{
    private readonly IProductDeploymentRepository _repository;
    private readonly IDockerService _dockerService;
    private readonly INotificationService? _inAppNotificationService;
    private readonly ILogger<RestartProductContainersHandler> _logger;

    public RestartProductContainersHandler(
        IProductDeploymentRepository repository,
        IDockerService dockerService,
        ILogger<RestartProductContainersHandler> logger,
        INotificationService? inAppNotificationService = null)
    {
        _repository = repository;
        _dockerService = dockerService;
        _logger = logger;
        _inAppNotificationService = inAppNotificationService;
    }

    public async Task<RestartProductContainersResponse> Handle(
        RestartProductContainersCommand request, CancellationToken cancellationToken)
    {
        // 1. Load product deployment
        if (!Guid.TryParse(request.ProductDeploymentId, out var pdGuid))
        {
            return RestartProductContainersResponse.Failed("Invalid product deployment ID format.");
        }

        var productDeployment = _repository.Get(ProductDeploymentId.FromGuid(pdGuid));
        if (productDeployment == null)
        {
            return RestartProductContainersResponse.Failed("Product deployment not found.");
        }

        // 2. Validate status — only operational deployments can be restarted
        if (!productDeployment.IsOperational)
        {
            return RestartProductContainersResponse.Failed(
                $"Cannot restart containers. Product deployment status is {productDeployment.Status}, must be Running or PartiallyRunning.");
        }

        // 3. Resolve environment ID
        var environmentId = productDeployment.EnvironmentId.Value.ToString();

        // 4. Determine which stacks to restart
        var allStacks = productDeployment.GetStacksInDeployOrder();
        var targetStacks = ResolveTargetStacks(allStacks, request.StackNames, out var unknownNames);

        if (unknownNames.Count > 0)
        {
            return RestartProductContainersResponse.Failed(
                $"Unknown stack name(s): {string.Join(", ", unknownNames)}. " +
                $"Available stacks: {string.Join(", ", allStacks.Select(s => s.StackName))}.");
        }

        _logger.LogInformation(
            "Restarting containers for product deployment {ProductDeploymentId} ({ProductName} v{Version}), {StackCount} stacks",
            productDeployment.Id, productDeployment.ProductName, productDeployment.ProductVersion,
            targetStacks.Count);

        // 5. Restart containers for each stack (Stop + Start)
        var results = new List<StackRestartResult>();

        foreach (var stack in targetStacks)
        {
            var result = new StackRestartResult
            {
                StackName = stack.StackName,
                StackDisplayName = stack.StackDisplayName
            };

            if (string.IsNullOrEmpty(stack.DeploymentStackName))
            {
                result.Success = true;
                result.ContainersStopped = 0;
                result.ContainersStarted = 0;
                _logger.LogInformation("Stack {StackName} has no deployment stack name, skipping", stack.StackDisplayName);
            }
            else
            {
                try
                {
                    // Stop
                    var stoppedContainers = await _dockerService.StopStackContainersAsync(
                        environmentId, stack.DeploymentStackName, cancellationToken);
                    result.ContainersStopped = stoppedContainers.Count;

                    _logger.LogInformation(
                        "Stopped {Count} containers for stack {StackName}, now starting...",
                        stoppedContainers.Count, stack.StackDisplayName);

                    // Start (only if Stop succeeded)
                    var startedContainers = await _dockerService.StartStackContainersAsync(
                        environmentId, stack.DeploymentStackName, cancellationToken);
                    result.ContainersStarted = startedContainers.Count;
                    result.Success = true;

                    _logger.LogInformation(
                        "Started {Count} containers for stack {StackName}",
                        startedContainers.Count, stack.StackDisplayName);
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = ex.Message;

                    _logger.LogWarning(ex,
                        "Failed to restart containers for stack {StackName}", stack.StackDisplayName);
                }
            }

            results.Add(result);
        }

        // 6. Build response
        var restartedCount = results.Count(r => r.Success);
        var failedCount = results.Count(r => !r.Success);
        var success = failedCount == 0;

        var message = success
            ? $"Restarted containers for {restartedCount} stack(s) of product '{productDeployment.ProductName}'."
            : $"Restarted {restartedCount}/{results.Count} stack(s) with {failedCount} error(s).";

        // 7. Create in-app notification
        await CreateInAppNotificationAsync(productDeployment, success, restartedCount, failedCount, cancellationToken);

        return new RestartProductContainersResponse
        {
            Success = success,
            Message = message,
            ProductDeploymentId = productDeployment.Id.Value.ToString(),
            ProductName = productDeployment.ProductName,
            TotalStacks = results.Count,
            RestartedStacks = restartedCount,
            FailedStacks = failedCount,
            Results = results
        };
    }

    private static List<ProductStackDeployment> ResolveTargetStacks(
        IReadOnlyList<ProductStackDeployment> allStacks,
        List<string>? requestedNames,
        out List<string> unknownNames)
    {
        unknownNames = new List<string>();

        if (requestedNames == null || requestedNames.Count == 0)
        {
            return allStacks.Where(s => s.DeploymentStackName != null).ToList();
        }

        var resolved = new List<ProductStackDeployment>();

        foreach (var name in requestedNames)
        {
            var stack = allStacks.FirstOrDefault(s =>
                string.Equals(s.StackName, name, StringComparison.OrdinalIgnoreCase));

            if (stack == null)
            {
                unknownNames.Add(name);
            }
            else
            {
                resolved.Add(stack);
            }
        }

        return resolved;
    }

    private async Task CreateInAppNotificationAsync(
        ProductDeployment pd, bool success, int restartedStacks, int failedStacks, CancellationToken ct)
    {
        if (_inAppNotificationService == null) return;

        try
        {
            var notification = NotificationFactory.CreateProductDeploymentResult(
                success, "restart", pd.ProductName, pd.ProductVersion,
                pd.TotalStacks, restartedStacks, failedStacks,
                productDeploymentId: pd.Id.Value.ToString());

            await _inAppNotificationService.AddAsync(notification, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to create in-app notification for product container restart");
        }
    }
}
