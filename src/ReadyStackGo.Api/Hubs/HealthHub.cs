using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ReadyStackGo.Application.UseCases.Health;
using ReadyStackGo.Application.UseCases.Health.GetEnvironmentHealthSummary;

namespace ReadyStackGo.Api.Hubs;

/// <summary>
/// SignalR Hub for real-time health updates.
/// Clients can subscribe to specific environments or deployments.
/// </summary>
[Authorize]
public class HealthHub : Hub
{
    private readonly ILogger<HealthHub> _logger;
    private readonly IMediator _mediator;

    public HealthHub(ILogger<HealthHub> logger, IMediator mediator)
    {
        _logger = logger;
        _mediator = mediator;
    }

    /// <summary>
    /// Subscribe to health updates for a specific environment.
    /// Immediately sends current health status to the caller.
    /// </summary>
    public async Task SubscribeToEnvironment(string environmentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"env:{environmentId}");
        _logger.LogInformation(
            "Client {ConnectionId} subscribed to environment {EnvironmentId}",
            Context.ConnectionId, environmentId);

        // Send current health status immediately to the caller
        try
        {
            var query = new GetEnvironmentHealthSummaryQuery(environmentId);
            var response = await _mediator.Send(query);

            if (response.Success && response.Data != null)
            {
                await Clients.Caller.SendAsync("EnvironmentHealthChanged", response.Data);
                _logger.LogInformation(
                    "Sent initial health status to client {ConnectionId} for environment {EnvironmentId} with {StackCount} stacks",
                    Context.ConnectionId, environmentId, response.Data.Stacks?.Count ?? 0);
            }
            else
            {
                _logger.LogWarning(
                    "No health data available for environment {EnvironmentId} (Success={Success})",
                    environmentId, response.Success);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send initial health status to client {ConnectionId} for environment {EnvironmentId}",
                Context.ConnectionId, environmentId);
        }
    }

    /// <summary>
    /// Unsubscribe from health updates for a specific environment.
    /// </summary>
    public async Task UnsubscribeFromEnvironment(string environmentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"env:{environmentId}");
        _logger.LogDebug(
            "Client {ConnectionId} unsubscribed from environment {EnvironmentId}",
            Context.ConnectionId, environmentId);
    }

    /// <summary>
    /// Subscribe to health updates for a specific deployment.
    /// </summary>
    public async Task SubscribeToDeployment(string deploymentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"deploy:{deploymentId}");
        _logger.LogDebug(
            "Client {ConnectionId} subscribed to deployment {DeploymentId}",
            Context.ConnectionId, deploymentId);
    }

    /// <summary>
    /// Unsubscribe from health updates for a specific deployment.
    /// </summary>
    public async Task UnsubscribeFromDeployment(string deploymentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"deploy:{deploymentId}");
        _logger.LogDebug(
            "Client {ConnectionId} unsubscribed from deployment {DeploymentId}",
            Context.ConnectionId, deploymentId);
    }

    /// <summary>
    /// Subscribe to all health updates (for dashboard).
    /// </summary>
    public async Task SubscribeToAllHealth()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "health:all");
        _logger.LogDebug(
            "Client {ConnectionId} subscribed to all health updates",
            Context.ConnectionId);
    }

    /// <summary>
    /// Unsubscribe from all health updates.
    /// </summary>
    public async Task UnsubscribeFromAllHealth()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "health:all");
        _logger.LogDebug(
            "Client {ConnectionId} unsubscribed from all health updates",
            Context.ConnectionId);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "Client connected to HealthHub: {ConnectionId}, User: {User}",
            Context.ConnectionId, Context.User?.Identity?.Name ?? "Unknown");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation(
            "Client disconnected from HealthHub: {ConnectionId}, Reason: {Reason}",
            Context.ConnectionId, exception?.Message ?? "Normal disconnect");
        await base.OnDisconnectedAsync(exception);
    }
}
