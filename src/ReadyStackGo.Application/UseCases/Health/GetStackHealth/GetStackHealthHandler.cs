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
                cancellationToken);
        }

        // Map to DTO - snapshot is guaranteed to be non-null after CaptureHealthSnapshotAsync
        var dto = MapToDto(snapshot!, environmentId);
        return GetStackHealthResponse.Ok(dto);
    }

    private bool IsSnapshotStale(HealthSnapshot snapshot)
    {
        return DateTime.UtcNow - snapshot.CapturedAtUtc > StaleThreshold;
    }

    private static StackHealthDto MapToDto(HealthSnapshot snapshot, EnvironmentId environmentId)
    {
        return new StackHealthDto
        {
            DeploymentId = snapshot.DeploymentId.Value.ToString(),
            EnvironmentId = environmentId.Value.ToString(),
            StackName = snapshot.StackName,
            CurrentVersion = snapshot.CurrentVersion,
            TargetVersion = snapshot.TargetVersion,

            // Overall status
            OverallStatus = snapshot.Overall.Name,
            OverallStatusColor = snapshot.Overall.CssColorClass,
            OverallStatusIcon = GetStatusIcon(snapshot.Overall),

            // Operation mode
            OperationMode = snapshot.OperationMode.Name,
            OperationModeColor = snapshot.OperationMode.CssColorClass,
            OperationModeIcon = GetOperationModeIcon(snapshot.OperationMode),

            // Summary
            StatusMessage = GenerateStatusMessage(snapshot),
            RequiresAttention = snapshot.Overall.RequiresAttention,
            CapturedAtUtc = snapshot.CapturedAtUtc,

            // Self health
            Self = MapSelfHealth(snapshot.Self),

            // Optional: Bus health
            Bus = snapshot.Bus != null ? MapBusHealth(snapshot.Bus) : null,

            // Optional: Infra health
            Infra = snapshot.Infra != null ? MapInfraHealth(snapshot.Infra) : null
        };
    }

    private static SelfHealthDto MapSelfHealth(SelfHealth self)
    {
        return new SelfHealthDto
        {
            Status = self.Status.Name,
            HealthyCount = self.HealthyCount,
            TotalCount = self.TotalCount,
            Services = self.Services.Select(MapServiceHealth).ToList()
        };
    }

    private static ServiceHealthDto MapServiceHealth(ServiceHealth service)
    {
        return new ServiceHealthDto
        {
            Name = service.Name,
            Status = service.Status.Name,
            StatusColor = service.Status.CssColorClass,
            StatusIcon = GetStatusIcon(service.Status),
            ContainerId = service.ContainerId,
            ContainerName = service.ContainerName,
            Reason = service.Reason,
            RestartCount = service.RestartCount
        };
    }

    private static BusHealthDto? MapBusHealth(BusHealth bus)
    {
        return new BusHealthDto
        {
            Status = bus.Status.Name,
            TransportKey = bus.TransportKey,
            HasCriticalError = bus.HasCriticalError,
            CriticalErrorMessage = bus.CriticalErrorMessage,
            LastHealthPingProcessedUtc = bus.LastHealthPingProcessedUtc,
            Endpoints = bus.Endpoints.Select(e => new BusEndpointHealthDto
            {
                EndpointName = e.EndpointName,
                Status = e.Status.Name,
                LastPingUtc = e.LastPingUtc,
                Reason = e.Reason
            }).ToList()
        };
    }

    private static InfraHealthDto? MapInfraHealth(InfraHealth infra)
    {
        return new InfraHealthDto
        {
            Status = infra.Status.Name,
            Databases = infra.Databases.Select(d => new DatabaseHealthDto
            {
                Id = d.Id,
                Status = d.Status.Name,
                LatencyMs = d.LatencyMs,
                Error = d.Error
            }).ToList(),
            Disks = infra.Disks.Select(d => new DiskHealthDto
            {
                Mount = d.Mount,
                Status = d.Status.Name,
                FreePercent = d.FreePercent,
                Error = d.Error
            }).ToList(),
            ExternalServices = infra.ExternalServices.Select(s => new ExternalServiceHealthDto
            {
                Id = s.Id,
                Status = s.Status.Name,
                Error = s.Error,
                ResponseTimeMs = s.ResponseTimeMs
            }).ToList()
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
        if (snapshot.OperationMode == OperationMode.Migrating)
        {
            return $"Migrating from {snapshot.CurrentVersion ?? "unknown"} to {snapshot.TargetVersion ?? "unknown"}";
        }

        if (snapshot.OperationMode == OperationMode.Maintenance)
        {
            return "Stack is in maintenance mode";
        }

        if (snapshot.OperationMode == OperationMode.Stopped)
        {
            return "Stack is stopped";
        }

        if (snapshot.OperationMode == OperationMode.Failed)
        {
            return "Stack deployment failed";
        }

        // Normal operation - describe health
        var self = snapshot.Self;
        if (snapshot.Overall == HealthStatus.Healthy)
        {
            return $"All {self.TotalCount} services healthy";
        }

        if (snapshot.Overall == HealthStatus.Degraded)
        {
            var degradedCount = self.TotalCount - self.HealthyCount;
            return $"{degradedCount} of {self.TotalCount} services degraded";
        }

        if (snapshot.Overall == HealthStatus.Unhealthy)
        {
            var unhealthyCount = self.Services.Count(s => s.Status == HealthStatus.Unhealthy);
            return $"{unhealthyCount} of {self.TotalCount} services unhealthy";
        }

        return "Health status unknown";
    }
}
