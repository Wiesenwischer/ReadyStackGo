using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Notifications;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.DeployStack;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.UseCases.Deployments.UpgradeProduct;

/// <summary>
/// Orchestrates upgrading all stacks of a product deployment to a new version.
/// Existing stacks are upgraded and new stacks in the target version are deployed fresh.
/// </summary>
public class UpgradeProductHandler : IRequestHandler<UpgradeProductCommand, UpgradeProductResponse>
{
    private readonly IProductSourceService _productSourceService;
    private readonly IProductDeploymentRepository _repository;
    private readonly IMediator _mediator;
    private readonly IDeploymentService _deploymentService;
    private readonly IDeploymentNotificationService? _notificationService;
    private readonly INotificationService? _inAppNotificationService;
    private readonly ILogger<UpgradeProductHandler> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly Application.Services.Edge.IEdgeBundleReader? _edgeBundleReader;

    public UpgradeProductHandler(
        IProductSourceService productSourceService,
        IProductDeploymentRepository repository,
        IMediator mediator,
        IDeploymentService deploymentService,
        ILogger<UpgradeProductHandler> logger,
        IDeploymentNotificationService? notificationService = null,
        INotificationService? inAppNotificationService = null,
        TimeProvider? timeProvider = null,
        Application.Services.Edge.IEdgeBundleReader? edgeBundleReader = null)
    {
        _productSourceService = productSourceService;
        _repository = repository;
        _mediator = mediator;
        _deploymentService = deploymentService;
        _logger = logger;
        _notificationService = notificationService;
        _inAppNotificationService = inAppNotificationService;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _edgeBundleReader = edgeBundleReader;
    }

