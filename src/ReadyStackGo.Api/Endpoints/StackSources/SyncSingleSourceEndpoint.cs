using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.Notifications;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.API.Endpoints.StackSources;

/// <summary>
/// POST /api/stack-sources/{id}/sync - Sync a specific stack source.
/// Accessible by: SystemAdmin only.
/// </summary>
[RequireSystemAdmin]
public class SyncSingleSourceEndpoint : Endpoint<EmptyRequest, SyncSourcesResponse>
{
    private readonly IProductSourceService _productSourceService;
    private readonly INotificationService? _notificationService;
    private readonly ILogger<SyncSingleSourceEndpoint> _logger;

    public SyncSingleSourceEndpoint(
        IProductSourceService productSourceService,
        ILogger<SyncSingleSourceEndpoint> logger,
        INotificationService? notificationService = null)
    {
        _productSourceService = productSourceService;
        _logger = logger;
        _notificationService = notificationService;
    }

    public override void Configure()
    {
        Post("/api/stack-sources/{id}/sync");
        PreProcessor<RbacPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var sourceId = Route<string>("id")!;
        var result = await _productSourceService.SyncSourceAsync(sourceId, ct);

        Response = new SyncSourcesResponse
        {
            Success = result.Success,
            StacksLoaded = result.StacksLoaded,
            SourcesSynced = result.SourcesSynced,
            Errors = result.Errors.ToList(),
            Warnings = result.Warnings.ToList()
        };

        if (!result.Success && result.Errors.Any(e => e.Contains("not found")))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
        }

        await CreateSyncNotificationAsync(result, sourceId, ct);
    }

    private async Task CreateSyncNotificationAsync(SyncResult result, string sourceName, CancellationToken ct)
    {
        if (_notificationService == null) return;

        try
        {
            var notification = NotificationFactory.CreateSyncResult(
                result.Success, result.StacksLoaded, result.SourcesSynced,
                result.Errors, result.Warnings, sourceName);

            await _notificationService.AddAsync(notification, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to create sync notification for source {SourceId}", sourceName);
        }
    }
}
