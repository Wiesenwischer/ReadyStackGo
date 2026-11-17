using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Deployment.Observers;

namespace ReadyStackGo.Application.Services.Impl;

/// <summary>
/// Creates the configured setter via the factory and invokes it, turning any exception into a
/// non-fatal failure result. No-ops (skips) when no setter is configured.
/// </summary>
public class MaintenanceSetterService : IMaintenanceSetterService
{
    private readonly IMaintenanceSetterFactory _factory;
    private readonly ILogger<MaintenanceSetterService> _logger;

    public MaintenanceSetterService(
        IMaintenanceSetterFactory factory,
        ILogger<MaintenanceSetterService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<SetterResult> ApplyAsync(
        MaintenanceSetterConfig? config,
        MaintenanceState state,
        CancellationToken cancellationToken = default)
    {
        if (config == null)
        {
            return SetterResult.WasSkipped("No maintenance setter configured");
        }

        if (!_factory.IsSupported(config.Type))
        {
            _logger.LogWarning("Maintenance setter type {Type} is not supported", config.Type);
            return SetterResult.Failed($"Unsupported setter type: {config.Type}");
        }

        try
        {
            var setter = _factory.Create(config);
            var result = await setter.SetAsync(state, cancellationToken);

            if (!result.Success)
            {
                // The setter itself logs details (without secrets); keep this terse.
                _logger.LogWarning("Maintenance setter ({Type}) failed for state {State}: {Error}",
                    config.Type, state, result.Error);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Maintenance setter ({Type}) threw while applying state {State}",
                config.Type, state);
            return SetterResult.Failed(ex.Message);
        }
    }
}
