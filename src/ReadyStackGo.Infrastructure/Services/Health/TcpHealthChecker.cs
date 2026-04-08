using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Infrastructure.Services.Health;

/// <summary>
/// TCP health checker that verifies service availability by attempting a TCP connection.
/// Used for services that don't expose HTTP endpoints (e.g., Redis, PostgreSQL, RabbitMQ).
/// </summary>
public class TcpHealthChecker : ITcpHealthChecker
{
    private readonly ILogger<TcpHealthChecker> _logger;

    public TcpHealthChecker(ILogger<TcpHealthChecker> logger)
    {
        _logger = logger;
    }

    public async Task<TcpHealthCheckResult> CheckHealthAsync(
        string containerAddress,
        TcpHealthCheckConfig config,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(config.Timeout);

        using var client = new TcpClient();

        try
        {
            await client.ConnectAsync(containerAddress, config.Port, timeoutCts.Token);
            sw.Stop();

            var responseTimeMs = (int)sw.ElapsedMilliseconds;
            _logger.LogDebug(
                "TCP health check succeeded for {Address}:{Port} in {ResponseTimeMs}ms",
                containerAddress, config.Port, responseTimeMs);

            return TcpHealthCheckResult.Healthy(responseTimeMs);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            var error = $"TCP connection to {containerAddress}:{config.Port} timed out after {config.Timeout.TotalSeconds}s";
            _logger.LogDebug(error);
            return TcpHealthCheckResult.Unhealthy(error, (int)sw.ElapsedMilliseconds);
        }
        catch (SocketException ex)
        {
            sw.Stop();
            var error = $"TCP connection to {containerAddress}:{config.Port} failed: {ex.Message}";
            _logger.LogDebug(error);
            return TcpHealthCheckResult.ConnectionFailed(error);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            var error = $"TCP health check for {containerAddress}:{config.Port} failed: {ex.Message}";
            _logger.LogDebug(ex, error);
            return TcpHealthCheckResult.ConnectionFailed(error);
        }
    }
}
