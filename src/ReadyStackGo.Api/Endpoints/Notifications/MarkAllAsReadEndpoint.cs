using FastEndpoints;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Api.Endpoints.Notifications;

/// <summary>
/// POST /api/notifications/read-all
/// Marks all notifications as read.
/// </summary>
public class MarkAllAsReadEndpoint : EndpointWithoutRequest
{
    private readonly INotificationService _notificationService;

    public MarkAllAsReadEndpoint(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public override void Configure()
    {
        Post("/api/notifications/read-all");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await _notificationService.MarkAllAsReadAsync(ct);
        HttpContext.Response.StatusCode = StatusCodes.Status204NoContent;
    }
}
