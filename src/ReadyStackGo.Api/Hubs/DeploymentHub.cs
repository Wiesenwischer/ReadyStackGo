using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ReadyStackGo.Api.Hubs;

/// <summary>
/// SignalR Hub for real-time deployment progress updates.
/// Clients can subscribe to specific deployments to receive live updates.
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
    /// Subscribe to progress updates for a specific deployment.
    /// </summary>
    public async Task SubscribeToDeployment(string deploymentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"deployment:{deploymentId}");
        _logger.LogInformation(
            "Client {ConnectionId} subscribed to deployment {DeploymentId}",
            Context.ConnectionId, deploymentId);
    }

    /// <summary>
    /// Unsubscribe from progress updates for a specific deployment.
    /// </summary>
    public async Task UnsubscribeFromDeployment(string deploymentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"deployment:{deploymentId}");
        _logger.LogDebug(
            "Client {ConnectionId} unsubscribed from deployment {DeploymentId}",
            Context.ConnectionId, deploymentId);
    }

    /// <summary>
    /// Subscribe to all deployment progress updates (for dashboard).
    /// </summary>
    public async Task SubscribeToAllDeployments()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "deployments:all");
        _logger.LogDebug(
            "Client {ConnectionId} subscribed to all deployment updates",
            Context.ConnectionId);
    }

    /// <summary>
    /// Unsubscribe from all deployment progress updates.
    /// </summary>
    public async Task UnsubscribeFromAllDeployments()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "deployments:all");
        _logger.LogDebug(
            "Client {ConnectionId} unsubscribed from all deployment updates",
            Context.ConnectionId);
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

/// <summary>
/// DTO for deployment progress updates sent to clients.
/// </summary>
public record DeploymentProgressUpdate
{
    public required string DeploymentId { get; init; }
    public required string Phase { get; init; }
    public required string Message { get; init; }
    public int ProgressPercent { get; init; }
    public string? CurrentService { get; init; }
    public int TotalServices { get; init; }
    public int CompletedServices { get; init; }
    public bool IsComplete { get; init; }
    public bool IsError { get; init; }
    public string? ErrorMessage { get; init; }
}
