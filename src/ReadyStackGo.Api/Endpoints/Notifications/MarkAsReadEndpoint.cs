using FastEndpoints;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Api.Endpoints.Notifications;

/// <summary>
/// POST /api/notifications/{id}/read
/// Marks a single notification as read.
/// </summary>
public class MarkAsReadEndpoint : Endpoint<MarkAsReadRequest>
{
    private readonly INotificationService _notificationService;

    public MarkAsReadEndpoint(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public override void Configure()
    {
        Post("/api/notifications/{id}/read");
    }

    public override async Task HandleAsync(MarkAsReadRequest req, CancellationToken ct)
    {
        await _notificationService.MarkAsReadAsync(req.Id, ct);
        HttpContext.Response.StatusCode = StatusCodes.Status204NoContent;
    }
}

public class MarkAsReadRequest
{
    public string Id { get; set; } = string.Empty;
}
