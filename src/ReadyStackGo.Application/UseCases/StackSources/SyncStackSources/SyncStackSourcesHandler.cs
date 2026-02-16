using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Notifications;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.Application.UseCases.StackSources.SyncStackSources;

public class SyncStackSourcesHandler : IRequestHandler<SyncStackSourcesCommand, SyncStackSourcesResult>
{
    private readonly IProductSourceService _productSourceService;
    private readonly INotificationService? _notificationService;
    private readonly ILogger<SyncStackSourcesHandler> _logger;

    public SyncStackSourcesHandler(
        IProductSourceService productSourceService,
        ILogger<SyncStackSourcesHandler> logger,
        INotificationService? notificationService = null)
    {
        _productSourceService = productSourceService;
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task<SyncStackSourcesResult> Handle(SyncStackSourcesCommand request, CancellationToken cancellationToken)
    {
        var result = await _productSourceService.SyncAllAsync(cancellationToken);

        await CreateSyncNotificationAsync(result, cancellationToken);

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
}
