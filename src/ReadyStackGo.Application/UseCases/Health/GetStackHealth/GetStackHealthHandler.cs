using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using DomainHealthCheckConfig = ReadyStackGo.Domain.Deployment.RuntimeConfig.ServiceHealthCheckConfig;
using AppHealthCheckConfig = ReadyStackGo.Application.Services.ServiceHealthCheckConfig;

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
    private readonly IProductDeploymentRepository _productDeploymentRepository;
    private readonly ILogger<GetStackHealthHandler> _logger;

    // Cache threshold - if snapshot is older than this, consider it stale
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(5);

    public GetStackHealthHandler(
        IHealthMonitoringService healthMonitoringService,
        IDeploymentRepository deploymentRepository,
        IEnvironmentRepository environmentRepository,
        IProductDeploymentRepository productDeploymentRepository,
        ILogger<GetStackHealthHandler> logger)
    {
        _healthMonitoringService = healthMonitoringService;
        _deploymentRepository = deploymentRepository;
        _environmentRepository = environmentRepository;
        _productDeploymentRepository = productDeploymentRepository;
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

            var serviceHealthConfigs = MapHealthCheckConfigs(deployment.HealthCheckConfigs);

            snapshot = await _healthMonitoringService.CaptureHealthSnapshotAsync(
                environment.OrganizationId,
                environmentId,
                deploymentId,
                deployment.StackName,
                deployment.StackVersion,
                serviceHealthConfigs,
                cancellationToken);
        }

        // Map to DTO - snapshot is guaranteed to be non-null after CaptureHealthSnapshotAsync
        var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot!, environmentId);

        // Enrich with product info
        var productDeployment = _productDeploymentRepository.GetByStackDeploymentId(deploymentId);
        if (productDeployment != null && !productDeployment.IsTerminal)
        {
            dto.ProductDeploymentId = productDeployment.Id.Value.ToString();
            dto.ProductDisplayName = productDeployment.ProductDisplayName;
        }

        return GetStackHealthResponse.Ok(dto);
    }

    private static bool IsSnapshotStale(HealthSnapshot snapshot)
    {
        return DateTime.UtcNow - snapshot.CapturedAtUtc > StaleThreshold;
    }

    private static IReadOnlyDictionary<string, AppHealthCheckConfig>? MapHealthCheckConfigs(
        IReadOnlyCollection<DomainHealthCheckConfig>? domainConfigs)
    {
        if (domainConfigs == null || domainConfigs.Count == 0)
            return null;

        var result = new Dictionary<string, AppHealthCheckConfig>();

        foreach (var config in domainConfigs)
        {
            var timeoutSeconds = 5;
            if (!string.IsNullOrEmpty(config.Timeout) && TimeSpan.TryParse(config.Timeout, out var timeout))
            {
                timeoutSeconds = (int)timeout.TotalSeconds;
            }

            result[config.ServiceName] = new AppHealthCheckConfig
            {
                Type = config.Type,
                Path = config.Path ?? "/hc",
                Port = config.Port,
                TimeoutSeconds = timeoutSeconds,
                UseHttps = config.Https,
                ExpectedStatusCodes = config.ExpectedStatusCodes ?? new[] { 200 }
            };
        }

        return result.Count > 0 ? result : null;
    }
}
