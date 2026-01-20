using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Deployment.Deployments;

namespace ReadyStackGo.Application.UseCases.Deployments.MarkDeploymentFailed;

/// <summary>
/// Handler for manually marking a deployment as failed.
/// Only allows marking deployments in transitional states (Installing/Upgrading).
/// </summary>
public class MarkDeploymentFailedHandler : IRequestHandler<MarkDeploymentFailedCommand, MarkDeploymentFailedResponse>
{
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly ILogger<MarkDeploymentFailedHandler> _logger;

    public MarkDeploymentFailedHandler(
        IDeploymentRepository deploymentRepository,
        ILogger<MarkDeploymentFailedHandler> logger)
    {
        _deploymentRepository = deploymentRepository;
        _logger = logger;
    }

    public Task<MarkDeploymentFailedResponse> Handle(MarkDeploymentFailedCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "MarkDeploymentFailed: Request received for deployment {DeploymentId}",
            request.DeploymentId);

        // Parse deployment ID
        if (!Guid.TryParse(request.DeploymentId, out var deploymentGuid))
        {
            _logger.LogWarning("MarkDeploymentFailed: Invalid deployment ID format: {DeploymentId}", request.DeploymentId);
            return Task.FromResult(new MarkDeploymentFailedResponse(false, "Invalid deployment ID format."));
        }

        // Get deployment
        var deployment = _deploymentRepository.GetById(new DeploymentId(deploymentGuid));
        if (deployment == null)
        {
            _logger.LogWarning("MarkDeploymentFailed: Deployment not found: {DeploymentId}", request.DeploymentId);
            return Task.FromResult(new MarkDeploymentFailedResponse(false, "Deployment not found."));
        }

        var previousStatus = deployment.Status.ToString();

        // Only allow marking as failed if in transitional state
        if (deployment.Status != DeploymentStatus.Installing && deployment.Status != DeploymentStatus.Upgrading)
        {
            _logger.LogWarning(
                "MarkDeploymentFailed: Deployment {DeploymentId} is not in a transitional state. Status: {Status}",
                request.DeploymentId, deployment.Status);
            return Task.FromResult(new MarkDeploymentFailedResponse(
                false,
                $"Deployment can only be marked as failed when in Installing or Upgrading status. Current status: {deployment.Status}"));
        }

        // Mark as failed
        var reason = request.Reason ?? "Manually marked as failed by user";
        _logger.LogInformation(
            "MarkDeploymentFailed: Marking deployment {DeploymentId} ({StackName}) as failed. Previous status: {PreviousStatus}, Reason: {Reason}",
            request.DeploymentId, deployment.StackName, previousStatus, reason);

        deployment.MarkAsFailed(reason);
        _deploymentRepository.SaveChanges();

        _logger.LogInformation(
            "MarkDeploymentFailed: Successfully marked deployment {DeploymentId} as failed",
            request.DeploymentId);

        return Task.FromResult(new MarkDeploymentFailedResponse(
            true,
            $"Deployment marked as failed. Previous status: {previousStatus}",
            previousStatus));
    }
}
