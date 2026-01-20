using ReadyStackGo.Domain.Deployment.Deployments;

namespace ReadyStackGo.Api.BackgroundServices;

/// <summary>
/// Startup service that recovers deployments stuck in transitional states.
/// This handles the edge case where RSGO crashes during an upgrade/install operation,
/// leaving deployments in Installing or Upgrading status.
/// </summary>
public class DeploymentRecoveryService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DeploymentRecoveryService> _logger;

    public DeploymentRecoveryService(
        IServiceProvider serviceProvider,
        ILogger<DeploymentRecoveryService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deployment Recovery Service starting - checking for stuck deployments");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var deploymentRepository = scope.ServiceProvider.GetRequiredService<IDeploymentRepository>();

            // Find all deployments in transitional states
            var stuckDeployments = deploymentRepository
                .GetByStatuses(DeploymentStatus.Installing, DeploymentStatus.Upgrading)
                .ToList();

            if (stuckDeployments.Count == 0)
            {
                _logger.LogInformation("No stuck deployments found");
                return Task.CompletedTask;
            }

            _logger.LogWarning(
                "Found {Count} deployment(s) in transitional state. Marking as failed.",
                stuckDeployments.Count);

            foreach (var deployment in stuckDeployments)
            {
                var previousStatus = deployment.Status;
                var reason = previousStatus == DeploymentStatus.Installing
                    ? "Application restart during installation"
                    : "Application restart during upgrade";

                _logger.LogWarning(
                    "Recovering deployment {DeploymentId} ({StackName}): {PreviousStatus} -> Failed. Reason: {Reason}",
                    deployment.Id,
                    deployment.StackName,
                    previousStatus,
                    reason);

                deployment.MarkAsFailed(reason);
            }

            deploymentRepository.SaveChanges();

            _logger.LogInformation(
                "Successfully recovered {Count} stuck deployment(s)",
                stuckDeployments.Count);
        }
        catch (Exception ex)
        {
            // Log but don't throw - we don't want to prevent app startup
            _logger.LogError(ex, "Error during deployment recovery - application will continue");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
