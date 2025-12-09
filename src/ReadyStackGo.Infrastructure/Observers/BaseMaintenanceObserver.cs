using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Deployment.Observers;

namespace ReadyStackGo.Infrastructure.Observers;

/// <summary>
/// Base class for maintenance observers providing common functionality.
/// </summary>
public abstract class BaseMaintenanceObserver : IMaintenanceObserver
{
    protected readonly MaintenanceObserverConfig Config;
    protected readonly ILogger Logger;

    protected BaseMaintenanceObserver(MaintenanceObserverConfig config, ILogger logger)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public abstract ObserverType Type { get; }

    public async Task<ObserverResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogDebug("Performing maintenance check for {ObserverType}", Type.DisplayName);

            var observedValue = await GetObservedValueAsync(cancellationToken);

            var result = DetermineResult(observedValue);

            Logger.LogDebug(
                "Maintenance check completed: {Result} (observed: {ObservedValue}, maintenance value: {MaintenanceValue})",
                result.IsMaintenanceRequired ? "Maintenance Required" : "Normal Operation",
                observedValue,
                Config.MaintenanceValue);

            return result;
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("Maintenance check was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Maintenance check failed for {ObserverType}", Type.DisplayName);
            return ObserverResult.Failed(ex.Message);
        }
    }

    /// <summary>
    /// Gets the observed value from the external system.
    /// Implemented by each specific observer type.
    /// </summary>
    protected abstract Task<string> GetObservedValueAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Determines the result based on the observed value and configuration.
    /// </summary>
    protected virtual ObserverResult DetermineResult(string observedValue)
    {
        // Check if it matches the maintenance value
        if (string.Equals(observedValue, Config.MaintenanceValue, StringComparison.OrdinalIgnoreCase))
        {
            return ObserverResult.MaintenanceRequired(observedValue);
        }

        // If normalValue is specified, check for explicit normal confirmation
        if (!string.IsNullOrEmpty(Config.NormalValue))
        {
            if (string.Equals(observedValue, Config.NormalValue, StringComparison.OrdinalIgnoreCase))
            {
                return ObserverResult.NormalOperation(observedValue);
            }

            // Neither maintenance nor normal value matched - treat as error
            Logger.LogWarning(
                "Observed value '{ObservedValue}' matches neither maintenance value '{MaintenanceValue}' nor normal value '{NormalValue}'",
                observedValue, Config.MaintenanceValue, Config.NormalValue);

            return ObserverResult.Failed($"Unexpected value: {observedValue}");
        }

        // No normalValue specified - anything other than maintenance value is normal
        return ObserverResult.NormalOperation(observedValue);
    }
}
