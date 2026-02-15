using Microsoft.AspNetCore.SignalR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Api.Hubs;

/// <summary>
/// SignalR Hub for real-time self-update progress.
/// Anonymous access — the update page must work without authentication.
/// On connect, sends the current progress to the client immediately.
/// </summary>
public class UpdateHub : Hub
{
    private readonly ISelfUpdateService _selfUpdateService;
    private readonly ILogger<UpdateHub> _logger;

    public UpdateHub(ISelfUpdateService selfUpdateService, ILogger<UpdateHub> logger)
    {
        _selfUpdateService = selfUpdateService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogDebug("Client connected to UpdateHub: {ConnectionId}", Context.ConnectionId);

        // Send current progress immediately so late-joiners see the current state
        var progress = _selfUpdateService.GetProgress();
        await Clients.Caller.SendAsync("UpdateProgress", progress);

        await base.OnConnectedAsync();
    }
}
