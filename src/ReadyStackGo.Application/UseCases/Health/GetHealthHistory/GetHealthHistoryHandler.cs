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

            // Overall status
            OverallStatus = snapshot.Overall.Name,
            OverallStatusColor = snapshot.Overall.CssColorClass,
            OverallStatusIcon = GetStatusIcon(snapshot.Overall),

            // Operation mode
            OperationMode = snapshot.OperationMode.Name,
            OperationModeColor = snapshot.OperationMode.CssColorClass,
            OperationModeIcon = GetOperationModeIcon(snapshot.OperationMode),

            // Services summary
            HealthyServices = snapshot.Self.HealthyCount,
            TotalServices = snapshot.Self.TotalCount,

            // Status
            StatusMessage = GenerateStatusMessage(snapshot),
            RequiresAttention = snapshot.Overall.RequiresAttention,
            CapturedAtUtc = snapshot.CapturedAtUtc
        };
    }

    private static string GetStatusIcon(HealthStatus status)
    {
        if (status == HealthStatus.Healthy) return "check-circle";
        if (status == HealthStatus.Degraded) return "alert-triangle";
        if (status == HealthStatus.Unhealthy) return "x-circle";
        return "help-circle";
    }

    private static string GetOperationModeIcon(OperationMode mode)
    {
        if (mode == OperationMode.Normal) return "play";
        if (mode == OperationMode.Migrating) return "refresh-cw";
        if (mode == OperationMode.Maintenance) return "tool";
        if (mode == OperationMode.Stopped) return "square";
        if (mode == OperationMode.Failed) return "alert-octagon";
        return "help-circle";
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
