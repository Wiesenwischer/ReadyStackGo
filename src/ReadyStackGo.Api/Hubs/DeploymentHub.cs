using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ReadyStackGo.Api.Hubs;

/// <summary>
/// SignalR Hub for real-time deployment progress updates.
/// Clients can subscribe to specific deployment sessions.
/// </summary>
[Authorize]
public class DeploymentHub : Hub
{
    private readonly ILogger<DeploymentHub> _logger;

    public DeploymentHub(ILogger<DeploymentHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Subscribe to deployment progress updates for a specific session.
    /// Call this before starting the deployment to receive all progress updates.
    /// </summary>
    /// <param name="sessionId">The deployment session ID to subscribe to.</param>
    public async Task SubscribeToDeployment(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"deployment:{sessionId}");
        _logger.LogInformation(
            "Client {ConnectionId} subscribed to deployment session {SessionId}",
            Context.ConnectionId, sessionId);
    }

    /// <summary>
    /// Unsubscribe from deployment progress updates.
    /// </summary>
    /// <param name="sessionId">The deployment session ID to unsubscribe from.</param>
    public async Task UnsubscribeFromDeployment(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"deployment:{sessionId}");
        _logger.LogDebug(
            "Client {ConnectionId} unsubscribed from deployment session {SessionId}",
            Context.ConnectionId, sessionId);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "Client connected to DeploymentHub: {ConnectionId}, User: {User}",
            Context.ConnectionId, Context.User?.Identity?.Name ?? "Unknown");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation(
            "Client disconnected from DeploymentHub: {ConnectionId}, Reason: {Reason}",
            Context.ConnectionId, exception?.Message ?? "Normal disconnect");
        await base.OnDisconnectedAsync(exception);
    }
}
