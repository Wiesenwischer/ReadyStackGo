using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Notifications;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.UseCases.Deployments.StopProductContainers;

/// <summary>
/// Stops all (or selected) containers of a product deployment.
/// Iterates over product stacks and calls IDockerService.StopStackContainersAsync for each.
/// Transitions the ProductDeployment to Stopped when all stacks are stopped successfully.
/// </summary>
public class StopProductContainersHandler : IRequestHandler<StopProductContainersCommand, StopProductContainersResponse>
{
    private readonly IProductDeploymentRepository _repository;
    private readonly IDockerService _dockerService;
    private readonly INotificationService? _inAppNotificationService;
    private readonly ILogger<StopProductContainersHandler> _logger;

    public StopProductContainersHandler(
        IProductDeploymentRepository repository,
        IDockerService dockerService,
        ILogger<StopProductContainersHandler> logger,
        INotificationService? inAppNotificationService = null)
    {
        _repository = repository;
        _dockerService = dockerService;
        _logger = logger;
        _inAppNotificationService = inAppNotificationService;
    }

    public async Task<StopProductContainersResponse> Handle(
        StopProductContainersCommand request, CancellationToken cancellationToken)
    {
        // 1. Load product deployment
        if (!Guid.TryParse(request.ProductDeploymentId, out var pdGuid))
        {
            return StopProductContainersResponse.Failed("Invalid product deployment ID format.");
        }

        var productDeployment = _repository.Get(ProductDeploymentId.FromGuid(pdGuid));
        if (productDeployment == null)
        {
            return StopProductContainersResponse.Failed("Product deployment not found.");
        }

        // 2. Validate status — only operational deployments can have containers stopped
        if (!productDeployment.CanStop)
        {
            return StopProductContainersResponse.Failed(
                $"Cannot stop containers. Product deployment status is {productDeployment.Status}, must be Running or PartiallyRunning.");
        }

        // 3. Resolve environment ID
        var environmentId = productDeployment.EnvironmentId.Value.ToString();

        // 4. Determine which stacks to stop
        var allStacks = productDeployment.GetStacksInDeployOrder();
        var targetStacks = ResolveTargetStacks(allStacks, request.StackNames, out var unknownNames);

        if (unknownNames.Count > 0)
        {
            return StopProductContainersResponse.Failed(
                $"Unknown stack name(s): {string.Join(", ", unknownNames)}. " +
                $"Available stacks: {string.Join(", ", allStacks.Select(s => s.StackName))}.");
        }

        _logger.LogInformation(
            "Stopping containers for product deployment {ProductDeploymentId} ({ProductName} v{Version}), {StackCount} stacks",
            productDeployment.Id, productDeployment.ProductName, productDeployment.ProductVersion,
            targetStacks.Count);

        // 5. Stop containers for each stack
        var results = new List<StackContainerResult>();

        foreach (var stack in targetStacks)
        {
            var result = new StackContainerResult
            {
                StackName = stack.StackName,
                StackDisplayName = stack.StackDisplayName
            };

            if (string.IsNullOrEmpty(stack.DeploymentStackName))
            {
                result.Success = true;
                result.ContainersStopped = 0;
                _logger.LogInformation("Stack {StackName} has no deployment stack name, skipping", stack.StackDisplayName);
            }
            else
            {
                try
                {
                    var stoppedContainers = await _dockerService.StopStackContainersAsync(
                        environmentId, stack.DeploymentStackName, cancellationToken);

                    result.Success = true;
                    result.ContainersStopped = stoppedContainers.Count;

                    _logger.LogInformation(
                        "Stopped {Count} containers for stack {StackName}",
                        stoppedContainers.Count, stack.StackDisplayName);
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = ex.Message;

                    _logger.LogWarning(ex,
                        "Failed to stop containers for stack {StackName}", stack.StackDisplayName);
                }
            }

            results.Add(result);
        }

        // 6. Build response
        var stoppedCount = results.Count(r => r.Success);
        var failedCount = results.Count(r => !r.Success);
        var success = failedCount == 0;

        var message = success
            ? $"Stopped containers for {stoppedCount} stack(s) of product '{productDeployment.ProductName}'."
            : $"Stopped {stoppedCount}/{results.Count} stack(s) with {failedCount} error(s).";

        // 7. Transition to Stopped when all stacks were stopped successfully
        if (success)
        {
            productDeployment.MarkAsStopped(message);
            _repository.Update(productDeployment);
            _repository.SaveChanges();
        }

        // 8. Create in-app notification
        await CreateInAppNotificationAsync(productDeployment, success, stoppedCount, failedCount, cancellationToken);

        return new StopProductContainersResponse
        {
            Success = success,
            Message = message,
            ProductDeploymentId = productDeployment.Id.Value.ToString(),
            ProductName = productDeployment.ProductName,
            TotalStacks = results.Count,
            StoppedStacks = stoppedCount,
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
        ProductDeployment pd, bool success, int stoppedStacks, int failedStacks, CancellationToken ct)
    {
        if (_inAppNotificationService == null) return;

        try
        {
            var notification = NotificationFactory.CreateProductDeploymentResult(
                success, "stop", pd.ProductName, pd.ProductVersion,
                pd.TotalStacks, stoppedStacks, failedStacks,
                productDeploymentId: pd.Id.Value.ToString());

            await _inAppNotificationService.AddAsync(notification, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to create in-app notification for product container stop");
        }
    }
}
