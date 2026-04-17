using FastEndpoints;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Api.Endpoints.Notifications;

/// <summary>
/// GET /api/notifications/count
/// Returns the total number of notifications.
/// </summary>
public class GetNotificationCountEndpoint : EndpointWithoutRequest<NotificationCountResponse>
{
    private readonly INotificationService _notificationService;

    public GetNotificationCountEndpoint(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public override void Configure()
    {
        Get("/api/notifications/count");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var count = await _notificationService.GetCountAsync(ct);
        Response = new NotificationCountResponse { Count = count };
    }
}

public class NotificationCountResponse
{
    public int Count { get; set; }
}
