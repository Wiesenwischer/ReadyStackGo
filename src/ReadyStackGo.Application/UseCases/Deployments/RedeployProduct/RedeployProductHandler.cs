using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Notifications;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.DeployProduct;
using ReadyStackGo.Application.UseCases.Deployments.DeployStack;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.UseCases.Deployments.RedeployProduct;

/// <summary>
/// Orchestrates redeploying all or selected stacks of a running product deployment.
/// Uses same version with fresh image pull. Supports variable overrides merged on top of stored values.
/// </summary>
public class RedeployProductHandler : IRequestHandler<RedeployProductCommand, DeployProductResponse>
{
    private readonly IProductDeploymentRepository _repository;
    private readonly IMediator _mediator;
    private readonly IDeploymentService _deploymentService;
    private readonly IDeploymentNotificationService? _notificationService;
    private readonly INotificationService? _inAppNotificationService;
    private readonly ILogger<RedeployProductHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public RedeployProductHandler(
        IProductDeploymentRepository repository,
        IMediator mediator,
        IDeploymentService deploymentService,
        ILogger<RedeployProductHandler> logger,
        IDeploymentNotificationService? notificationService = null,
        INotificationService? inAppNotificationService = null,
        TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _mediator = mediator;
        _deploymentService = deploymentService;
        _logger = logger;
        _notificationService = notificationService;
        _inAppNotificationService = inAppNotificationService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<DeployProductResponse> Handle(RedeployProductCommand request, CancellationToken cancellationToken)
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

        // 2. Validate can redeploy
        if (!productDeployment.CanRedeploy)
        {
            return DeployProductResponse.Failed(
                $"Product deployment cannot be redeployed. Current status: {productDeployment.Status}");
        }

        // 3. Start redeploy → transitions to Redeploying, resets targeted stacks to Pending
        try
        {
            productDeployment.StartRedeploy(request.StackNames);
        }
        catch (InvalidOperationException ex)
        {
            return DeployProductResponse.Failed(ex.Message);
        }

        _repository.Update(productDeployment);
        _repository.SaveChanges();

        var pendingStacks = productDeployment.Stacks.Count(s => s.Status == StackDeploymentStatus.Pending);
        _logger.LogInformation(
            "Product redeploy {ProductDeploymentId} initiated for {ProductName} v{Version} with {PendingStacks} stacks to redeploy",
            productDeployment.Id, productDeployment.ProductName, productDeployment.ProductVersion, pendingStacks);

        // 4. Generate session ID
        var sessionId = !string.IsNullOrEmpty(request.SessionId)
            ? request.SessionId
            : $"product-redeploy-{productDeployment.ProductName}-{_timeProvider.GetUtcNow():yyyyMMddHHmmssfff}";

        // 5. Deploy each pending stack sequentially (skip Running stacks)
        var stackResults = new List<DeployProductStackResult>();
        var stacks = productDeployment.GetStacksInDeployOrder();
        var aborted = false;

        for (var i = 0; i < stacks.Count; i++)
        {
            if (aborted) break;

            var stack = stacks[i];

            // Skip stacks that are already Running (not selected for redeploy)
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

            _logger.LogInformation(
                "Redeploying stack {StackIndex}/{TotalStacks}: {StackName} for product {ProductName}",
                i + 1, stacks.Count, stack.StackDisplayName, productDeployment.ProductName);

            // Build variables: stored stack config as base + optional overrides
            var mergedVariables = new Dictionary<string, string>(stack.Variables);
            if (request.VariableOverrides is { Count: > 0 })
            {
                foreach (var kvp in request.VariableOverrides)
                {
                    mergedVariables[kvp.Key] = kvp.Value;
                }
            }

            var stackDeploymentName = ProductDeployment.DeriveStackDeploymentName(
                productDeployment.DeploymentName, stack.StackName);

            // Remove the existing deployment first, then deploy fresh.
            // Redeploy = remove old + deploy new.
            await NotifyProductProgressAsync(
                sessionId, stack.StackName, stack.StackDisplayName, i, stacks.Count,
                productDeployment.CompletedStacks, cancellationToken,
                phase: "Removing");

            var removeResult = await _deploymentService.RemoveDeploymentAsync(
                request.EnvironmentId, stackDeploymentName);
            if (!removeResult.Success)
            {
                _logger.LogWarning(
                    "Remove of stack '{StackName}' before redeploy failed ({Error}) — continuing with fresh deploy",
                    stackDeploymentName, removeResult.Message);
                await _deploymentService.MarkDeploymentAsRemovedAsync(
                    request.EnvironmentId, stackDeploymentName);
            }

            // Transition UI from 'removing' → 'deploying'
            await NotifyProductProgressAsync(
                sessionId, stack.StackName, stack.StackDisplayName, i, stacks.Count,
                productDeployment.CompletedStacks, cancellationToken);

            DeployStackResponse deployResult;
            try
            {
                deployResult = await _mediator.Send(new DeployStackCommand(
                    request.EnvironmentId,
                    stack.StackId,
                    stackDeploymentName,
                    mergedVariables,
                    sessionId,
                    SuppressNotification: false), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception redeploying stack {StackName}", stack.StackDisplayName);
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

                _logger.LogInformation("Stack {StackName} redeployed successfully", stack.StackDisplayName);

                await NotifyStackCompletedAsync(
                    sessionId, stack.StackName, stack.StackDisplayName,
                    true, null, i, stacks.Count, productDeployment.CompletedStacks, cancellationToken);
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

                _logger.LogWarning("Stack {StackName} redeploy failed: {Error}", stack.StackDisplayName, error);

                await NotifyStackCompletedAsync(
                    sessionId, stack.StackName, stack.StackDisplayName,
                    false, error, i, stacks.Count, productDeployment.CompletedStacks, cancellationToken);

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
        await NotifyFinalResultAsync(sessionId, productDeployment, stacks.Count, cancellationToken);
        await CreateInAppNotificationAsync(productDeployment, cancellationToken);

        _logger.LogInformation(
            "Product redeploy {ProductDeploymentId} completed with status {Status}. {Completed}/{Total} stacks succeeded",
            productDeployment.Id, productDeployment.Status, productDeployment.CompletedStacks, stacks.Count);

        // 8. Return response
        return new DeployProductResponse
        {
            Success = productDeployment.Status == ProductDeploymentStatus.Running
                      || productDeployment.Status == ProductDeploymentStatus.PartiallyRunning,
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
                $"Redeploy aborted after failure. {productDeployment.CompletedStacks} of {productDeployment.TotalStacks} stacks running.");
        }
    }

    private static string FormatResultMessage(ProductDeployment pd)
    {
        return pd.Status switch
        {
            ProductDeploymentStatus.Running =>
                $"Product '{pd.ProductName}' v{pd.ProductVersion} redeploy succeeded ({pd.TotalStacks} stacks).",
            ProductDeploymentStatus.PartiallyRunning =>
                $"Product '{pd.ProductName}' partially running after redeploy. {pd.CompletedStacks}/{pd.TotalStacks} stacks running, {pd.FailedStacks} failed.",
            ProductDeploymentStatus.Failed =>
                $"Redeploy failed for product '{pd.ProductName}'. {pd.FailedStacks}/{pd.TotalStacks} stacks failed.",
            _ => $"Product '{pd.ProductName}' redeploy completed with status {pd.Status}."
        };
    }

    private async Task NotifyProductProgressAsync(
        string sessionId, string stackName, string stackDisplayName, int stackIndex, int totalStacks,
        int completedStacks, CancellationToken ct, string phase = "Redeploying")
    {
        if (_notificationService == null) return;

        try
        {
            var percentComplete = totalStacks > 0 ? (int)(completedStacks * 100.0 / totalStacks) : 0;
            var message = phase == "Removing"
                ? $"Removing stack {stackIndex + 1}/{totalStacks}: {stackDisplayName}"
                : $"Redeploying stack {stackIndex + 1}/{totalStacks}: {stackDisplayName}";
            await _notificationService.NotifyProgressAsync(
                sessionId,
                "ProductDeploy",
                message,
                percentComplete,
                stackName,
                totalStacks,
                completedStacks,
                0, 0, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to send product redeploy progress notification");
        }
    }

    private async Task NotifyStackCompletedAsync(
        string sessionId, string stackName, string stackDisplayName,
        bool success, string? error, int stackIndex, int totalStacks,
        int completedStacks, CancellationToken ct)
    {
        if (_notificationService == null) return;

        try
        {
            var percentComplete = totalStacks > 0 ? (int)(completedStacks * 100.0 / totalStacks) : 0;
            var message = success
                ? $"Stack {stackDisplayName} redeployed successfully"
                : $"Stack {stackDisplayName} redeploy failed: {error}";
            await _notificationService.NotifyProgressAsync(
                sessionId,
                "ProductDeploy",
                message,
                percentComplete,
                stackName,
                totalStacks,
                completedStacks,
                0, 0, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to send stack completion notification");
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
                    $"Product '{pd.ProductName}' redeploy succeeded ({totalStacks} stacks).",
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
            _logger.LogDebug(ex, "Failed to send final product redeploy notification");
        }
    }

    private async Task CreateInAppNotificationAsync(ProductDeployment pd, CancellationToken ct)
    {
        if (_inAppNotificationService == null) return;

        try
        {
            var success = pd.Status == ProductDeploymentStatus.Running;
            var notification = NotificationFactory.CreateProductDeploymentResult(
                success, "redeploy", pd.ProductName, pd.ProductVersion,
                pd.TotalStacks, pd.CompletedStacks, pd.FailedStacks,
                productDeploymentId: pd.Id.Value.ToString());

            await _inAppNotificationService.AddAsync(notification, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to create in-app notification for product redeploy");
        }
    }
}
