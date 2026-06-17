using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Notifications;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.Application.UseCases.StackSources.SyncStackSources;

public class SyncStackSourcesHandler : IRequestHandler<SyncStackSourcesCommand, SyncStackSourcesResult>
{
    private readonly IProductSourceService _productSourceService;
    private readonly INotificationService? _notificationService;
    private readonly IProductDeploymentRepository? _productDeployments;
    private readonly ILogger<SyncStackSourcesHandler> _logger;

    public SyncStackSourcesHandler(
        IProductSourceService productSourceService,
        ILogger<SyncStackSourcesHandler> logger,
        INotificationService? notificationService = null,
        IProductDeploymentRepository? productDeployments = null)
    {
        _productSourceService = productSourceService;
        _logger = logger;
        _notificationService = notificationService;
        _productDeployments = productDeployments;
    }

    public async Task<SyncStackSourcesResult> Handle(SyncStackSourcesCommand request, CancellationToken cancellationToken)
    {
        var result = await _productSourceService.SyncAllAsync(cancellationToken);

        await CreateSyncNotificationAsync(result, cancellationToken);
        await CheckForProductUpdatesAsync(cancellationToken);

        return new SyncStackSourcesResult(
            result.Success,
            result.StacksLoaded,
            result.SourcesSynced,
            result.Errors.ToList(),
            result.Warnings.ToList()
        );
    }

    private async Task CreateSyncNotificationAsync(SyncResult result, CancellationToken ct)
    {
        if (_notificationService == null) return;

        try
        {
            var notification = NotificationFactory.CreateSyncResult(
                result.Success, result.StacksLoaded, result.SourcesSynced,
                result.Errors, result.Warnings);

            await _notificationService.AddAsync(notification, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to create sync notification");
        }
    }

    /// <summary>
    /// After a sync, raises a one-time notification per active product deployment that has a
    /// newer version available in the catalog. Deduped per (deployment, target version).
    /// Best-effort: failures here must not break the sync.
    /// </summary>
    private async Task CheckForProductUpdatesAsync(CancellationToken ct)
    {
        if (_notificationService == null || _productDeployments == null) return;

        try
        {
            foreach (var deployment in _productDeployments.GetAllActive())
            {
                if (string.IsNullOrEmpty(deployment.ProductVersion))
                    continue;

                var upgrades = (await _productSourceService.GetAvailableUpgradesAsync(
                    deployment.ProductGroupId, deployment.ProductVersion, ct)).ToList();

                var latest = upgrades.FirstOrDefault();
                if (latest?.ProductVersion == null)
                    continue;

                var deploymentId = deployment.Id.Value.ToString();
                var dedupKey = NotificationFactory.ProductUpdateDedupKey(deploymentId, latest.ProductVersion);

                if (await _notificationService.ExistsAsync(
                        NotificationType.ProductUpdateAvailable, "dedupKey", dedupKey, ct))
                    continue;

                var notification = NotificationFactory.CreateProductUpdateAvailable(
                    deployment.ProductName, deployment.ProductVersion, latest.ProductVersion, deploymentId);

                await _notificationService.AddAsync(notification, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to check for product updates after sync");
        }
    }
}
