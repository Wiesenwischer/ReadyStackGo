using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Notifications;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.DeployStack;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Application.UseCases.Deployments.UpgradeStack;

/// <summary>
/// Handler for upgrading a deployment to a new version.
/// Validates the upgrade, creates a snapshot, and delegates to the deploy flow.
/// </summary>
public class UpgradeStackHandler : IRequestHandler<UpgradeStackCommand, UpgradeStackResponse>
{
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IProductSourceService _productSourceService;
    private readonly IMediator _mediator;
    private readonly INotificationService? _notificationService;
    private readonly ILogger<UpgradeStackHandler> _logger;

    public UpgradeStackHandler(
        IDeploymentRepository deploymentRepository,
        IProductSourceService productSourceService,
        IMediator mediator,
        ILogger<UpgradeStackHandler> logger,
        INotificationService? notificationService = null)
    {
        _deploymentRepository = deploymentRepository;
        _productSourceService = productSourceService;
        _mediator = mediator;
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task<UpgradeStackResponse> Handle(UpgradeStackCommand request, CancellationToken cancellationToken)
    {
        // 1. Parse and validate deployment ID
        if (!Guid.TryParse(request.DeploymentId, out var deploymentGuid))
        {
            return UpgradeStackResponse.Failed("Invalid deployment ID format.");
        }

        // 2. Get deployment
        var deployment = _deploymentRepository.GetById(new DeploymentId(deploymentGuid));
        if (deployment == null)
        {
            return UpgradeStackResponse.Failed("Deployment not found.");
        }

        // 3. Validate deployment can be upgraded
        if (!deployment.CanUpgrade())
        {
            return UpgradeStackResponse.Failed(
                $"Only running deployments can be upgraded. Current status: {deployment.Status}");
        }

        // 4. Load new stack from catalog
        var newStack = await _productSourceService.GetStackAsync(request.NewStackId, cancellationToken);
        if (newStack == null)
        {
            return UpgradeStackResponse.Failed($"Stack '{request.NewStackId}' not found in catalog.");
        }

        // 5. Version comparison (prevent downgrade)
        var previousVersion = deployment.StackVersion;
        var newVersion = newStack.ProductVersion;

        var comparison = CompareVersions(previousVersion, newVersion);
        if (comparison.HasValue)
        {
            if (comparison.Value == 0)
            {
                return UpgradeStackResponse.Failed($"Already running version {previousVersion}.");
            }
            if (comparison.Value > 0)
            {
                return UpgradeStackResponse.Failed(
                    $"Downgrade from {previousVersion} to {newVersion} is not supported. Use rollback instead.");
            }
        }

        _logger.LogInformation(
            "Starting upgrade of deployment {DeploymentId} from {PreviousVersion} to {NewVersion}",
            deployment.Id, previousVersion, newVersion);

        // 6. Merge variables: Stack defaults < Existing deployment < Explicit request
        var mergedVariables = MergeVariables(
            deployment.Variables,
            request.Variables ?? new Dictionary<string, string>(),
            newStack.Variables.Select(v => new { v.Name, v.DefaultValue }).ToList());

        // 7. Execute deployment via DeployStack command
        // The existing deploy flow handles snapshot creation, container management, etc.
        // SuppressNotification: true — we create our own upgrade-specific notification
        var deployResult = await _mediator.Send(new DeployStackCommand(
            request.EnvironmentId,
            request.NewStackId,
            deployment.StackName, // Keep same stack name
            mergedVariables,
            request.SessionId,
            SuppressNotification: true), cancellationToken);

        if (!deployResult.Success)
        {
            // Check if rollback is available (snapshot should have been created)
            var updatedDeployment = _deploymentRepository.GetById(deployment.Id);
            var canRollback = updatedDeployment?.CanRollback() ?? false;
            var rollbackVersion = updatedDeployment?.GetRollbackTargetVersion();

            await CreateUpgradeNotificationAsync(false, deployment.StackName,
                previousVersion, newVersion, deployResult.DeploymentId, cancellationToken);

            return new UpgradeStackResponse
            {
                Success = false,
                Message = deployResult.Message ?? "Upgrade failed",
                DeploymentId = request.DeploymentId,
                PreviousVersion = previousVersion,
                NewVersion = newVersion,
                Errors = deployResult.Errors,
                CanRollback = canRollback,
                RollbackVersion = rollbackVersion
            };
        }

        // Note: RecordUpgrade is now called by DeploymentService during the upgrade flow
        // to avoid concurrency issues (EF Core change tracking with Version token)

        _logger.LogInformation(
            "Successfully upgraded deployment {DeploymentId} from {PreviousVersion} to {NewVersion}",
            deployment.Id, previousVersion, newVersion);

        await CreateUpgradeNotificationAsync(true, deployment.StackName,
            previousVersion, newVersion, deployResult.DeploymentId, cancellationToken);

        return new UpgradeStackResponse
        {
            Success = true,
            Message = $"Successfully upgraded from {previousVersion} to {newVersion}",
            DeploymentId = request.DeploymentId,
            PreviousVersion = previousVersion,
            NewVersion = newVersion,
            SnapshotId = null, // Snapshot was cleared after successful upgrade (Point of No Return)
            CanRollback = false // Successful upgrade = no rollback needed
        };
    }

    private async Task CreateUpgradeNotificationAsync(
        bool success, string stackName, string? previousVersion, string? newVersion,
        string? deploymentId, CancellationToken ct)
    {
        if (_notificationService == null) return;

        try
        {
            var message = success
                ? $"Stack '{stackName}' upgraded from {previousVersion} to {newVersion}."
                : $"Failed to upgrade stack '{stackName}' from {previousVersion} to {newVersion}.";

            var notification = NotificationFactory.CreateDeploymentResult(
                success, "upgrade", stackName, message, deploymentId);

            await _notificationService.AddAsync(notification, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to create upgrade notification for {StackName}", stackName);
        }
    }

    /// <summary>
    /// Merges variables from existing deployment with new values and stack defaults.
    /// Priority: Explicit request > Existing deployment > Stack defaults
    /// </summary>
    private static Dictionary<string, string> MergeVariables(
        IReadOnlyDictionary<string, string> existing,
        Dictionary<string, string> requested,
        IReadOnlyList<dynamic> stackVariables)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 1. Start with stack defaults
        foreach (var v in stackVariables)
        {
            if (!string.IsNullOrEmpty(v.DefaultValue))
            {
                merged[v.Name] = v.DefaultValue;
            }
        }

        // 2. Overlay with existing deployment values
        foreach (var kvp in existing)
        {
            merged[kvp.Key] = kvp.Value;
        }

        // 3. Overlay with explicitly requested values
        foreach (var kvp in requested)
        {
            merged[kvp.Key] = kvp.Value;
        }

        return merged;
    }

    /// <summary>
    /// Compares two semantic versions.
    /// Returns: -1 (v1 &lt; v2), 0 (equal), 1 (v1 &gt; v2)
    /// Returns null if either version is not valid SemVer.
    /// </summary>
    private static int? CompareVersions(string? v1, string? v2)
    {
        if (string.IsNullOrEmpty(v1) && string.IsNullOrEmpty(v2)) return 0;
        if (string.IsNullOrEmpty(v1)) return -1;
        if (string.IsNullOrEmpty(v2)) return 1;

        // Normalize: remove 'v' prefix
        var normalized1 = v1.TrimStart('v', 'V');
        var normalized2 = v2.TrimStart('v', 'V');

        // Try semantic version comparison
        if (Version.TryParse(normalized1, out var ver1) &&
            Version.TryParse(normalized2, out var ver2))
        {
            return ver1.CompareTo(ver2);
        }

        // Return null if not valid SemVer
        return null;
    }
}
