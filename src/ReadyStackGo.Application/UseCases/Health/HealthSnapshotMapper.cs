using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;

namespace ReadyStackGo.Application.UseCases.Health;

/// <summary>
/// Maps HealthSnapshot domain objects to DTOs.
/// Shared by HealthCollectorService and GetStackHealthHandler.
/// </summary>
public static class HealthSnapshotMapper
{
    public static StackHealthDto MapToStackHealthDto(HealthSnapshot snapshot, EnvironmentId environmentId)
    {
        return new StackHealthDto
        {
            DeploymentId = snapshot.DeploymentId.Value.ToString(),
            EnvironmentId = environmentId.Value.ToString(),
            StackName = snapshot.StackName,
            CurrentVersion = snapshot.CurrentVersion,
            TargetVersion = snapshot.TargetVersion,

            // Overall status (UI presentation handled in frontend)
            OverallStatus = snapshot.Overall.Name,

            // Operation mode
            OperationMode = snapshot.OperationMode.Name,

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

    public static StackHealthSummaryDto MapToStackHealthSummary(HealthSnapshot snapshot)
    {
        return new StackHealthSummaryDto
        {
            DeploymentId = snapshot.DeploymentId.Value.ToString(),
            StackName = snapshot.StackName,
            CurrentVersion = snapshot.CurrentVersion,
            OverallStatus = snapshot.Overall.Name,
            OperationMode = snapshot.OperationMode.Name,
            HealthyServices = snapshot.Self.HealthyCount,
            TotalServices = snapshot.Self.TotalCount,
            StatusMessage = snapshot.GetStatusMessage(),
            RequiresAttention = snapshot.RequiresAttention,
            CapturedAtUtc = snapshot.CapturedAtUtc
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

    private static string GenerateStatusMessage(HealthSnapshot snapshot)
    {
        if (snapshot.OperationMode == OperationMode.Maintenance)
        {
            return "Stack is in maintenance mode";
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
