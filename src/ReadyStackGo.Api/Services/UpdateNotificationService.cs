using Microsoft.AspNetCore.SignalR;
using ReadyStackGo.Api.Hubs;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Api.Services;

/// <summary>
/// Implementation of IUpdateNotificationService using SignalR.
/// Broadcasts self-update progress to all connected UpdateHub clients.
/// </summary>
public class UpdateNotificationService : IUpdateNotificationService
{
    private readonly IHubContext<UpdateHub> _hubContext;
    private readonly ILogger<UpdateNotificationService> _logger;

    public UpdateNotificationService(
        IHubContext<UpdateHub> hubContext,
        ILogger<UpdateNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyProgressAsync(UpdateProgress progress, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Sending update progress: {Phase} - {Percent}%",
            progress.Phase, progress.ProgressPercent);

        await _hubContext.Clients.All.SendAsync("UpdateProgress", progress, cancellationToken);
    }
}
