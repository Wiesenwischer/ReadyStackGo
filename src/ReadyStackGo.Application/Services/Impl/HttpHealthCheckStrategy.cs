using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Domain.Deployment.Health;

namespace ReadyStackGo.Application.Services.Impl;

/// <summary>
/// Health check strategy for HTTP endpoints.
/// Calls the container's HTTP health endpoint and parses the ASP.NET Core HealthReport response.
/// </summary>
public class HttpHealthCheckStrategy : IHealthCheckStrategy
{
    private readonly IHttpHealthChecker _httpHealthChecker;
    private readonly ILogger<HttpHealthCheckStrategy> _logger;

    public HttpHealthCheckStrategy(IHttpHealthChecker httpHealthChecker, ILogger<HttpHealthCheckStrategy> logger)
    {
        _httpHealthChecker = httpHealthChecker;
        _logger = logger;
    }

    public string SupportedType => "http";

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
                "No port configured for HTTP health check on service {ServiceName}, falling back to Docker status",
                serviceName);
            return DockerHealthCheckStrategy.FromDocker(container);
        }

        var httpConfig = new HttpHealthCheckConfig
        {
            Path = config.Path,
            Port = port.Value,
            Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds),
            HealthyStatusCodes = config.ExpectedStatusCodes,
            UseHttps = config.UseHttps
        };

        try
        {
            var result = await _httpHealthChecker.CheckHealthAsync(containerAddress, httpConfig, cancellationToken);

            var entries = result.Entries?.Select(e => HealthCheckEntry.Create(
                e.Name,
                MapEntryStatus(e.Status),
                e.Description,
                e.DurationMs,
                e.Data,
                e.Tags as IReadOnlyList<string>,
                e.Exception
            )).ToList() as IReadOnlyList<HealthCheckEntry>;

            if (result.IsHealthy)
            {
                return new HealthCheckStrategyResult(HealthStatus.Healthy, null, entries, result.ResponseTimeMs);
            }

            var status = result.ReportedStatus?.ToLowerInvariant() switch
            {
                "healthy" => HealthStatus.Healthy,
                "degraded" => HealthStatus.Degraded,
                "unhealthy" => HealthStatus.Unhealthy,
                _ => HealthStatus.Unhealthy
            };

            var reason = result.Error ?? $"HTTP health check: {result.ReportedStatus}";
            if (result.ResponseTimeMs.HasValue)
            {
                reason += $" ({result.ResponseTimeMs}ms)";
            }

            return new HealthCheckStrategyResult(status, reason, entries, result.ResponseTimeMs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HTTP health check failed for {ServiceName}", serviceName);
            return new HealthCheckStrategyResult(HealthStatus.Unhealthy, $"HTTP health check error: {ex.Message}");
        }
    }

    private static HealthStatus MapEntryStatus(string statusString)
    {
        return statusString.ToLowerInvariant() switch
        {
            "healthy" => HealthStatus.Healthy,
            "degraded" => HealthStatus.Degraded,
            "unhealthy" => HealthStatus.Unhealthy,
            _ => HealthStatus.Unknown
        };
    }
}
