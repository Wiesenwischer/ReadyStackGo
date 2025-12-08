using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Health;

namespace ReadyStackGo.Application.UseCases.Health.GetHealthHistory;

/// <summary>
/// Handler for GetHealthHistoryQuery.
/// Returns historical health snapshots for a deployment.
/// </summary>
public class GetHealthHistoryHandler
    : IRequestHandler<GetHealthHistoryQuery, GetHealthHistoryResponse>
{
    private readonly IHealthMonitoringService _healthMonitoringService;
    private readonly ILogger<GetHealthHistoryHandler> _logger;

    public GetHealthHistoryHandler(
        IHealthMonitoringService healthMonitoringService,
        ILogger<GetHealthHistoryHandler> logger)
    {
        _healthMonitoringService = healthMonitoringService;
        _logger = logger;
    }

    public async Task<GetHealthHistoryResponse> Handle(
        GetHealthHistoryQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Getting health history for deployment {DeploymentId}, limit {Limit}",
            request.DeploymentId, request.Limit);

        // Parse deployment ID
        if (!Guid.TryParse(request.DeploymentId, out var deploymentGuid))
        {
            return GetHealthHistoryResponse.Failure("Invalid deployment ID format");
        }

        var deploymentId = DeploymentId.FromGuid(deploymentGuid);

        // Get history
        var history = await _healthMonitoringService.GetHealthHistoryAsync(
            deploymentId, request.Limit, cancellationToken);

        // Map to DTOs
        var historyDtos = history.Select(MapToSummaryDto).ToList();

        return GetHealthHistoryResponse.Ok(historyDtos);
    }

    private static StackHealthSummaryDto MapToSummaryDto(HealthSnapshot snapshot)
    {
        return new StackHealthSummaryDto
        {
            DeploymentId = snapshot.DeploymentId.Value.ToString(),
            StackName = snapshot.StackName,
            CurrentVersion = snapshot.CurrentVersion,

            // Overall status (UI presentation handled in frontend)
            OverallStatus = snapshot.Overall.Name,

            // Operation mode
            OperationMode = snapshot.OperationMode.Name,

            // Services summary
            HealthyServices = snapshot.Self.HealthyCount,
            TotalServices = snapshot.Self.TotalCount,

            // Status
            StatusMessage = GenerateStatusMessage(snapshot),
            RequiresAttention = snapshot.Overall.RequiresAttention,
            CapturedAtUtc = snapshot.CapturedAtUtc
        };
    }

    private static string GenerateStatusMessage(HealthSnapshot snapshot)
    {
        if (snapshot.OperationMode != OperationMode.Normal)
        {
            return snapshot.OperationMode.Name;
        }

        var self = snapshot.Self;
        return $"{self.HealthyCount}/{self.TotalCount} healthy";
    }
}
