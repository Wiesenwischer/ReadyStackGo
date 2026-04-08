using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Domain.Deployment.Health;

namespace ReadyStackGo.Application.Services.Impl;

/// <summary>
/// Health check strategy for TCP port connectivity.
/// Attempts a TCP connection to the container's port to verify the service is accepting connections.
/// Used for services without HTTP endpoints (e.g., Redis, PostgreSQL, RabbitMQ).
/// </summary>
public class TcpHealthCheckStrategy : IHealthCheckStrategy
{
    private readonly ITcpHealthChecker _tcpHealthChecker;
    private readonly ILogger<TcpHealthCheckStrategy> _logger;

    public TcpHealthCheckStrategy(ITcpHealthChecker tcpHealthChecker, ILogger<TcpHealthCheckStrategy> logger)
    {
        _tcpHealthChecker = tcpHealthChecker;
        _logger = logger;
    }

    public string SupportedType => "tcp";

    public async Task<HealthCheckStrategyResult> CheckHealthAsync(
        ContainerDto container,
        string serviceName,
        ServiceHealthCheckConfig config,
        CancellationToken cancellationToken)
    {
        var containerAddress = container.Name.TrimStart('/');
        var port = config.Port ?? ContainerPortHelper.GetFirstExposedPort(container);

        if (port == null)
        {
            _logger.LogWarning(
                "No port configured for TCP health check on service {ServiceName}, falling back to Docker status",
                serviceName);
            return DockerHealthCheckStrategy.FromDocker(container);
        }

        var tcpConfig = new TcpHealthCheckConfig
        {
            Port = port.Value,
            Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
        };

        try
        {
            var result = await _tcpHealthChecker.CheckHealthAsync(containerAddress, tcpConfig, cancellationToken);

            return new HealthCheckStrategyResult(
                result.IsHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                result.Error,
                ResponseTimeMs: result.ResponseTimeMs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TCP health check failed for {ServiceName}", serviceName);
            return new HealthCheckStrategyResult(HealthStatus.Unhealthy, $"TCP health check error: {ex.Message}");
        }
    }
}
