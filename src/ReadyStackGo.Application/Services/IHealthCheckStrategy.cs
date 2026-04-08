using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Domain.Deployment.Health;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Strategy interface for performing health checks on containers.
/// Each health check type (HTTP, TCP, Docker) implements this interface.
/// Strategies are resolved by their <see cref="SupportedType"/> via <see cref="IHealthCheckStrategyFactory"/>.
/// </summary>
public interface IHealthCheckStrategy
{
    /// <summary>
    /// The health check type this strategy handles (e.g., "http", "tcp", "docker").
    /// </summary>
    string SupportedType { get; }

    /// <summary>
    /// Performs a health check on the given container.
    /// </summary>
    Task<HealthCheckStrategyResult> CheckHealthAsync(
        ContainerDto container,
        string serviceName,
        ServiceHealthCheckConfig config,
        CancellationToken cancellationToken);
}

/// <summary>
/// Factory for resolving the appropriate <see cref="IHealthCheckStrategy"/> by type.
/// </summary>
public interface IHealthCheckStrategyFactory
{
    /// <summary>
    /// Returns the strategy for the given health check type.
    /// Falls back to Docker strategy for unknown types.
    /// </summary>
    IHealthCheckStrategy GetStrategy(string healthCheckType);
}

/// <summary>
/// Unified result from any health check strategy.
/// </summary>
public record HealthCheckStrategyResult(
    HealthStatus Status,
    string? Reason,
    IReadOnlyList<HealthCheckEntry>? Entries = null,
    int? ResponseTimeMs = null);
