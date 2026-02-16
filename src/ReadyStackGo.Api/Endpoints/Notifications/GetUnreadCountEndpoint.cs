using FastEndpoints;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Api.Endpoints.Notifications;

/// <summary>
/// GET /api/notifications/unread-count
/// Returns the number of unread notifications.
/// </summary>
public class GetUnreadCountEndpoint : EndpointWithoutRequest<UnreadCountResponse>
{
    private readonly INotificationService _notificationService;

    public GetUnreadCountEndpoint(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public override void Configure()
    {
        Get("/api/notifications/unread-count");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var count = await _notificationService.GetUnreadCountAsync(ct);
        Response = new UnreadCountResponse { Count = count };
    }
}

public class UnreadCountResponse
{
    public int Count { get; set; }
}
