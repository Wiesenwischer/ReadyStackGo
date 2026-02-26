using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Api.Hubs;

/// <summary>
/// SignalR Hub for real-time container log streaming.
/// Clients can subscribe to live log output from specific containers.
/// </summary>
[Authorize]
public class ContainerHub : Hub
{
    private readonly IDockerService _dockerService;
    private readonly ILogger<ContainerHub> _logger;

    // Tracks active log streams per connection: "connectionId:containerId" → CTS
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> ActiveStreams = new();

    public ContainerHub(IDockerService dockerService, ILogger<ContainerHub> logger)
    {
        _dockerService = dockerService;
        _logger = logger;
    }

    /// <summary>
    /// Subscribe to live log streaming for a specific container.
    /// Starts a background task that streams log lines to the caller.
    /// </summary>
    public async Task SubscribeToContainerLogs(string environmentId, string containerId)
    {
        var streamKey = $"{Context.ConnectionId}:{containerId}";

        // Cancel any existing stream for this container on this connection
        if (ActiveStreams.TryRemove(streamKey, out var existingCts))
        {
            await existingCts.CancelAsync();
            existingCts.Dispose();
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(Context.ConnectionAborted);
        ActiveStreams[streamKey] = cts;

        _logger.LogInformation(
            "Client {ConnectionId} subscribing to container logs: {EnvironmentId}/{ContainerId}",
            Context.ConnectionId, environmentId, containerId);

        var connectionId = Context.ConnectionId;
        var token = cts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var logLine in _dockerService.StreamContainerLogsAsync(
                    environmentId, containerId, token))
                {
                    await Clients.Client(connectionId).SendAsync(
                        "ContainerLogLine", containerId, logLine, token);
                }

                await Clients.Client(connectionId).SendAsync(
                    "ContainerLogStreamEnded", containerId, "Container stopped", CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // Expected when client unsubscribes or disconnects
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Log stream error for container {ContainerId}", containerId);

                try
                {
                    await Clients.Client(connectionId).SendAsync(
                        "ContainerLogStreamEnded", containerId, ex.Message, CancellationToken.None);
                }
                catch
                {
                    // Client may already be disconnected
                }
            }
            finally
            {
                ActiveStreams.TryRemove(streamKey, out _);
                cts.Dispose();
            }
        }, CancellationToken.None);
    }

    /// <summary>
    /// Unsubscribe from container log streaming.
    /// </summary>
    public async Task UnsubscribeFromContainerLogs(string containerId)
    {
        var streamKey = $"{Context.ConnectionId}:{containerId}";

        if (ActiveStreams.TryRemove(streamKey, out var cts))
        {
            _logger.LogDebug(
                "Client {ConnectionId} unsubscribing from container logs: {ContainerId}",
                Context.ConnectionId, containerId);

            await cts.CancelAsync();
            cts.Dispose();
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Cancel all active streams for this connection
        var prefix = $"{Context.ConnectionId}:";
        var keysToRemove = ActiveStreams.Keys.Where(k => k.StartsWith(prefix)).ToList();

        foreach (var key in keysToRemove)
        {
            if (ActiveStreams.TryRemove(key, out var cts))
            {
                await cts.CancelAsync();
                cts.Dispose();
            }
        }

        _logger.LogInformation(
            "Client disconnected from ContainerHub: {ConnectionId}, streams cancelled: {Count}",
            Context.ConnectionId, keysToRemove.Count);

        await base.OnDisconnectedAsync(exception);
    }
}
