using FastEndpoints;
using ReadyStackGo.Application.Notifications;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Api.Endpoints.Notifications;

/// <summary>
/// GET /api/notifications
/// Returns all notifications, newest first.
/// </summary>
public class ListNotificationsEndpoint : EndpointWithoutRequest<ListNotificationsResponse>
{
    private readonly INotificationService _notificationService;

    public ListNotificationsEndpoint(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public override void Configure()
    {
        Get("/api/notifications");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var notifications = await _notificationService.GetAllAsync(ct);
        Response = new ListNotificationsResponse { Notifications = notifications };
    }
}

public class ListNotificationsResponse
{
    public IReadOnlyList<Notification> Notifications { get; set; } = [];
}
