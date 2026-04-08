using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Domain.Deployment.Health;

namespace ReadyStackGo.Application.Services.Impl;

/// <summary>
/// Fallback health check strategy that uses Docker container state and HEALTHCHECK status.
/// This is the default strategy when no RSGO-managed health check (HTTP/TCP) is configured.
/// </summary>
public class DockerHealthCheckStrategy : IHealthCheckStrategy
{
    public string SupportedType => "docker";

    public Task<HealthCheckStrategyResult> CheckHealthAsync(
        ContainerDto container,
        string serviceName,
        ServiceHealthCheckConfig config,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(FromDocker(container));
    }

    /// <summary>
    /// Creates a health check result from Docker container state.
    /// Shared by other strategies as fallback when port resolution fails.
    /// </summary>
    public static HealthCheckStrategyResult FromDocker(ContainerDto container)
    {
        var status = DetermineHealthStatusFromDocker(container);
        var reason = DetermineHealthReason(container, status);
        return new HealthCheckStrategyResult(status, reason);
    }

    /// <summary>
    /// Determines the health status based on Docker container state and HEALTHCHECK.
    /// If a Docker HEALTHCHECK is defined, its result is authoritative.
    /// If no HEALTHCHECK is defined, falls back to container state.
    /// </summary>
    internal static HealthStatus DetermineHealthStatusFromDocker(ContainerDto container)
    {
        if (!string.IsNullOrEmpty(container.HealthStatus) && container.HealthStatus != "none")
        {
            return container.HealthStatus.ToLowerInvariant() switch
            {
                "healthy" => HealthStatus.Healthy,
                "unhealthy" => HealthStatus.Unhealthy,
                "starting" => HealthStatus.Degraded,
                _ => HealthStatus.Unknown
            };
        }

        return container.State.ToLowerInvariant() switch
        {
            "running" => HealthStatus.Running,
            "restarting" => HealthStatus.Degraded,
            "paused" => HealthStatus.Degraded,
            "exited" => HealthStatus.Unhealthy,
            "dead" => HealthStatus.Unhealthy,
            "created" => HealthStatus.Unknown,
            _ => HealthStatus.Unknown
        };
    }

    /// <summary>
    /// Determines the reason for the current health status.
    /// </summary>
    internal static string? DetermineHealthReason(ContainerDto container, HealthStatus status)
    {
        if (status == HealthStatus.Healthy)
            return null;

        if (!string.IsNullOrEmpty(container.HealthStatus) && container.HealthStatus != "none")
        {
            if (container.HealthStatus.Equals("unhealthy", StringComparison.OrdinalIgnoreCase))
                return $"Health check failing (streak: {container.FailingStreak})";

            if (container.HealthStatus.Equals("starting", StringComparison.OrdinalIgnoreCase))
                return "Container starting, health check pending";
        }

        return container.State.ToLowerInvariant() switch
        {
            "running" => "Container is running",
            "restarting" => "Container is restarting",
            "paused" => "Container is paused",
            "exited" => $"Container exited (status: {container.Status})",
            "dead" => "Container is dead",
            "created" => "Container created but not started",
            _ => $"Unknown state: {container.State}"
        };
    }
}
