using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Notifications;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.DeployStack;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.UseCases.Deployments.DeployProduct;

/// <summary>
/// Orchestrates deploying all stacks of a product as a single unit.
/// Deploys stacks sequentially in manifest order, with configurable error handling.
/// </summary>
public class DeployProductHandler : IRequestHandler<DeployProductCommand, DeployProductResponse>
{
    private readonly IProductSourceService _productSourceService;
    private readonly IProductDeploymentRepository _repository;
    private readonly IMediator _mediator;
    private readonly IDeploymentNotificationService? _notificationService;
    private readonly INotificationService? _inAppNotificationService;
    private readonly ILogger<DeployProductHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public DeployProductHandler(
        IProductSourceService productSourceService,
        IProductDeploymentRepository repository,
        IMediator mediator,
        ILogger<DeployProductHandler> logger,
        IDeploymentNotificationService? notificationService = null,
        INotificationService? inAppNotificationService = null,
        TimeProvider? timeProvider = null)
    {
        _productSourceService = productSourceService;
        _repository = repository;
        _mediator = mediator;
        _logger = logger;
        _notificationService = notificationService;
        _inAppNotificationService = inAppNotificationService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<DeployProductResponse> Handle(DeployProductCommand request, CancellationToken cancellationToken)
    {
        // 1. Load product from catalog
        var product = await _productSourceService.GetProductAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            return DeployProductResponse.Failed($"Product '{request.ProductId}' not found in catalog.");
        }

        // 2. Check for existing active deployment
        var environmentId = new EnvironmentId(Guid.Parse(request.EnvironmentId));
        var existing = _repository.GetActiveByProductGroupId(environmentId, product.GroupId);
        if (existing != null)
        {
            if (existing.IsInProgress)
            {
                return DeployProductResponse.Failed(
                    $"A deployment is already in progress for product '{product.Name}'.");
            }

            if (existing.IsOperational)
            {
                return DeployProductResponse.Failed(
                    $"Product '{product.Name}' is already deployed (status: {existing.Status}). Use upgrade instead.");
            }
        }

        // 3. Validate stack configs
        if (request.StackConfigs.Count == 0)
        {
            return DeployProductResponse.Failed("At least one stack configuration is required.");
        }

        // 4. Build StackDeploymentConfig array from request + catalog data
        var stackConfigs = new List<StackDeploymentConfig>();
        foreach (var reqStack in request.StackConfigs)
        {
            var stackDef = product.Stacks.FirstOrDefault(s =>
                s.Id.Value.Equals(reqStack.StackId, StringComparison.OrdinalIgnoreCase));

            if (stackDef == null)
            {
                return DeployProductResponse.Failed(
                    $"Stack '{reqStack.StackId}' not found in product '{product.Name}'.");
            }

            var mergedVariables = MergeVariables(stackDef, request.SharedVariables, reqStack.Variables);

            stackConfigs.Add(new StackDeploymentConfig(
                reqStack.DeploymentStackName,
                stackDef.Name,
                reqStack.StackId,
                stackDef.Services.Count,
                mergedVariables));
        }

        // 5. Create ProductDeployment aggregate
        var deployedBy = !string.IsNullOrEmpty(request.UserId) && Guid.TryParse(request.UserId, out var userGuid)
            ? Domain.Deployment.UserId.FromGuid(userGuid)
            : Domain.Deployment.UserId.Create();

        var productDeploymentId = _repository.NextIdentity();
        var productDeployment = ProductDeployment.InitiateDeployment(
            productDeploymentId,
            environmentId,
            product.GroupId,
            product.Id,
            product.Name,
            product.DisplayName,
            product.ProductVersion ?? "unknown",
            deployedBy,
            stackConfigs,
            request.SharedVariables,
            request.ContinueOnError);

        _repository.Add(productDeployment);
        _repository.SaveChanges();

        _logger.LogInformation(
            "Product deployment {ProductDeploymentId} initiated for {ProductName} v{ProductVersion} with {StackCount} stacks",
            productDeploymentId, product.Name, product.ProductVersion, stackConfigs.Count);

        // 6. Generate session ID
        var sessionId = !string.IsNullOrEmpty(request.SessionId)
            ? request.SessionId
            : $"product-{product.Name}-{_timeProvider.GetUtcNow():yyyyMMddHHmmssfff}";

        // 7. Deploy each stack sequentially
        var stackResults = new List<DeployProductStackResult>();
        var stacks = productDeployment.GetStacksInDeployOrder();
        var aborted = false;

        for (var i = 0; i < stacks.Count; i++)
        {
            if (aborted) break;

            var stack = stacks[i];
            var reqStack = request.StackConfigs.First(s =>
                s.StackId.Equals(stack.StackId, StringComparison.OrdinalIgnoreCase));

            // Send product-level progress
            await NotifyProductProgressAsync(
                sessionId, stack.StackDisplayName, i, stacks.Count,
                productDeployment.CompletedStacks, cancellationToken);

            _logger.LogInformation(
                "Deploying stack {StackIndex}/{TotalStacks}: {StackName} for product {ProductName}",
                i + 1, stacks.Count, stack.StackDisplayName, product.Name);

            // Dispatch DeployStackCommand
            var mergedVariables = MergeVariables(
                product.Stacks.First(s => s.Id.Value.Equals(stack.StackId, StringComparison.OrdinalIgnoreCase)),
                request.SharedVariables,
                reqStack.Variables);

            DeployStackResponse deployResult;
            try
            {
                deployResult = await _mediator.Send(new DeployStackCommand(
                    request.EnvironmentId,
                    stack.StackId,
                    reqStack.DeploymentStackName,
                    new Dictionary<string, string>(mergedVariables),
                    sessionId,
                    SuppressNotification: true), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception deploying stack {StackName}", stack.StackDisplayName);
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
                productDeployment.StartStack(stack.StackName, deploymentId, reqStack.DeploymentStackName);
                productDeployment.CompleteStack(stack.StackName);

                stackResult.Success = true;
                stackResult.DeploymentId = deployResult.DeploymentId;
                stackResult.DeploymentStackName = reqStack.DeploymentStackName;

                _logger.LogInformation("Stack {StackName} deployed successfully", stack.StackDisplayName);
            }
            else
            {
                var error = deployResult.Message ?? "Unknown error";

                if (!string.IsNullOrEmpty(deployResult.DeploymentId))
                {
                    // Deployment was created but failed
                    var deploymentId = new DeploymentId(Guid.Parse(deployResult.DeploymentId));
                    productDeployment.StartStack(stack.StackName, deploymentId, reqStack.DeploymentStackName);
                    productDeployment.FailStack(stack.StackName, error);
                    stackResult.DeploymentId = deployResult.DeploymentId;
                    stackResult.DeploymentStackName = reqStack.DeploymentStackName;
                }
                else
                {
                    // Pre-deployment failure (no Deployment entity created)
                    productDeployment.FailStack(stack.StackName, error);
                }

                stackResult.Success = false;
                stackResult.ErrorMessage = error;

                _logger.LogWarning("Stack {StackName} failed: {Error}", stack.StackDisplayName, error);

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

        // 8. Finalize product status
        FinalizeProductStatus(productDeployment);
        _repository.Update(productDeployment);
        _repository.SaveChanges();

        // 9. Send final notifications
        var overallSuccess = productDeployment.Status == ProductDeploymentStatus.Running;
        var isPartial = productDeployment.Status == ProductDeploymentStatus.PartiallyRunning;

        await NotifyFinalResultAsync(sessionId, productDeployment, stacks.Count, cancellationToken);
        await CreateInAppNotificationAsync(productDeployment, cancellationToken);

        _logger.LogInformation(
            "Product deployment {ProductDeploymentId} completed with status {Status}. {Completed}/{Total} stacks succeeded",
            productDeploymentId, productDeployment.Status, productDeployment.CompletedStacks, stacks.Count);

        // 10. Return response
        return new DeployProductResponse
        {
            Success = overallSuccess || isPartial,
            Message = FormatResultMessage(productDeployment),
            ProductDeploymentId = productDeploymentId.Value.ToString(),
            ProductName = product.Name,
            ProductVersion = product.ProductVersion,
            Status = productDeployment.Status.ToString(),
            SessionId = sessionId,
            StackResults = stackResults
        };
    }

    private static Dictionary<string, string> MergeVariables(
        Domain.StackManagement.Stacks.StackDefinition stackDef,
        Dictionary<string, string> sharedVariables,
        Dictionary<string, string> perStackVariables)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 1. Stack definition defaults
        foreach (var variable in stackDef.Variables)
        {
            if (!string.IsNullOrEmpty(variable.DefaultValue))
            {
                merged[variable.Name] = variable.DefaultValue;
            }
        }

        // 2. Shared variables (product-level)
        foreach (var kvp in sharedVariables)
        {
            merged[kvp.Key] = kvp.Value;
        }

        // 3. Per-stack overrides
        foreach (var kvp in perStackVariables)
        {
            merged[kvp.Key] = kvp.Value;
        }

        return merged;
    }

    private static void FinalizeProductStatus(ProductDeployment productDeployment)
    {
        // If all stacks completed, status is already Running (set by CompleteStack)
        if (productDeployment.Status is ProductDeploymentStatus.Running
            or ProductDeploymentStatus.PartiallyRunning
            or ProductDeploymentStatus.Failed)
        {
            return;
        }

        // Still in Deploying status — determine final state
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
                 productDeployment.Stacks.Any(s => s.Status == Domain.Deployment.ProductDeployments.StackDeploymentStatus.Pending))
        {
            // Some succeeded, some still pending (aborted due to ContinueOnError=false)
            productDeployment.MarkAsPartiallyRunning(
                $"Deployment aborted after failure. {productDeployment.CompletedStacks} of {productDeployment.TotalStacks} stacks running.");
        }
    }

    private static string FormatResultMessage(ProductDeployment pd)
    {
        return pd.Status switch
        {
            ProductDeploymentStatus.Running =>
                $"Product '{pd.ProductName}' v{pd.ProductVersion} deployed successfully ({pd.TotalStacks} stacks).",
            ProductDeploymentStatus.PartiallyRunning =>
                $"Product '{pd.ProductName}' partially deployed. {pd.CompletedStacks}/{pd.TotalStacks} stacks running, {pd.FailedStacks} failed.",
            ProductDeploymentStatus.Failed =>
                $"Failed to deploy product '{pd.ProductName}'. {pd.FailedStacks}/{pd.TotalStacks} stacks failed.",
            _ => $"Product '{pd.ProductName}' deployment completed with status {pd.Status}."
        };
    }

    private async Task NotifyProductProgressAsync(
        string sessionId, string stackDisplayName, int stackIndex, int totalStacks,
        int completedStacks, CancellationToken ct)
    {
        if (_notificationService == null) return;

        try
        {
            var percentComplete = totalStacks > 0 ? (int)(completedStacks * 100.0 / totalStacks) : 0;
            await _notificationService.NotifyProgressAsync(
                sessionId,
                "ProductDeploy",
                $"Deploying stack {stackIndex + 1}/{totalStacks}: {stackDisplayName}",
                percentComplete,
                stackDisplayName,
                totalStacks,
                completedStacks,
                0, 0, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to send product progress notification");
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
                    $"Product '{pd.ProductName}' deployed successfully ({totalStacks} stacks).",
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
            _logger.LogDebug(ex, "Failed to send final product notification");
        }
    }

    private async Task CreateInAppNotificationAsync(ProductDeployment pd, CancellationToken ct)
    {
        if (_inAppNotificationService == null) return;

        try
        {
            var success = pd.Status == ProductDeploymentStatus.Running;
            var notification = NotificationFactory.CreateProductDeploymentResult(
                success, "deploy", pd.ProductName, pd.ProductVersion,
                pd.TotalStacks, pd.CompletedStacks, pd.FailedStacks,
                productDeploymentId: pd.Id.Value.ToString());

            await _inAppNotificationService.AddAsync(notification, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to create in-app notification for product deployment");
        }
    }
}
