using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Deployments;

namespace ReadyStackGo.Application.UseCases.Health.GetHealthTransitions;

/// <summary>
/// Handler for GetHealthTransitionsQuery.
/// Returns only the snapshots where overall health status changed.
/// </summary>
public class GetHealthTransitionsHandler
    : IRequestHandler<GetHealthTransitionsQuery, GetHealthTransitionsResponse>
{
    private readonly IHealthMonitoringService _healthMonitoringService;
    private readonly ILogger<GetHealthTransitionsHandler> _logger;

    public GetHealthTransitionsHandler(
        IHealthMonitoringService healthMonitoringService,
        ILogger<GetHealthTransitionsHandler> logger)
    {
        _healthMonitoringService = healthMonitoringService;
        _logger = logger;
    }

    public async Task<GetHealthTransitionsResponse> Handle(
        GetHealthTransitionsQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Getting health transitions for deployment {DeploymentId}",
            request.DeploymentId);

        if (!Guid.TryParse(request.DeploymentId, out var deploymentGuid))
        {
            return GetHealthTransitionsResponse.Failure("Invalid deployment ID format");
        }

        var deploymentId = DeploymentId.FromGuid(deploymentGuid);

        var transitions = await _healthMonitoringService.GetHealthTransitionsAsync(
            deploymentId, cancellationToken);

        var transitionDtos = transitions.Select(snapshot => new HealthTransitionDto
        {
            OverallStatus = snapshot.Overall.Name,
            OperationMode = snapshot.OperationMode.Name,
            HealthyServices = snapshot.Self.HealthyCount,
            TotalServices = snapshot.Self.TotalCount,
            CapturedAtUtc = snapshot.CapturedAtUtc,
            Services = snapshot.Self.Services.Select(s => new ServiceTransitionDto
            {
                Name = s.Name,
                Status = s.Status.Name,
            }).ToList(),
        }).ToList();

        return GetHealthTransitionsResponse.Ok(transitionDtos);
    }
}
