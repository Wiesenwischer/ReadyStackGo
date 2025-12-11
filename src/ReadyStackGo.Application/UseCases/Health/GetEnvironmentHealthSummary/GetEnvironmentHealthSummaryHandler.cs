using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;

namespace ReadyStackGo.Application.UseCases.Health.GetEnvironmentHealthSummary;

/// <summary>
/// Handler for GetEnvironmentHealthSummaryQuery.
/// Uses domain value objects for aggregation logic.
/// </summary>
public class GetEnvironmentHealthSummaryHandler
    : IRequestHandler<GetEnvironmentHealthSummaryQuery, GetEnvironmentHealthSummaryResponse>
{
    private readonly IHealthSnapshotRepository _healthSnapshotRepository;
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly ILogger<GetEnvironmentHealthSummaryHandler> _logger;

    public GetEnvironmentHealthSummaryHandler(
        IHealthSnapshotRepository healthSnapshotRepository,
        IEnvironmentRepository environmentRepository,
        IDeploymentRepository deploymentRepository,
        ILogger<GetEnvironmentHealthSummaryHandler> logger)
    {
        _healthSnapshotRepository = healthSnapshotRepository;
        _environmentRepository = environmentRepository;
        _deploymentRepository = deploymentRepository;
        _logger = logger;
    }

    public Task<GetEnvironmentHealthSummaryResponse> Handle(
        GetEnvironmentHealthSummaryQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting environment health summary for {EnvironmentId}", request.EnvironmentId);

        // Parse environment ID
        if (!Guid.TryParse(request.EnvironmentId, out var environmentGuid))
        {
            return Task.FromResult(
                GetEnvironmentHealthSummaryResponse.Failure("Invalid environment ID format"));
        }

        var environmentId = EnvironmentId.FromGuid(environmentGuid);

        // Get environment to verify it exists
        var environment = _environmentRepository.Get(environmentId);
        if (environment == null)
        {
            return Task.FromResult(
                GetEnvironmentHealthSummaryResponse.Failure($"Environment {request.EnvironmentId} not found"));
        }

        // Get latest snapshots via repository
        var snapshots = _healthSnapshotRepository.GetLatestForEnvironment(environmentId);

        // Get active deployment IDs (exclude removed deployments)
        var activeDeploymentIds = _deploymentRepository
            .GetByEnvironment(environmentId)
            .Where(d => d.Status != DeploymentStatus.Removed)
            .Select(d => d.Id)
            .ToHashSet();

        // Filter snapshots to only include active deployments
        var activeSnapshots = snapshots.Where(s => activeDeploymentIds.Contains(s.DeploymentId));

        // Use domain value object for aggregation
        var summary = EnvironmentHealthSummary.FromSnapshots(environment, activeSnapshots);

        // Map domain object to DTO
        var dto = MapToDto(summary);

        return Task.FromResult(GetEnvironmentHealthSummaryResponse.Ok(dto));
    }

    private static EnvironmentHealthSummaryDto MapToDto(EnvironmentHealthSummary summary)
    {
        return new EnvironmentHealthSummaryDto
        {
            EnvironmentId = summary.EnvironmentId.Value.ToString(),
            EnvironmentName = summary.EnvironmentName,
            TotalStacks = summary.TotalStacks,
            HealthyCount = summary.HealthyCount,
            DegradedCount = summary.DegradedCount,
            UnhealthyCount = summary.UnhealthyCount,
            Stacks = summary.Stacks.Select(MapStackSummaryToDto).ToList()
        };
    }

    private static StackHealthSummaryDto MapStackSummaryToDto(StackHealthSummary stack)
    {
        return new StackHealthSummaryDto
        {
            DeploymentId = stack.DeploymentId.Value.ToString(),
            StackName = stack.StackName,
            CurrentVersion = stack.CurrentVersion,

            // Overall status (UI presentation handled in frontend)
            OverallStatus = stack.OverallStatus.Name,

            // Operation mode
            OperationMode = stack.OperationMode.Name,

            // Services summary
            HealthyServices = stack.HealthyServices,
            TotalServices = stack.TotalServices,

            // Status - using domain behavior
            StatusMessage = stack.StatusMessage,
            RequiresAttention = stack.RequiresAttention,
            CapturedAtUtc = stack.CapturedAtUtc
        };
    }
}
