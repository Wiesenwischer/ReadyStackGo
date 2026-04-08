namespace ReadyStackGo.Application.Services.Impl;

/// <summary>
/// Resolves the appropriate <see cref="IHealthCheckStrategy"/> by health check type.
/// Builds a dictionary from all registered strategies at construction time.
/// Falls back to <see cref="DockerHealthCheckStrategy"/> for unknown types.
/// </summary>
public class HealthCheckStrategyFactory : IHealthCheckStrategyFactory
{
    private readonly Dictionary<string, IHealthCheckStrategy> _strategies;
    private readonly IHealthCheckStrategy _fallback = new DockerHealthCheckStrategy();

    public HealthCheckStrategyFactory(IEnumerable<IHealthCheckStrategy> strategies)
    {
        _strategies = new(StringComparer.OrdinalIgnoreCase);
        foreach (var strategy in strategies)
        {
            _strategies[strategy.SupportedType] = strategy;
        }
    }

    public IHealthCheckStrategy GetStrategy(string healthCheckType) =>
        _strategies.GetValueOrDefault(healthCheckType, _fallback);
}
