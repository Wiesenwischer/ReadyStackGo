using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Notifications;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.DeployProduct;
using ReadyStackGo.Application.UseCases.Deployments.DeployStack;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.UseCases.Deployments.RetryProduct;

/// <summary>
/// Orchestrates retrying failed stacks of a product deployment.
/// Skips stacks that are already Running; only deploys Pending stacks (including those reset from Failed).
/// </summary>
public class RetryProductHandler : IRequestHandler<RetryProductCommand, DeployProductResponse>
{
    private readonly IProductDeploymentRepository _repository;
    private readonly IMediator _mediator;
    private readonly IDeploymentNotificationService? _notificationService;
    private readonly INotificationService? _inAppNotificationService;
    private readonly ILogger<RetryProductHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public RetryProductHandler(
        IProductDeploymentRepository repository,
        IMediator mediator,
        ILogger<RetryProductHandler> logger,
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

    public async Task<DeployProductResponse> Handle(RetryProductCommand request, CancellationToken cancellationToken)
    {
        // 1. Load existing product deployment
        if (!Guid.TryParse(request.ProductDeploymentId, out var pdGuid))
        {
            return DeployProductResponse.Failed("Invalid product deployment ID format.");
        }

        var productDeployment = _repository.Get(ProductDeploymentId.FromGuid(pdGuid));
        if (productDeployment == null)
        {
            return DeployProductResponse.Failed("Product deployment not found.");
        }

        // 2. Validate can retry
        if (!productDeployment.CanRetry)
        {
            return DeployProductResponse.Failed(
                $"Product deployment cannot be retried. Current status: {productDeployment.Status}");
        }

        // 3. Start retry → transitions to Deploying, resets Failed stacks to Pending
        productDeployment.StartRetry();
        _repository.Update(productDeployment);
        _repository.SaveChanges();

        _logger.LogInformation(
            "Product retry {ProductDeploymentId} initiated for {ProductName} v{Version} with {FailedStacks} stacks to retry",
            productDeployment.Id, productDeployment.ProductName, productDeployment.ProductVersion,
            productDeployment.Stacks.Count(s => s.Status == StackDeploymentStatus.Pending));

        // 4. Generate session ID
        var sessionId = !string.IsNullOrEmpty(request.SessionId)
            ? request.SessionId
            : $"product-retry-{productDeployment.ProductName}-{_timeProvider.GetUtcNow():yyyyMMddHHmmssfff}";

        // 5. Deploy each pending stack sequentially (skip Running stacks)
        var stackResults = new List<DeployProductStackResult>();
        var stacks = productDeployment.GetStacksInDeployOrder();
        var aborted = false;

        for (var i = 0; i < stacks.Count; i++)
        {
            if (aborted) break;

            var stack = stacks[i];

            // Skip stacks that are already Running
            if (stack.Status == StackDeploymentStatus.Running)
            {
                stackResults.Add(new DeployProductStackResult
                {
                    StackName = stack.StackName,
                    StackDisplayName = stack.StackDisplayName,
                    Success = true,
                    DeploymentId = stack.DeploymentId?.Value.ToString(),
                    DeploymentStackName = stack.DeploymentStackName,
                    ServiceCount = stack.ServiceCount
                });
                continue;
            }

            // Send product-level progress
            await NotifyProductProgressAsync(
                sessionId, stack.StackName, stack.StackDisplayName, i, stacks.Count,
                productDeployment.CompletedStacks, cancellationToken);

            _logger.LogInformation(
                "Retrying stack {StackIndex}/{TotalStacks}: {StackName} for product {ProductName}",
                i + 1, stacks.Count, stack.StackDisplayName, productDeployment.ProductName);

            // Build variables from stored stack config
            var mergedVariables = new Dictionary<string, string>(stack.Variables);
            var stackDeploymentName = ProductDeployment.DeriveStackDeploymentName(
                productDeployment.DeploymentName, stack.StackName);

            DeployStackResponse deployResult;
            try
            {
                deployResult = await _mediator.Send(new DeployStackCommand(
                    request.EnvironmentId,
                    stack.StackId,
                    stackDeploymentName,
                    mergedVariables,
                    sessionId,
                    SuppressNotification: true), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception retrying stack {StackName}", stack.StackDisplayName);
                deployResult = DeployStackResponse.Failed(
                    $"Exception deploying stack '{stack.StackDisplayName}': {ex.Message}",
                    ex.Message);
            }

            // Update aggregate state
            var stackResult = new DeployProductStackResult
            {
                StackName = stack.StackName,
                StackDisplayName = stack.StackDisplayName,
                ServiceCount = stack.ServiceCount
            };

            if (deployResult.Success && !string.IsNullOrEmpty(deployResult.DeploymentId))
            {
                var deploymentId = new DeploymentId(Guid.Parse(deployResult.DeploymentId));
                productDeployment.StartStack(stack.StackName, deploymentId);
                productDeployment.CompleteStack(stack.StackName);

                stackResult.Success = true;
                stackResult.DeploymentId = deployResult.DeploymentId;
                stackResult.DeploymentStackName = stackDeploymentName;

                _logger.LogInformation("Stack {StackName} retried successfully", stack.StackDisplayName);
            }
            else
            {
                var error = deployResult.Message ?? "Unknown error";

                if (!string.IsNullOrEmpty(deployResult.DeploymentId))
                {
                    var deploymentId = new DeploymentId(Guid.Parse(deployResult.DeploymentId));
                    productDeployment.StartStack(stack.StackName, deploymentId);
                    productDeployment.FailStack(stack.StackName, error);
                    stackResult.DeploymentId = deployResult.DeploymentId;
                    stackResult.DeploymentStackName = stackDeploymentName;
                }
                else
                {
                    productDeployment.FailStack(stack.StackName, error);
                }

                stackResult.Success = false;
                stackResult.ErrorMessage = error;

                _logger.LogWarning("Stack {StackName} retry failed: {Error}", stack.StackDisplayName, error);

                if (!request.ContinueOnError)
                {
                    aborted = true;
                }
            }

            stackResults.Add(stackResult);

            // Persist after each stack
            _repository.Update(productDeployment);
            _repository.SaveChanges();
        }

        // 6. Finalize product status
        FinalizeProductStatus(productDeployment);
        _repository.Update(productDeployment);
        _repository.SaveChanges();

        // 7. Send final notifications
        var overallSuccess = productDeployment.Status == ProductDeploymentStatus.Running;
        await NotifyFinalResultAsync(sessionId, productDeployment, stacks.Count, cancellationToken);
        await CreateInAppNotificationAsync(productDeployment, cancellationToken);

        _logger.LogInformation(
            "Product retry {ProductDeploymentId} completed with status {Status}. {Completed}/{Total} stacks succeeded",
            productDeployment.Id, productDeployment.Status, productDeployment.CompletedStacks, stacks.Count);

        // 8. Return response
        return new DeployProductResponse
        {
            Success = overallSuccess || productDeployment.Status == ProductDeploymentStatus.PartiallyRunning,
            Message = FormatResultMessage(productDeployment),
            ProductDeploymentId = productDeployment.Id.Value.ToString(),
            ProductName = productDeployment.ProductName,
            ProductVersion = productDeployment.ProductVersion,
            Status = productDeployment.Status.ToString(),
            SessionId = sessionId,
            StackResults = stackResults
        };
    }

    private static void FinalizeProductStatus(ProductDeployment productDeployment)
    {
        if (productDeployment.Status is ProductDeploymentStatus.Running
            or ProductDeploymentStatus.PartiallyRunning
            or ProductDeploymentStatus.Failed)
        {
            return;
        }

        if (productDeployment.CompletedStacks > 0 && productDeployment.FailedStacks > 0)
        {
            productDeployment.MarkAsPartiallyRunning(
                $"{productDeployment.FailedStacks} of {productDeployment.TotalStacks} stacks failed.");
        }
        else if (productDeployment.CompletedStacks == 0 && productDeployment.FailedStacks > 0)
        {
            productDeployment.MarkAsFailed(
                $"All {productDeployment.FailedStacks} stacks failed.");
        }
        else if (productDeployment.CompletedStacks > 0 &&
                 productDeployment.Stacks.Any(s => s.Status == StackDeploymentStatus.Pending))
        {
            productDeployment.MarkAsPartiallyRunning(
                $"Retry aborted after failure. {productDeployment.CompletedStacks} of {productDeployment.TotalStacks} stacks running.");
        }
    }

    private static string FormatResultMessage(ProductDeployment pd)
    {
        return pd.Status switch
        {
            ProductDeploymentStatus.Running =>
                $"Product '{pd.ProductName}' v{pd.ProductVersion} retry succeeded ({pd.TotalStacks} stacks).",
            ProductDeploymentStatus.PartiallyRunning =>
                $"Product '{pd.ProductName}' partially running after retry. {pd.CompletedStacks}/{pd.TotalStacks} stacks running, {pd.FailedStacks} failed.",
            ProductDeploymentStatus.Failed =>
                $"Retry failed for product '{pd.ProductName}'. {pd.FailedStacks}/{pd.TotalStacks} stacks failed.",
            _ => $"Product '{pd.ProductName}' retry completed with status {pd.Status}."
        };
    }

    private async Task NotifyProductProgressAsync(
        string sessionId, string stackName, string stackDisplayName, int stackIndex, int totalStacks,
        int completedStacks, CancellationToken ct)
    {
        if (_notificationService == null) return;

        try
        {
            var percentComplete = totalStacks > 0 ? (int)(completedStacks * 100.0 / totalStacks) : 0;
            await _notificationService.NotifyProgressAsync(
                sessionId,
                "ProductDeploy",
                $"Retrying stack {stackIndex + 1}/{totalStacks}: {stackDisplayName}",
                percentComplete,
                stackName,
                totalStacks,
                completedStacks,
                0, 0, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to send product retry progress notification");
        }
    }

    private async Task NotifyFinalResultAsync(
        string sessionId, ProductDeployment pd, int totalStacks, CancellationToken ct)
    {
        if (_notificationService == null) return;

        try
        {
            if (pd.Status == ProductDeploymentStatus.Running)
            {
                await _notificationService.NotifyCompletedAsync(
                    sessionId,
                    $"Product '{pd.ProductName}' retry succeeded ({totalStacks} stacks).",
                    totalStacks, ct);
            }
            else
            {
                await _notificationService.NotifyErrorAsync(
                    sessionId,
                    FormatResultMessage(pd),
                    null, totalStacks, pd.CompletedStacks, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to send final product retry notification");
        }
    }

    private async Task CreateInAppNotificationAsync(ProductDeployment pd, CancellationToken ct)
    {
        if (_inAppNotificationService == null) return;

        try
        {
            var success = pd.Status == ProductDeploymentStatus.Running;
            var notification = NotificationFactory.CreateProductDeploymentResult(
                success, "retry", pd.ProductName, pd.ProductVersion,
                pd.TotalStacks, pd.CompletedStacks, pd.FailedStacks,
                productDeploymentId: pd.Id.Value.ToString());

            await _inAppNotificationService.AddAsync(notification, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to create in-app notification for product retry");
        }
    }
}
