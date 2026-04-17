using FastEndpoints;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Api.Endpoints.Notifications;

/// <summary>
/// DELETE /api/notifications
/// Dismisses (removes) all notifications.
/// </summary>
public class DismissAllNotificationsEndpoint : EndpointWithoutRequest
{
    private readonly INotificationService _notificationService;

    public DismissAllNotificationsEndpoint(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public override void Configure()
    {
        Delete("/api/notifications");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await _notificationService.DismissAllAsync(ct);
        HttpContext.Response.StatusCode = StatusCodes.Status204NoContent;
    }
}
