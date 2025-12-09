using FastEndpoints;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Deployments;

namespace ReadyStackGo.API.Endpoints.Health;

/// <summary>
/// GET /api/health/deployments/{deploymentId}/observer
/// Get the current maintenance observer status for a deployment.
/// Returns the last check result and observer configuration.
/// </summary>
[RequirePermission("Health", "Read")]
public class GetObserverStatusEndpoint : Endpoint<GetObserverStatusRequest, GetObserverStatusResponse>
{
    private readonly IMaintenanceObserverService _observerService;
    private readonly IDeploymentRepository _deploymentRepository;

    public GetObserverStatusEndpoint(
        IMaintenanceObserverService observerService,
        IDeploymentRepository deploymentRepository)
    {
        _observerService = observerService;
        _deploymentRepository = deploymentRepository;
    }

    public override void Configure()
    {
        Get("/api/health/deployments/{deploymentId}/observer");
        PreProcessor<RbacPreProcessor<GetObserverStatusRequest>>();
    }

    public override async Task HandleAsync(GetObserverStatusRequest req, CancellationToken ct)
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

        var lastResult = await _observerService.GetLastResultAsync(deploymentId);

        Response = new GetObserverStatusResponse
        {
            DeploymentId = req.DeploymentId,
            StackName = deployment.StackName,
            HasObserver = lastResult != null,
            LastResult = lastResult != null
                ? new ObserverResultResponse
                {
                    IsSuccess = lastResult.IsSuccess,
                    IsMaintenanceRequired = lastResult.IsMaintenanceRequired,
                    ObservedValue = lastResult.ObservedValue,
                    ErrorMessage = lastResult.ErrorMessage,
                    CheckedAt = lastResult.CheckedAt
                }
                : null
        };
    }
}

public class GetObserverStatusRequest
{
    public string DeploymentId { get; set; } = string.Empty;
}

public class GetObserverStatusResponse
{
    public string DeploymentId { get; set; } = string.Empty;
    public string StackName { get; set; } = string.Empty;
    public bool HasObserver { get; set; }
    public ObserverResultResponse? LastResult { get; set; }
}

public class ObserverResultResponse
{
    public bool IsSuccess { get; set; }
    public bool IsMaintenanceRequired { get; set; }
    public string? ObservedValue { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CheckedAt { get; set; }
}
