namespace ReadyStackGo.Infrastructure.DataAccess.Configurations;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;

/// <summary>
/// EF Core configuration for HealthSnapshot aggregate.
/// Complex value objects (Bus, Infra, Self) are stored as JSON.
/// </summary>
public class HealthSnapshotConfiguration : IEntityTypeConfiguration<HealthSnapshot>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public void Configure(EntityTypeBuilder<HealthSnapshot> builder)
    {
        builder.ToTable("HealthSnapshots");

        builder.HasKey(h => h.Id);

        // Primary key conversion
        builder.Property(h => h.Id)
            .HasConversion(
                id => id.Value,
                value => new HealthSnapshotId(value))
            .IsRequired();

        // Foreign key conversions
        builder.Property(h => h.OrganizationId)
            .HasConversion(
                id => id.Value,
                value => new OrganizationId(value))
            .IsRequired();

        builder.Property(h => h.EnvironmentId)
            .HasConversion(
                id => id.Value,
                value => new EnvironmentId(value))
            .IsRequired();

        builder.Property(h => h.DeploymentId)
            .HasConversion(
                id => id.Value,
                value => new DeploymentId(value))
            .IsRequired();

        // Simple properties
        builder.Property(h => h.StackName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(h => h.CapturedAtUtc)
            .IsRequired();

        builder.Property(h => h.CurrentVersion)
            .HasMaxLength(50);

        builder.Property(h => h.TargetVersion)
            .HasMaxLength(50);

        // Smart enum conversions (store as int)
        builder.Property(h => h.Overall)
            .HasConversion(
                status => status.Value,
                value => HealthStatus.FromValue(value))
            .HasColumnName("OverallStatus")
            .IsRequired();

        builder.Property(h => h.OperationMode)
            .HasConversion(
                mode => mode.Value,
                value => OperationMode.FromValue(value))
            .HasColumnName("OperationMode")
            .IsRequired();

        // Complex value objects stored as JSON
        builder.Property(h => h.Self)
            .HasConversion(
                self => SerializeSelfHealth(self),
                json => DeserializeSelfHealth(json))
            .HasColumnName("SelfHealthJson")
            .HasColumnType("TEXT");

        builder.Property(h => h.Bus)
            .HasConversion(
                bus => SerializeBusHealth(bus),
                json => DeserializeBusHealth(json))
            .HasColumnName("BusHealthJson")
            .HasColumnType("TEXT");

        builder.Property(h => h.Infra)
            .HasConversion(
                infra => SerializeInfraHealth(infra),
                json => DeserializeInfraHealth(json))
            .HasColumnName("InfraHealthJson")
            .HasColumnType("TEXT");

        // Concurrency
        builder.Property(h => h.Version)
            .IsConcurrencyToken();

        // Indexes for efficient querying
        builder.HasIndex(h => h.DeploymentId);
        builder.HasIndex(h => h.EnvironmentId);
        builder.HasIndex(h => h.CapturedAtUtc);
        builder.HasIndex(h => new { h.DeploymentId, h.CapturedAtUtc });

        // Ignore domain events (not persisted)
        builder.Ignore(h => h.DomainEvents);
    }

    #region JSON Serialization Helpers

    private static string SerializeSelfHealth(SelfHealth self)
    {
        var dto = new SelfHealthDto
        {
            Status = self.Status.Value,
            Services = self.Services.Select(s => new ServiceHealthDto
            {
                Name = s.Name,
                Status = s.Status.Value,
                ContainerId = s.ContainerId,
                ContainerName = s.ContainerName,
                Reason = s.Reason,
                RestartCount = s.RestartCount
            }).ToList()
        };
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    private static SelfHealth DeserializeSelfHealth(string json)
    {
        if (string.IsNullOrEmpty(json))
            return SelfHealth.Empty();

        var dto = JsonSerializer.Deserialize<SelfHealthDto>(json, JsonOptions);
        if (dto == null)
            return SelfHealth.Empty();

        var services = dto.Services.Select(s =>
            ServiceHealth.Create(
                s.Name,
                HealthStatus.FromValue(s.Status),
                s.ContainerId,
                s.ContainerName,
                s.Reason,
                s.RestartCount));

        return SelfHealth.Create(services);
    }

    private static string? SerializeBusHealth(BusHealth? bus)
    {
        if (bus == null)
            return null;

        var dto = new BusHealthDto
        {
            Status = bus.Status.Value,
            TransportKey = bus.TransportKey,
            HasCriticalError = bus.HasCriticalError,
            CriticalErrorMessage = bus.CriticalErrorMessage,
            LastHealthPingProcessedUtc = bus.LastHealthPingProcessedUtc,
            TimeSinceLastPingMs = bus.TimeSinceLastPing?.TotalMilliseconds,
            UnhealthyAfterMs = bus.UnhealthyAfter?.TotalMilliseconds,
            Endpoints = bus.Endpoints.Select(e => new BusEndpointHealthDto
            {
                EndpointName = e.EndpointName,
                Status = e.Status.Value,
                LastPingUtc = e.LastPingUtc,
                Reason = e.Reason
            }).ToList()
        };
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    private static BusHealth? DeserializeBusHealth(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        var dto = JsonSerializer.Deserialize<BusHealthDto>(json, JsonOptions);
        if (dto == null)
            return null;

        var endpoints = dto.Endpoints.Select(e =>
            BusEndpointHealth.Create(
                e.EndpointName,
                HealthStatus.FromValue(e.Status),
                e.LastPingUtc,
                e.Reason));

        return BusHealth.Create(
            HealthStatus.FromValue(dto.Status),
            dto.TransportKey,
            dto.HasCriticalError,
            dto.CriticalErrorMessage,
            dto.LastHealthPingProcessedUtc,
            dto.TimeSinceLastPingMs.HasValue ? TimeSpan.FromMilliseconds(dto.TimeSinceLastPingMs.Value) : null,
            dto.UnhealthyAfterMs.HasValue ? TimeSpan.FromMilliseconds(dto.UnhealthyAfterMs.Value) : null,
            endpoints);
    }

    private static string? SerializeInfraHealth(InfraHealth? infra)
    {
        if (infra == null)
            return null;

        var dto = new InfraHealthDto
        {
            Status = infra.Status.Value,
            Databases = infra.Databases.Select(d => new DatabaseHealthDto
            {
                Id = d.Id,
                Status = d.Status.Value,
                LatencyMs = d.LatencyMs,
                Error = d.Error
            }).ToList(),
            Disks = infra.Disks.Select(d => new DiskHealthDto
            {
                Mount = d.Mount,
                Status = d.Status.Value,
                FreePercent = d.FreePercent,
                Error = d.Error
            }).ToList(),
            ExternalServices = infra.ExternalServices.Select(s => new ExternalServiceHealthDto
            {
                Id = s.Id,
                Status = s.Status.Value,
                Error = s.Error,
                ResponseTimeMs = s.ResponseTimeMs
            }).ToList()
        };
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    private static InfraHealth? DeserializeInfraHealth(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        var dto = JsonSerializer.Deserialize<InfraHealthDto>(json, JsonOptions);
        if (dto == null)
            return null;

        var databases = dto.Databases.Select(d =>
            DatabaseHealth.Create(d.Id, HealthStatus.FromValue(d.Status), d.LatencyMs, d.Error));

        var disks = dto.Disks.Select(d =>
            DiskHealth.Create(d.Mount, HealthStatus.FromValue(d.Status), d.FreePercent, d.Error));

        var externalServices = dto.ExternalServices.Select(s =>
            ExternalServiceHealth.Create(s.Id, HealthStatus.FromValue(s.Status), s.Error, s.ResponseTimeMs));

        return InfraHealth.Create(databases, disks, externalServices);
    }

    #endregion

    #region DTOs for JSON serialization

    private class SelfHealthDto
    {
        public int Status { get; set; }
        public List<ServiceHealthDto> Services { get; set; } = new();
    }

    private class ServiceHealthDto
    {
        public string Name { get; set; } = string.Empty;
        public int Status { get; set; }
        public string? ContainerId { get; set; }
        public string? ContainerName { get; set; }
        public string? Reason { get; set; }
        public int? RestartCount { get; set; }
    }

    private class BusHealthDto
    {
        public int Status { get; set; }
        public string? TransportKey { get; set; }
        public bool HasCriticalError { get; set; }
        public string? CriticalErrorMessage { get; set; }
        public DateTime? LastHealthPingProcessedUtc { get; set; }
        public double? TimeSinceLastPingMs { get; set; }
        public double? UnhealthyAfterMs { get; set; }
        public List<BusEndpointHealthDto> Endpoints { get; set; } = new();
    }

    private class BusEndpointHealthDto
    {
        public string EndpointName { get; set; } = string.Empty;
        public int Status { get; set; }
        public DateTime? LastPingUtc { get; set; }
        public string? Reason { get; set; }
    }

    private class InfraHealthDto
    {
        public int Status { get; set; }
        public List<DatabaseHealthDto> Databases { get; set; } = new();
        public List<DiskHealthDto> Disks { get; set; } = new();
        public List<ExternalServiceHealthDto> ExternalServices { get; set; } = new();
    }

    private class DatabaseHealthDto
    {
        public string Id { get; set; } = string.Empty;
        public int Status { get; set; }
        public int? LatencyMs { get; set; }
        public string? Error { get; set; }
    }

    private class DiskHealthDto
    {
        public string Mount { get; set; } = string.Empty;
        public int Status { get; set; }
        public double? FreePercent { get; set; }
        public string? Error { get; set; }
    }

    private class ExternalServiceHealthDto
    {
        public string Id { get; set; } = string.Empty;
        public int Status { get; set; }
        public string? Error { get; set; }
        public int? ResponseTimeMs { get; set; }
    }

    #endregion
}
