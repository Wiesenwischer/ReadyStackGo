using ReadyStackGo.Application.Services.Edge;

namespace ReadyStackGo.Api.BackgroundServices;

/// <summary>
/// Background service that drives the edge reconciler. For every active product deployment
/// with an edge configured, it ensures the edge container exists and pushes the desired
/// Caddy config when it changes. Mirrors <see cref="MaintenanceObserverBackgroundService"/>.
///
/// The whole feature is dormant unless a product opts in via its manifest <c>edge:</c> block,
/// so this loop is a no-op for installations that do not use the edge.
/// </summary>
public class EdgeReconcilerBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EdgeReconcilerBackgroundService> _logger;

    public EdgeReconcilerBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<EdgeReconcilerBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Edge Reconciler Background Service starting");

        // Initial delay to let the application and Docker connectivity settle.
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var reconciler = scope.ServiceProvider.GetRequiredService<IEdgeReconciler>();
                await reconciler.ReconcileAllAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during edge reconcile cycle");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Edge Reconciler Background Service stopped");
    }
}