    public async Task<UpgradeProductResponse> Handle(UpgradeProductCommand request, CancellationToken cancellationToken)
    {
        // 1. Load existing product deployment
        if (!Guid.TryParse(request.ProductDeploymentId, out var pdGuid))
        {
            return UpgradeProductResponse.Failed("Invalid product deployment ID format.");
        }

        var existing = _repository.Get(ProductDeploymentId.FromGuid(pdGuid));
        if (existing == null)
        {
            return UpgradeProductResponse.Failed("Product deployment not found.");
        }

        // 2. Validate can upgrade
        if (!existing.CanUpgrade)
        {
            return UpgradeProductResponse.Failed(
                $"Product deployment cannot be upgraded. Current status: {existing.Status}");
        }

        // 3. Load target product from catalog
        var targetProduct = await _productSourceService.GetProductAsync(request.TargetProductId, cancellationToken);
        if (targetProduct == null)
        {
            return UpgradeProductResponse.Failed(
                $"Target product '{request.TargetProductId}' not found in catalog.");
        }

        // 4. Version comparison — prevent downgrade/same-version
        var previousVersion = existing.ProductVersion;
        var targetVersion = targetProduct.ProductVersion ?? "unknown";

        var comparison = CompareVersions(previousVersion, targetVersion);
        if (comparison.HasValue)
        {
            if (comparison.Value == 0)
            {
                return UpgradeProductResponse.Failed(
                    $"Product is already running version {previousVersion}.");
            }
            if (comparison.Value > 0)
            {
                return UpgradeProductResponse.Failed(
                    $"Downgrade from {previousVersion} to {targetVersion} is not supported.");
            }
        }

        // 5. Validate stack configs
        if (request.StackConfigs.Count == 0)
        {
            return UpgradeProductResponse.Failed("At least one stack configuration is required.");
        }

        // 6. Build StackDeploymentConfig array, matching to existing stacks
        var deploymentName = existing.DeploymentName;
        var existingStackLookup = existing.Stacks
            .ToDictionary(s => s.StackName, s => s, StringComparer.OrdinalIgnoreCase);

        var stackConfigs = new List<StackDeploymentConfig>();

        foreach (var reqStack in request.StackConfigs)
        {
            var stackDef = targetProduct.Stacks.FirstOrDefault(s =>
                s.Id.Value.Equals(reqStack.StackId, StringComparison.OrdinalIgnoreCase));

            if (stackDef == null)
            {
                return UpgradeProductResponse.Failed(
                    $"Stack '{reqStack.StackId}' not found in target product '{targetProduct.Name}'.");
            }

            // Merge variables: stack defaults < existing values < shared overrides < per-stack overrides
            var existingVariables = existingStackLookup.TryGetValue(stackDef.Name, out var existingStack)
                ? existingStack.Variables
                : (IReadOnlyDictionary<string, string>?)null;

            var mergedVariables = MergeVariables(stackDef, existingVariables, request.SharedVariables, reqStack.Variables);

            stackConfigs.Add(new StackDeploymentConfig(
                stackDef.Name,
                stackDef.Name,
                reqStack.StackId,
                stackDef.Services.Count,
                mergedVariables));
        }

        // Remove stacks that exist in the current deployment but are not in the target version.
        // A rename or removal of a stack means the old deployment is obsolete: remove it (Docker + DB).
        var targetStackNames = stackConfigs.Select(s => s.StackName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stacksToRemove = existing.Stacks
            .Where(s => !targetStackNames.Contains(s.StackName))
            .ToList();

        foreach (var oldStack in stacksToRemove)
        {
            _logger.LogInformation(
                "Stack '{StackName}' no longer in target version — removing as part of upgrade",
                oldStack.StackName);
            try
            {
                var removeResult = await _deploymentService.RemoveDeploymentAsync(
                    request.EnvironmentId, oldStack.StackName);

                if (!removeResult.Success)
                {
                    _logger.LogWarning(
                        "Docker removal of old stack '{StackName}' failed ({Error}) — marking as removed in DB only",
                        oldStack.StackName, removeResult.Message);
                    await _deploymentService.MarkDeploymentAsRemovedAsync(
                        request.EnvironmentId, oldStack.StackName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Exception removing old stack '{StackName}' — marking as removed in DB only",
                    oldStack.StackName);
                await _deploymentService.MarkDeploymentAsRemovedAsync(
                    request.EnvironmentId, oldStack.StackName);
            }
        }

        // 7. Create new ProductDeployment via InitiateUpgrade
        var deployedBy = !string.IsNullOrEmpty(request.UserId) && Guid.TryParse(request.UserId, out var userGuid)
            ? Domain.Deployment.UserId.FromGuid(userGuid)
            : Domain.Deployment.UserId.Create();

        var newDeploymentId = _repository.NextIdentity();
        var environmentId = new EnvironmentId(Guid.Parse(request.EnvironmentId));

        var productDeployment = ProductDeployment.InitiateUpgrade(
            newDeploymentId,
            environmentId,
            targetProduct.GroupId,
            targetProduct.Id,
            targetProduct.Name,
            targetProduct.DisplayName,
            targetVersion,
            deployedBy,
            deploymentName,
            existing,
            stackConfigs,
            request.SharedVariables,
            request.ContinueOnError);

        // Carry the optional edge config forward onto the successor aggregate so the
        // managed edge keeps reconciling across the upgrade. Null = feature inert.
        string? edgeBundleHtml = null;
        if (_edgeBundleReader != null &&
            string.Equals(targetProduct.Edge?.MaintenancePage?.Mode, "bundle", StringComparison.OrdinalIgnoreCase))
        {
            edgeBundleHtml = await _edgeBundleReader.ReadBundleHtmlAsync(
                targetProduct.FilePath, targetProduct.Edge!.MaintenancePage!.BundlePath, cancellationToken);
        }
        var edgeConfig = EdgeConfigMapper.Map(targetProduct.Edge, request.SharedVariables, edgeBundleHtml);
        if (edgeConfig != null)
        {
            productDeployment.SetEdgeConfig(edgeConfig);
        }

        _repository.Add(productDeployment);

        // Mark the old aggregate as superseded so it no longer appears as an active
        // product deployment. Docker resources stay live under the same stack names
        // — they belong to the new aggregate now. Without this the old row would
        // linger in the dashboard with stale DeploymentId references and surface as
        // a "0 containers" zombie after the new deployment is removed.
        existing.MarkSuperseded(newDeploymentId);
        _repository.Update(existing);
        _repository.SaveChanges();

        _logger.LogInformation(
            "Product upgrade {ProductDeploymentId} initiated for {ProductName} from v{PreviousVersion} to v{TargetVersion} with {StackCount} stacks (superseded {OldId})",
            newDeploymentId, targetProduct.Name, previousVersion, targetVersion, stackConfigs.Count, existing.Id);

        // 8. Generate session ID
        var sessionId = !string.IsNullOrEmpty(request.SessionId)
            ? request.SessionId
            : $"product-upgrade-{targetProduct.Name}-{_timeProvider.GetUtcNow():yyyyMMddHHmmssfff}";

        // 9. Deploy/upgrade each stack sequentially
        var stackResults = new List<UpgradeProductStackResult>();
        var stacks = productDeployment.GetStacksInDeployOrder();
        var aborted = false;

        for (var i = 0; i < stacks.Count; i++)
        {
            if (aborted) break;

            var stack = stacks[i];
            var reqStack = request.StackConfigs.First(s =>
                s.StackId.Equals(stack.StackId, StringComparison.OrdinalIgnoreCase));

            // Merge variables for this stack
            var stackDef = targetProduct.Stacks.First(s =>
                s.Id.Value.Equals(stack.StackId, StringComparison.OrdinalIgnoreCase));

            var existingVariables = existingStackLookup.TryGetValue(stackDef.Name, out var existingStackEntry)
                ? existingStackEntry.Variables
                : null;

            var mergedVariables = MergeVariables(stackDef, existingVariables, request.SharedVariables, reqStack.Variables);
            var stackDeploymentName = ProductDeployment.DeriveStackDeploymentName(
                deploymentName, stackDef.Name);

            // Phase A — Remove the existing stack first, then deploy fresh.
            // Upgrade = remove old + deploy new (same pattern as Redeploy), so the UI
            // shows the same detailed Removing → Deploying steps per stack. Stacks
            // newly introduced in the target version have nothing to remove → deploy only.
            if (!stack.IsNewInUpgrade)
            {
                await NotifyProductProgressAsync(
                    sessionId, stack.StackName, stack.StackDisplayName, i, stacks.Count,
                    productDeployment.CompletedStacks, cancellationToken, phase: "Removing");

                var removeResult = await _deploymentService.RemoveDeploymentAsync(
                    request.EnvironmentId, stackDeploymentName);
                if (!removeResult.Success)
                {
                    _logger.LogWarning(
                        "Remove of stack '{StackName}' before upgrade failed ({Error}) — continuing with fresh deploy",
                        stackDeploymentName, removeResult.Message);
                    await _deploymentService.MarkDeploymentAsRemovedAsync(
                        request.EnvironmentId, stackDeploymentName);
                }
            }

            // Phase B — Deploy the new version
            await NotifyProductProgressAsync(
                sessionId, stack.StackName, stack.StackDisplayName, i, stacks.Count,
                productDeployment.CompletedStacks, cancellationToken, phase: "Upgrading");

            _logger.LogInformation(
                "Upgrading stack {StackIndex}/{TotalStacks}: {StackName} for product {ProductName} (isNew: {IsNew})",
                i + 1, stacks.Count, stack.StackDisplayName, targetProduct.Name, stack.IsNewInUpgrade);

            // Dispatch DeployStackCommand (handles both fresh deploy and upgrade)
            DeployStackResponse deployResult;
            try
            {
                deployResult = await _mediator.Send(new DeployStackCommand(
                    request.EnvironmentId,
                    stack.StackId,
                    stackDeploymentName,
                    new Dictionary<string, string>(mergedVariables),
                    sessionId,
                    SuppressNotification: true), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception upgrading stack {StackName}", stack.StackDisplayName);
                deployResult = DeployStackResponse.Failed(
                    $"Exception upgrading stack '{stack.StackDisplayName}': {ex.Message}",
                    ex.Message);
            }

            // Update aggregate state
            var stackResult = new UpgradeProductStackResult
            {
                StackName = stack.StackName,
                StackDisplayName = stack.StackDisplayName,
                ServiceCount = stack.ServiceCount,
                IsNewInUpgrade = stack.IsNewInUpgrade
            };

            if (deployResult.Success && !string.IsNullOrEmpty(deployResult.DeploymentId))
            {
                var deploymentId = new DeploymentId(Guid.Parse(deployResult.DeploymentId));
                productDeployment.StartStack(stack.StackName, deploymentId);
                productDeployment.CompleteStack(stack.StackName);

                stackResult.Success = true;
                stackResult.DeploymentId = deployResult.DeploymentId;
                stackResult.DeploymentStackName = stackDeploymentName;

                _logger.LogInformation("Stack {StackName} upgraded successfully", stack.StackDisplayName);

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

                _logger.LogWarning("Stack {StackName} upgrade failed: {Error}", stack.StackDisplayName, error);

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

        // 10. Finalize product status
        FinalizeProductStatus(productDeployment);
        _repository.Update(productDeployment);
        _repository.SaveChanges();

        // 11. Send final notifications
        var overallSuccess = productDeployment.Status == ProductDeploymentStatus.Running;
        var isPartial = productDeployment.Status == ProductDeploymentStatus.PartiallyRunning;

        await NotifyFinalResultAsync(sessionId, productDeployment, stacks.Count, cancellationToken);
        await CreateInAppNotificationAsync(productDeployment, cancellationToken);

        _logger.LogInformation(
            "Product upgrade {ProductDeploymentId} completed with status {Status}. {Completed}/{Total} stacks succeeded",
            newDeploymentId, productDeployment.Status, productDeployment.CompletedStacks, stacks.Count);

        return new UpgradeProductResponse
        {
            Success = overallSuccess || isPartial,
            Message = FormatResultMessage(productDeployment),
            ProductDeploymentId = newDeploymentId.Value.ToString(),
            ProductName = targetProduct.Name,
            PreviousVersion = previousVersion,
            NewVersion = targetVersion,
            Status = productDeployment.Status.ToString(),
            SessionId = sessionId,
            StackResults = stackResults,
            Warnings = null
        };
    }

    /// <summary>
    /// Merges variables with 4-tier priority for existing stacks, 3-tier for new stacks.
    /// Priority: Stack defaults &lt; Existing deployment values &lt; Shared overrides &lt; Per-stack overrides
    /// </summary>
    internal static Dictionary<string, string> MergeVariables(
        Domain.StackManagement.Stacks.StackDefinition stackDef,
        IReadOnlyDictionary<string, string>? existingVariables,
        Dictionary<string, string> sharedVariables,
        Dictionary<string, string> perStackVariables)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 1. Stack definition defaults (lowest priority)
        foreach (var variable in stackDef.Variables)
        {
            if (!string.IsNullOrEmpty(variable.DefaultValue))
            {
                merged[variable.Name] = variable.DefaultValue;
            }
        }

        // 2. Existing deployment values (preserve user-configured values)
        if (existingVariables != null)
        {
            foreach (var kvp in existingVariables)
            {
                merged[kvp.Key] = kvp.Value;
            }
        }

        // 3. Shared variables (product-level overrides)
        foreach (var kvp in sharedVariables)
        {
            merged[kvp.Key] = kvp.Value;
        }

        // 4. Per-stack overrides (highest priority)
        foreach (var kvp in perStackVariables)
        {
            merged[kvp.Key] = kvp.Value;
        }

        return merged;
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
                $"{productDeployment.FailedStacks} of {productDeployment.TotalStacks} stacks failed during upgrade.");
        }
        else if (productDeployment.CompletedStacks == 0 && productDeployment.FailedStacks > 0)
        {
            productDeployment.MarkAsFailed(
                $"All {productDeployment.FailedStacks} stacks failed during upgrade.");
        }
        else if (productDeployment.CompletedStacks > 0 &&
                 productDeployment.Stacks.Any(s => s.Status == StackDeploymentStatus.Pending))
        {
            productDeployment.MarkAsPartiallyRunning(
                $"Upgrade aborted after failure. {productDeployment.CompletedStacks} of {productDeployment.TotalStacks} stacks upgraded.");
        }
    }

    private static string FormatResultMessage(ProductDeployment pd)
    {
        return pd.Status switch
        {
            ProductDeploymentStatus.Running =>
                $"Product '{pd.ProductName}' upgraded to v{pd.ProductVersion} successfully ({pd.TotalStacks} stacks).",
            ProductDeploymentStatus.PartiallyRunning =>
                $"Product '{pd.ProductName}' partially upgraded. {pd.CompletedStacks}/{pd.TotalStacks} stacks succeeded, {pd.FailedStacks} failed.",
            ProductDeploymentStatus.Failed =>
                $"Failed to upgrade product '{pd.ProductName}'. {pd.FailedStacks}/{pd.TotalStacks} stacks failed.",
            _ => $"Product '{pd.ProductName}' upgrade completed with status {pd.Status}."
        };
    }

    private async Task NotifyProductProgressAsync(
        string sessionId, string stackName, string stackDisplayName, int stackIndex, int totalStacks,
        int completedStacks, CancellationToken ct, string phase = "Upgrading")
    {
        if (_notificationService == null) return;

        try
        {
            var percentComplete = totalStacks > 0 ? (int)(completedStacks * 100.0 / totalStacks) : 0;
            var message = phase == "Removing"
                ? $"Removing stack {stackIndex + 1}/{totalStacks}: {stackDisplayName}"
                : $"Upgrading stack {stackIndex + 1}/{totalStacks}: {stackDisplayName}";
            // SignalR phase "ProductDeploy" lets the WebUI reuse the shared
            // per-stack progress routing (same as Deploy/Redeploy); the
            // Removing/Upgrading distinction is carried in the message.
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
            _logger.LogDebug(ex, "Failed to send product upgrade progress notification");
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
                ? $"Stack {stackDisplayName} upgraded successfully"
                : $"Stack {stackDisplayName} upgrade failed: {error}";
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
                    $"Product '{pd.ProductName}' upgraded to v{pd.ProductVersion} successfully ({totalStacks} stacks).",
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
            _logger.LogDebug(ex, "Failed to send final product upgrade notification");
        }
    }

    private async Task CreateInAppNotificationAsync(ProductDeployment pd, CancellationToken ct)
    {
        if (_inAppNotificationService == null) return;

        try
        {
            var success = pd.Status == ProductDeploymentStatus.Running;
            var notification = NotificationFactory.CreateProductDeploymentResult(
                success, "upgrade", pd.ProductName, pd.ProductVersion,
                pd.TotalStacks, pd.CompletedStacks, pd.FailedStacks,
                productDeploymentId: pd.Id.Value.ToString());

            await _inAppNotificationService.AddAsync(notification, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to create in-app notification for product upgrade");
        }
    }

    /// <summary>
    /// Compares two semantic versions.
    /// Returns: -1 (v1 &lt; v2), 0 (equal), 1 (v1 &gt; v2), null if either is not valid SemVer.
    /// </summary>
    internal static int? CompareVersions(string? v1, string? v2)
    {
        if (string.IsNullOrEmpty(v1) && string.IsNullOrEmpty(v2)) return 0;
        if (string.IsNullOrEmpty(v1)) return -1;
        if (string.IsNullOrEmpty(v2)) return 1;

        var normalized1 = v1.TrimStart('v', 'V');
        var normalized2 = v2.TrimStart('v', 'V');

        if (Version.TryParse(normalized1, out var ver1) &&
            Version.TryParse(normalized2, out var ver2))
        {
            return ver1.CompareTo(ver2);
        }

        return null;
    }
}
