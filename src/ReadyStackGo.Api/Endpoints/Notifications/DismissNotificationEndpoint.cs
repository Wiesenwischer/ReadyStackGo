using FastEndpoints;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Api.Endpoints.Notifications;

/// <summary>
/// DELETE /api/notifications/{id}
/// Dismisses (removes) a notification.
/// </summary>
public class DismissNotificationEndpoint : Endpoint<DismissNotificationRequest>
{
    private readonly INotificationService _notificationService;

    public DismissNotificationEndpoint(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public override void Configure()
    {
        Delete("/api/notifications/{id}");
    }

    public override async Task HandleAsync(DismissNotificationRequest req, CancellationToken ct)
    {
        await _notificationService.DismissAsync(req.Id, ct);
        HttpContext.Response.StatusCode = StatusCodes.Status204NoContent;
    }
}

public class DismissNotificationRequest
{
    public string Id { get; set; } = string.Empty;
}
