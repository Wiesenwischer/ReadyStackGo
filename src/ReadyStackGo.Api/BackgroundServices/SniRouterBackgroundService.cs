using ReadyStackGo.Application.Services.Edge;

namespace ReadyStackGo.Api.BackgroundServices;

/// <summary>
/// Drives the optional shared SNI passthrough router reconciler (Phase 4). Only registered when
/// the feature is enabled, so it is fully inert by default.
/// </summary>
public class SniRouterBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SniRouterBackgroundService> _logger;

    public SniRouterBackgroundService(IServiceProvider serviceProvider, ILogger<SniRouterBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SNI Router Background Service starting");
        await Task.Delay(TimeSpan.FromSeconds(12), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var reconciler = scope.ServiceProvider.GetRequiredService<ISniRouterReconciler>();
                await reconciler.ReconcileAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during SNI router reconcile cycle");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }

        _logger.LogInformation("SNI Router Background Service stopped");
    }
}
