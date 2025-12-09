using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Api.BackgroundServices;

/// <summary>
/// Background service that runs maintenance observers for all deployments.
/// Each deployment with a configured maintenanceObserver is monitored independently.
/// </summary>
public class MaintenanceObserverBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MaintenanceObserverBackgroundService> _logger;

    public MaintenanceObserverBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<MaintenanceObserverBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Maintenance Observer Background Service starting");

        // Initial delay to let the application start up
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunObserversAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during maintenance observer cycle");
            }

            try
            {
                // Base polling interval - individual observers may have different intervals
                // This just ensures we check for new deployments periodically
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Maintenance Observer Background Service stopped");
    }

    private async Task RunObserversAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var observerService = scope.ServiceProvider.GetRequiredService<IMaintenanceObserverService>();

        await observerService.CheckAllObserversAsync(stoppingToken);
    }
}
