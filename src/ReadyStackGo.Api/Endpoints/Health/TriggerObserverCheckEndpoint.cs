using FastEndpoints;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Deployments;

namespace ReadyStackGo.API.Endpoints.Health;

/// <summary>
/// POST /api/health/deployments/{deploymentId}/observer/check
/// Manually trigger a maintenance observer check for a deployment.
/// Returns the current check result immediately.
/// </summary>
[RequirePermission("Health", "Write")]
public class TriggerObserverCheckEndpoint : Endpoint<TriggerObserverCheckRequest, TriggerObserverCheckResponse>
{
    private readonly IMaintenanceObserverService _observerService;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly ILogger<TriggerObserverCheckEndpoint> _logger;

    public TriggerObserverCheckEndpoint(
        IMaintenanceObserverService observerService,
        IDeploymentRepository deploymentRepository,
        ILogger<TriggerObserverCheckEndpoint> logger)
    {
        _observerService = observerService;
        _deploymentRepository = deploymentRepository;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/api/health/deployments/{deploymentId}/observer/check");
        PreProcessor<RbacPreProcessor<TriggerObserverCheckRequest>>();
    }

    public override async Task HandleAsync(TriggerObserverCheckRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(req.DeploymentId, out var deploymentGuid))
        {
            ThrowError("Invalid deployment ID format", StatusCodes.Status400BadRequest);
            return;
        }

        var deploymentId = new DeploymentId(deploymentGuid);
        var deployment = _deploymentRepository.Get(deploymentId);

        if (deployment == null)
        {
            ThrowError("Deployment not found", StatusCodes.Status404NotFound);
            return;
        }

        _logger.LogInformation(
            "Manual observer check triggered for deployment {DeploymentId} ({StackName})",
            deploymentId, deployment.StackName);

        var result = await _observerService.CheckDeploymentObserverAsync(deploymentId, ct);

        if (result == null)
        {
            Response = new TriggerObserverCheckResponse
            {
                DeploymentId = req.DeploymentId,
                StackName = deployment.StackName,
                HasObserver = false,
                Message = "No maintenance observer configured for this deployment"
            };
            return;
        }

        Response = new TriggerObserverCheckResponse
        {
            DeploymentId = req.DeploymentId,
            StackName = deployment.StackName,
            HasObserver = true,
            Result = new ObserverResultResponse
            {
                IsSuccess = result.IsSuccess,
                IsMaintenanceRequired = result.IsMaintenanceRequired,
                ObservedValue = result.ObservedValue,
                ErrorMessage = result.ErrorMessage,
                CheckedAt = result.CheckedAt
            }
        };
    }
}

public class TriggerObserverCheckRequest
{
    public string DeploymentId { get; set; } = string.Empty;
}

public class TriggerObserverCheckResponse
{
    public string DeploymentId { get; set; } = string.Empty;
    public string StackName { get; set; } = string.Empty;
    public bool HasObserver { get; set; }
    public string? Message { get; set; }
    public ObserverResultResponse? Result { get; set; }
}
