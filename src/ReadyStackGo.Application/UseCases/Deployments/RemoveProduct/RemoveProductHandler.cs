using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Notifications;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.RemoveDeployment;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.UseCases.Deployments.RemoveProduct;

/// <summary>
/// Orchestrates removing all stacks of a product deployment in reverse order.
/// Each stack is removed via the existing RemoveDeploymentByIdCommand.
/// </summary>
public class RemoveProductHandler : IRequestHandler<RemoveProductCommand, RemoveProductResponse>
{
    private readonly IProductDeploymentRepository _repository;
    private readonly IMediator _mediator;
    private readonly IDeploymentNotificationService? _notificationService;
    private readonly INotificationService? _inAppNotificationService;
    private readonly ILogger<RemoveProductHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public RemoveProductHandler(
        IProductDeploymentRepository repository,
        IMediator mediator,
        ILogger<RemoveProductHandler> logger,
        IDeploymentNotificationService? notificationService = null,
        INotificationService? inAppNotificationService = null,
        TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _mediator = mediator;
        _logger = logger;
        _notificationService = notificationService;
        _inAppNotificationService = inAppNotificationService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<RemoveProductResponse> Handle(RemoveProductCommand request, CancellationToken cancellationToken)
    {
        // 1. Load existing product deployment
        if (!Guid.TryParse(request.ProductDeploymentId, out var pdGuid))
        {
            return RemoveProductResponse.Failed("Invalid product deployment ID format.");
        }

        var productDeployment = _repository.Get(ProductDeploymentId.FromGuid(pdGuid));
        if (productDeployment == null)
        {
            return RemoveProductResponse.Failed("Product deployment not found.");
        }

        // 2. Validate can remove
        if (!productDeployment.CanRemove)
        {
            return RemoveProductResponse.Failed(
                $"Product deployment cannot be removed. Current status: {productDeployment.Status}");
        }

        // 3. Start removal → transitions to Removing status, resets all stacks to Pending
        productDeployment.StartRemoval();
        _repository.Update(productDeployment);
        _repository.SaveChanges();

        _logger.LogInformation(
            "Product removal {ProductDeploymentId} initiated for {ProductName} v{Version} with {StackCount} stacks",
            productDeployment.Id, productDeployment.ProductName, productDeployment.ProductVersion,
            productDeployment.TotalStacks);

        // 4. Generate session ID
        var sessionId = !string.IsNullOrEmpty(request.SessionId)
            ? request.SessionId
            : $"product-remove-{productDeployment.ProductName}-{_timeProvider.GetUtcNow():yyyyMMddHHmmssfff}";

        // 5. Remove each stack in reverse order
        var stackResults = new List<RemoveProductStackResult>();
        var stacks = productDeployment.GetStacksInRemoveOrder();
        var hasErrors = false;

        for (var i = 0; i < stacks.Count; i++)
        {
            var stack = stacks[i];

            // Send product-level progress
            await NotifyProductProgressAsync(
                sessionId, stack.StackDisplayName, i, stacks.Count,
                productDeployment.RemovedStacks, cancellationToken);

            _logger.LogInformation(
                "Removing stack {StackIndex}/{TotalStacks}: {StackName} (DeploymentId: {DeploymentId})",
                i + 1, stacks.Count, stack.StackDisplayName, stack.DeploymentId);

            var stackResult = new RemoveProductStackResult
            {
                StackName = stack.StackName,
                StackDisplayName = stack.StackDisplayName,
                ServiceCount = stack.ServiceCount
            };

            // Remove the individual deployment if it has a DeploymentId
            if (stack.DeploymentId != null)
            {
                DeployComposeResponse removeResult;
                try
                {
                    removeResult = await _mediator.Send(new RemoveDeploymentByIdCommand(
                        request.EnvironmentId,
                        stack.DeploymentId.Value.ToString(),
                        sessionId), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception removing stack {StackName}", stack.StackDisplayName);
                    removeResult = new DeployComposeResponse
                    {
                        Success = false,
                        Message = $"Exception removing stack '{stack.StackDisplayName}': {ex.Message}"
                    };
                }

                if (removeResult.Success)
                {
                    stackResult.Success = true;
                    _logger.LogInformation("Stack {StackName} removed successfully", stack.StackDisplayName);
                }
                else
                {
                    stackResult.Success = false;
                    stackResult.ErrorMessage = removeResult.Message;
                    hasErrors = true;
                    _logger.LogWarning("Stack {StackName} removal failed: {Error}",
                        stack.StackDisplayName, removeResult.Message);
                }
            }
            else
            {
                // No deployment to remove (stack was never deployed or already removed)
                stackResult.Success = true;
                _logger.LogInformation("Stack {StackName} has no deployment to remove, marking as removed",
                    stack.StackDisplayName);
            }

            // Mark stack as removed in the product deployment regardless of Docker result.
            // The product deployment tracks orchestration state, not container state.
            productDeployment.MarkStackRemoved(stack.StackName);
            stackResults.Add(stackResult);

            // Persist after each stack
            _repository.Update(productDeployment);
            _repository.SaveChanges();
        }

        // 6. Send final notifications
        await NotifyFinalResultAsync(sessionId, productDeployment, stacks.Count, hasErrors, cancellationToken);
        await CreateInAppNotificationAsync(productDeployment, hasErrors, cancellationToken);

        _logger.LogInformation(
            "Product removal {ProductDeploymentId} completed with status {Status}. {Removed}/{Total} stacks removed",
            productDeployment.Id, productDeployment.Status, productDeployment.RemovedStacks, stacks.Count);

        var successCount = stackResults.Count(r => r.Success);
        var failCount = stackResults.Count(r => !r.Success);

        return new RemoveProductResponse
        {
            Success = !hasErrors,
            Message = FormatResultMessage(productDeployment, successCount, failCount),
            ProductDeploymentId = productDeployment.Id.Value.ToString(),
            ProductName = productDeployment.ProductName,
            Status = productDeployment.Status.ToString(),
            SessionId = sessionId,
            StackResults = stackResults
        };
    }

    private static string FormatResultMessage(ProductDeployment pd, int successCount, int failCount)
    {
        if (failCount == 0)
        {
            return $"Product '{pd.ProductName}' v{pd.ProductVersion} removed successfully ({pd.TotalStacks} stacks).";
        }

        return $"Product '{pd.ProductName}' removed with {failCount} error(s). " +
               $"{successCount}/{pd.TotalStacks} stacks removed cleanly.";
    }

    private async Task NotifyProductProgressAsync(
        string sessionId, string stackDisplayName, int stackIndex, int totalStacks,
        int removedStacks, CancellationToken ct)
    {
        if (_notificationService == null) return;

        try
        {
            var percentComplete = totalStacks > 0 ? (int)(removedStacks * 100.0 / totalStacks) : 0;
            await _notificationService.NotifyProgressAsync(
                sessionId,
                "ProductRemoval",
                $"Removing stack {stackIndex + 1}/{totalStacks}: {stackDisplayName}",
                percentComplete,
                stackDisplayName,
                totalStacks,
                removedStacks,
                0, 0, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to send product removal progress notification");
        }
    }

    private async Task NotifyFinalResultAsync(
        string sessionId, ProductDeployment pd, int totalStacks, bool hasErrors, CancellationToken ct)
    {
        if (_notificationService == null) return;

        try
        {
            if (!hasErrors)
            {
                await _notificationService.NotifyCompletedAsync(
                    sessionId,
                    $"Product '{pd.ProductName}' v{pd.ProductVersion} removed successfully ({totalStacks} stacks).",
                    totalStacks, ct);
            }
            else
            {
                await _notificationService.NotifyErrorAsync(
                    sessionId,
                    $"Product '{pd.ProductName}' removal completed with errors.",
                    null, totalStacks, pd.RemovedStacks, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to send final product removal notification");
        }
    }

    private async Task CreateInAppNotificationAsync(ProductDeployment pd, bool hasErrors, CancellationToken ct)
    {
        if (_inAppNotificationService == null) return;

        try
        {
            var notification = NotificationFactory.CreateProductDeploymentResult(
                !hasErrors, "remove", pd.ProductName, pd.ProductVersion,
                pd.TotalStacks, pd.RemovedStacks, 0,
                productDeploymentId: pd.Id.Value.ToString());

            await _inAppNotificationService.AddAsync(notification, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to create in-app notification for product removal");
        }
    }
}
