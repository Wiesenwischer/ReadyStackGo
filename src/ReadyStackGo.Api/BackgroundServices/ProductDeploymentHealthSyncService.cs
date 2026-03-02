using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Api.BackgroundServices;

/// <summary>
/// Background service that periodically synchronizes ProductDeployment status
/// with the underlying Deployment aggregates (eventual consistency).
///
/// Corrects drift between ProductStackDeployment.Status and Deployment.Status
/// (e.g., when a container crashes and the Deployment transitions to Failed).
/// </summary>
public class ProductDeploymentHealthSyncService : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan SyncInterval = TimeSpan.FromSeconds(60);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProductDeploymentHealthSyncService> _logger;

    public ProductDeploymentHealthSyncService(
        IServiceProvider serviceProvider,
        ILogger<ProductDeploymentHealthSyncService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProductDeployment Health Sync Service starting");

        await Task.Delay(InitialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncHealthAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during product deployment health sync cycle");
            }

            try
            {
                await Task.Delay(SyncInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("ProductDeployment Health Sync Service stopped");
    }

    private async Task SyncHealthAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var productRepo = scope.ServiceProvider.GetRequiredService<IProductDeploymentRepository>();
        var deploymentRepo = scope.ServiceProvider.GetRequiredService<IDeploymentRepository>();

        var activeDeployments = productRepo.GetAllActive().ToList();

        foreach (var productDeployment in activeDeployments)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (!productDeployment.IsOperational) continue;

            var changed = false;

            foreach (var stack in productDeployment.Stacks)
            {
                if (stack.DeploymentId == null) continue;

                var deployment = deploymentRepo.Get(stack.DeploymentId);
                if (deployment == null) continue;

                var targetStatus = MapDeploymentStatus(deployment.Status);
                if (targetStatus == null) continue;

                var stackChanged = productDeployment.SyncStackHealth(
                    stack.StackName,
                    targetStatus.Value,
                    deployment.Status == DeploymentStatus.Failed ? "Detected by health sync" : null);

                if (stackChanged)
                {
                    _logger.LogInformation(
                        "Health sync: Stack '{StackName}' in product '{ProductName}' status corrected from {OldStatus} to {NewStatus}",
                        stack.StackName, productDeployment.ProductName, stack.Status, targetStatus.Value);
                    changed = true;
                }
            }

            if (changed)
            {
                var productChanged = productDeployment.RecalculateProductStatus();
                if (productChanged)
                {
                    _logger.LogInformation(
                        "Health sync: Product '{ProductName}' status recalculated to {Status}",
                        productDeployment.ProductName, productDeployment.Status);
                }

                productRepo.Update(productDeployment);
                productRepo.SaveChanges();
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Maps a Deployment.Status to the corresponding StackDeploymentStatus.
    /// Returns null for in-progress statuses (Installing/Upgrading) — those are managed by the handlers.
    /// </summary>
    private static StackDeploymentStatus? MapDeploymentStatus(DeploymentStatus status) => status switch
    {
        DeploymentStatus.Running => StackDeploymentStatus.Running,
        DeploymentStatus.Failed => StackDeploymentStatus.Failed,
        DeploymentStatus.Removed => StackDeploymentStatus.Removed,
        _ => null // Installing, Upgrading — don't interfere with active operations
    };
}
