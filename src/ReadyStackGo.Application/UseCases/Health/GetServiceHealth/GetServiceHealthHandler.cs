using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using DomainHealthCheckConfig = ReadyStackGo.Domain.Deployment.RuntimeConfig.ServiceHealthCheckConfig;
using AppHealthCheckConfig = ReadyStackGo.Application.Services.ServiceHealthCheckConfig;

namespace ReadyStackGo.Application.UseCases.Health.GetServiceHealth;

/// <summary>
/// Handler for GetServiceHealthQuery.
/// Returns health data for a single service with full health check entries.
/// </summary>
public class GetServiceHealthHandler : IRequestHandler<GetServiceHealthQuery, GetServiceHealthResponse>
{
    private readonly IHealthMonitoringService _healthMonitoringService;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly ILogger<GetServiceHealthHandler> _logger;

    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(5);

    public GetServiceHealthHandler(
        IHealthMonitoringService healthMonitoringService,
        IDeploymentRepository deploymentRepository,
        IEnvironmentRepository environmentRepository,
        ILogger<GetServiceHealthHandler> logger)
    {
        _healthMonitoringService = healthMonitoringService;
        _deploymentRepository = deploymentRepository;
        _environmentRepository = environmentRepository;
        _logger = logger;
    }

    public async Task<GetServiceHealthResponse> Handle(
        GetServiceHealthQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Getting service health for {ServiceName} in deployment {DeploymentId}",
            request.ServiceName, request.DeploymentId);

        if (!Guid.TryParse(request.DeploymentId, out var deploymentGuid))
            return GetServiceHealthResponse.Failure("Invalid deployment ID format");

        if (!Guid.TryParse(request.EnvironmentId, out var environmentGuid))
            return GetServiceHealthResponse.Failure("Invalid environment ID format");

        var deploymentId = DeploymentId.FromGuid(deploymentGuid);
        var environmentId = EnvironmentId.FromGuid(environmentGuid);

        var deployment = _deploymentRepository.Get(deploymentId);
        if (deployment == null)
            return GetServiceHealthResponse.Failure($"Deployment {request.DeploymentId} not found");

        var environment = _environmentRepository.Get(environmentId);
        if (environment == null)
            return GetServiceHealthResponse.Failure($"Environment {request.EnvironmentId} not found");

        // Get latest snapshot (or capture fresh one if stale/forced)
        var snapshot = await _healthMonitoringService.GetLatestHealthSnapshotAsync(
            deploymentId, cancellationToken);

        bool needsRefresh = request.ForceRefresh ||
            snapshot == null ||
            (DateTime.UtcNow - snapshot.CapturedAtUtc > StaleThreshold);

        if (needsRefresh)
        {
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

        // Find the specific service in the snapshot
        var service = snapshot!.Self.Services
            .FirstOrDefault(s => string.Equals(s.Name, request.ServiceName, StringComparison.OrdinalIgnoreCase));

        if (service == null)
            return GetServiceHealthResponse.Failure($"Service '{request.ServiceName}' not found in deployment");

        // Map to DTO using the shared mapper logic
        var dto = HealthSnapshotMapper.MapServiceToDto(service);

        return GetServiceHealthResponse.Ok(dto, snapshot.StackName, snapshot.CapturedAtUtc);
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
                timeoutSeconds = (int)timeout.TotalSeconds;

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

/// <summary>
/// Response for getting service health detail.
/// </summary>
public class GetServiceHealthResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public ServiceHealthDto? Data { get; set; }
    public string? StackName { get; set; }
    public DateTime? CapturedAtUtc { get; set; }

    public static GetServiceHealthResponse Ok(ServiceHealthDto data, string stackName, DateTime capturedAtUtc) =>
        new() { Success = true, Data = data, StackName = stackName, CapturedAtUtc = capturedAtUtc };

    public static GetServiceHealthResponse Failure(string message) =>
        new() { Success = false, Message = message };
}
