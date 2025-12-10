using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;

namespace ReadyStackGo.Application.UseCases.Health.GetStackHealth;

/// <summary>
/// Handler for GetStackHealthQuery.
/// Returns cached health data or captures a fresh snapshot if requested.
/// </summary>
public class GetStackHealthHandler : IRequestHandler<GetStackHealthQuery, GetStackHealthResponse>
{
    private readonly IHealthMonitoringService _healthMonitoringService;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly ILogger<GetStackHealthHandler> _logger;

    // Cache threshold - if snapshot is older than this, consider it stale
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(5);

    public GetStackHealthHandler(
        IHealthMonitoringService healthMonitoringService,
        IDeploymentRepository deploymentRepository,
        IEnvironmentRepository environmentRepository,
        ILogger<GetStackHealthHandler> logger)
    {
        _healthMonitoringService = healthMonitoringService;
        _deploymentRepository = deploymentRepository;
        _environmentRepository = environmentRepository;
        _logger = logger;
    }

    public async Task<GetStackHealthResponse> Handle(
        GetStackHealthQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Getting stack health for deployment {DeploymentId} in environment {EnvironmentId}",
            request.DeploymentId, request.EnvironmentId);

        // Parse IDs
        if (!Guid.TryParse(request.DeploymentId, out var deploymentGuid))
        {
            return GetStackHealthResponse.Failure("Invalid deployment ID format");
        }

        if (!Guid.TryParse(request.EnvironmentId, out var environmentGuid))
        {
            return GetStackHealthResponse.Failure("Invalid environment ID format");
        }

        var deploymentId = DeploymentId.FromGuid(deploymentGuid);
        var environmentId = EnvironmentId.FromGuid(environmentGuid);

        // Get deployment to verify it exists and get stack info
        var deployment = _deploymentRepository.Get(deploymentId);
        if (deployment == null)
        {
            return GetStackHealthResponse.Failure($"Deployment {request.DeploymentId} not found");
        }

        // Get environment to get organization ID
        var environment = _environmentRepository.Get(environmentId);
        if (environment == null)
        {
            return GetStackHealthResponse.Failure($"Environment {request.EnvironmentId} not found");
        }

        // Get latest snapshot
        var snapshot = await _healthMonitoringService.GetLatestHealthSnapshotAsync(
            deploymentId, cancellationToken);

        // Check if we need a fresh snapshot
        bool needsRefresh = request.ForceRefresh ||
            snapshot == null ||
            IsSnapshotStale(snapshot);

        if (needsRefresh)
        {
            _logger.LogDebug("Capturing fresh health snapshot for deployment {DeploymentId}", deploymentId);

            snapshot = await _healthMonitoringService.CaptureHealthSnapshotAsync(
                environment.OrganizationId,
                environmentId,
                deploymentId,
                deployment.StackName,
                deployment.StackVersion,
                serviceHealthConfigs: null, // TODO: Load from stack definition
                cancellationToken);
        }

        // Map to DTO - snapshot is guaranteed to be non-null after CaptureHealthSnapshotAsync
        var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot!, environmentId);
        return GetStackHealthResponse.Ok(dto);
    }

    private bool IsSnapshotStale(HealthSnapshot snapshot)
    {
        return DateTime.UtcNow - snapshot.CapturedAtUtc > StaleThreshold;
    }
}
