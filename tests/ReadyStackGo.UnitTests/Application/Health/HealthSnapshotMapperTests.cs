using FluentAssertions;
using ReadyStackGo.Application.UseCases.Health;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;

namespace ReadyStackGo.UnitTests.Application.Health;

/// <summary>
/// Unit tests for HealthSnapshotMapper.
/// Ensures proper mapping from domain HealthSnapshot to DTOs.
/// Note: OperationMode is now simplified to only Normal and Maintenance.
/// </summary>
public class HealthSnapshotMapperTests
{
    private readonly OrganizationId _orgId = OrganizationId.NewId();
    private readonly EnvironmentId _envId = EnvironmentId.NewId();
    private readonly DeploymentId _deploymentId = DeploymentId.NewId();

    private SelfHealth CreateSelfHealth(params (string name, HealthStatus status)[] services)
    {
        var serviceList = services.Select((s, i) =>
            ServiceHealth.Create(
                s.name,
                s.status,
                $"container-{i}",
                $"{s.name}-container",
                s.status == HealthStatus.Healthy ? null : "Service issue",
                s.status == HealthStatus.Healthy ? 0 : 1))
            .ToList();

        return SelfHealth.Create(serviceList);
    }

    #region MapToStackHealthDto

    [Fact]
    public void MapToStackHealthDto_MapsBasicProperties()
    {
        var selfHealth = CreateSelfHealth(("api", HealthStatus.Healthy), ("db", HealthStatus.Healthy));
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            currentVersion: "1.0.0",
            targetVersion: "2.0.0",
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, _envId);

        dto.DeploymentId.Should().Be(_deploymentId.Value.ToString());
        dto.EnvironmentId.Should().Be(_envId.Value.ToString());
        dto.StackName.Should().Be("my-stack");
        dto.CurrentVersion.Should().Be("1.0.0");
        dto.TargetVersion.Should().Be("2.0.0");
        dto.OperationMode.Should().Be("Normal");
        dto.OverallStatus.Should().Be("Healthy");
    }

    [Fact]
    public void MapToStackHealthDto_MapsServicesCorrectly()
    {
        var selfHealth = CreateSelfHealth(
            ("api", HealthStatus.Healthy),
            ("db", HealthStatus.Degraded),
            ("cache", HealthStatus.Unhealthy));
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, _envId);

        dto.Self.Services.Should().HaveCount(3);
        dto.Self.Services.Should().Contain(s => s.Name == "api" && s.Status == "Healthy");
        dto.Self.Services.Should().Contain(s => s.Name == "db" && s.Status == "Degraded");
        dto.Self.Services.Should().Contain(s => s.Name == "cache" && s.Status == "Unhealthy");
    }

    [Fact]
    public void MapToStackHealthDto_MapsSelfHealthCounts()
    {
        var selfHealth = CreateSelfHealth(
            ("api", HealthStatus.Healthy),
            ("db", HealthStatus.Healthy),
            ("cache", HealthStatus.Degraded));
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, _envId);

        dto.Self.HealthyCount.Should().Be(2);
        dto.Self.TotalCount.Should().Be(3);
        dto.Self.Status.Should().Be("Degraded");
    }

    [Fact]
    public void MapToStackHealthDto_MapsServiceDetails()
    {
        var service = ServiceHealth.Create(
            "my-service",
            HealthStatus.Unhealthy,
            "abc123def456",
            "my-stack_my-service_1",
            "Container health check failed",
            5);
        var selfHealth = SelfHealth.Create(new[] { service });
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, _envId);

        var serviceDto = dto.Self.Services.Single();
        serviceDto.Name.Should().Be("my-service");
        serviceDto.Status.Should().Be("Unhealthy");
        serviceDto.ContainerId.Should().Be("abc123def456");
        serviceDto.ContainerName.Should().Be("my-stack_my-service_1");
        serviceDto.Reason.Should().Be("Container health check failed");
        serviceDto.RestartCount.Should().Be(5);
    }

    [Fact]
    public void MapToStackHealthDto_MapsBusHealthWhenPresent()
    {
        var selfHealth = CreateSelfHealth(("api", HealthStatus.Healthy));
        var busHealth = BusHealth.Create(
            HealthStatus.Healthy,
            "rabbitmq",
            hasCriticalError: false,
            criticalErrorMessage: null);
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            bus: busHealth,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, _envId);

        dto.Bus.Should().NotBeNull();
        dto.Bus!.Status.Should().Be("Healthy");
        dto.Bus.TransportKey.Should().Be("rabbitmq");
        dto.Bus.HasCriticalError.Should().BeFalse();
    }

    [Fact]
    public void MapToStackHealthDto_BusIsNullWhenNotPresent()
    {
        var selfHealth = CreateSelfHealth(("api", HealthStatus.Healthy));
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, _envId);

        dto.Bus.Should().BeNull();
    }

    [Fact]
    public void MapToStackHealthDto_MapsInfraHealthWhenPresent()
    {
        var selfHealth = CreateSelfHealth(("api", HealthStatus.Healthy));
        var infraHealth = InfraHealth.Create(
            new[] { DatabaseHealth.Create("db1", HealthStatus.Healthy, 50, null) },
            new[] { DiskHealth.Create("/data", HealthStatus.Healthy, 75.5, null) },
            new[] { ExternalServiceHealth.Create("redis", HealthStatus.Healthy, null, 10) });
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            infra: infraHealth,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, _envId);

        dto.Infra.Should().NotBeNull();
        dto.Infra!.Status.Should().Be("Healthy");
        dto.Infra.Databases.Should().ContainSingle(d => d.Id == "db1" && d.LatencyMs == 50);
        dto.Infra.Disks.Should().ContainSingle(d => d.Mount == "/data" && d.FreePercent == 75.5);
        dto.Infra.ExternalServices.Should().ContainSingle(s => s.Id == "redis" && s.ResponseTimeMs == 10);
    }

    [Fact]
    public void MapToStackHealthDto_GeneratesStatusMessage_ForMaintenanceMode()
    {
        var selfHealth = CreateSelfHealth(("api", HealthStatus.Healthy));
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Maintenance,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, _envId);

        dto.StatusMessage.Should().Contain("Maintenance");
    }

    [Fact]
    public void MapToStackHealthDto_GeneratesStatusMessage_ForNormalMode()
    {
        var selfHealth = CreateSelfHealth(("api", HealthStatus.Healthy));
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, _envId);

        dto.StatusMessage.Should().Contain("operational");
    }

    [Fact]
    public void MapToStackHealthDto_SetsRequiresAttention_WhenUnhealthy()
    {
        var selfHealth = CreateSelfHealth(("api", HealthStatus.Unhealthy));
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, _envId);

        dto.RequiresAttention.Should().BeTrue();
    }

    [Fact]
    public void MapToStackHealthDto_RequiresAttention_FalseForHealthyNormal()
    {
        var selfHealth = CreateSelfHealth(("api", HealthStatus.Healthy));
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, _envId);

        dto.RequiresAttention.Should().BeFalse();
    }

    [Fact]
    public void MapToStackHealthDto_RequiresAttention_FalseForMaintenance()
    {
        // Maintenance mode is a planned state, does not require attention
        var selfHealth = CreateSelfHealth(("api", HealthStatus.Healthy));
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Maintenance,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, _envId);

        dto.RequiresAttention.Should().BeFalse();
    }

    #endregion

    #region Service Count Fields

    [Fact]
    public void MapToStackHealthDto_MapsTopLevelServiceCounts()
    {
        var selfHealth = CreateSelfHealth(
            ("api", HealthStatus.Healthy),
            ("db", HealthStatus.Healthy),
            ("cache", HealthStatus.Unhealthy));
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, _envId);

        dto.HealthyServices.Should().Be(2);
        dto.TotalServices.Should().Be(3);
        // Top-level counts should match Self counts
        dto.HealthyServices.Should().Be(dto.Self.HealthyCount);
        dto.TotalServices.Should().Be(dto.Self.TotalCount);
    }

    [Fact]
    public void MapToStackHealthDto_ProductFieldsDefaultToNull()
    {
        var selfHealth = CreateSelfHealth(("api", HealthStatus.Healthy));
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, _envId);

        dto.ProductDeploymentId.Should().BeNull();
        dto.ProductDisplayName.Should().BeNull();
    }

    [Fact]
    public void MapToStackHealthDto_ProductFieldsCanBeSet()
    {
        var selfHealth = CreateSelfHealth(("api", HealthStatus.Healthy));
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, _envId);
        dto.ProductDeploymentId = "pd-123";
        dto.ProductDisplayName = "My Product";

        dto.ProductDeploymentId.Should().Be("pd-123");
        dto.ProductDisplayName.Should().Be("My Product");
    }

    #endregion

    #region OperationMode Mapping

    [Fact]
    public void MapToStackHealthDto_MapsNormalOperationMode()
    {
        var selfHealth = CreateSelfHealth(("api", HealthStatus.Healthy));
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, _envId);

        dto.OperationMode.Should().Be("Normal");
    }

    [Fact]
    public void MapToStackHealthDto_MapsMaintenanceOperationMode()
    {
        var selfHealth = CreateSelfHealth(("api", HealthStatus.Healthy));
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Maintenance,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, _envId);

        dto.OperationMode.Should().Be("Maintenance");
    }

    #endregion

    #region Health Check Entries Mapping

    [Fact]
    public void MapToStackHealthDto_MapsHealthCheckEntries()
    {
        var entries = new List<HealthCheckEntry>
        {
            HealthCheckEntry.Create("database", HealthStatus.Healthy, "SQL OK", 12.3,
                new Dictionary<string, string> { { "server", "sql01" } },
                new List<string> { "db" }),
            HealthCheckEntry.Create("redis", HealthStatus.Unhealthy, "Connection refused", 5001.2,
                exception: "SocketException")
        };

        var service = ServiceHealth.Create("api", HealthStatus.Degraded,
            "c123", "api-container", "Some checks failing", null, entries, 150);
        var selfHealth = SelfHealth.Create(new[] { service });
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, _envId);
        var serviceDto = dto.Self.Services.Single();

        serviceDto.HealthCheckEntries.Should().NotBeNull();
        serviceDto.HealthCheckEntries.Should().HaveCount(2);
        serviceDto.ResponseTimeMs.Should().Be(150);

        var dbEntry = serviceDto.HealthCheckEntries!.Single(e => e.Name == "database");
        dbEntry.Status.Should().Be("Healthy");
        dbEntry.Description.Should().Be("SQL OK");
        dbEntry.DurationMs.Should().Be(12.3);
        dbEntry.Data.Should().ContainKey("server");
        dbEntry.Tags.Should().ContainSingle("db");
        dbEntry.Exception.Should().BeNull();

        var redisEntry = serviceDto.HealthCheckEntries!.Single(e => e.Name == "redis");
        redisEntry.Status.Should().Be("Unhealthy");
        redisEntry.Exception.Should().Be("SocketException");
    }

    [Fact]
    public void MapToStackHealthDto_NullEntries_MapsToNull()
    {
        var service = ServiceHealth.Create("api", HealthStatus.Healthy, "c123", "api-container");
        var selfHealth = SelfHealth.Create(new[] { service });
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, _envId);
        var serviceDto = dto.Self.Services.Single();

        serviceDto.HealthCheckEntries.Should().BeNull();
        serviceDto.ResponseTimeMs.Should().BeNull();
    }

    [Fact]
    public void MapToStackHealthDto_EmptyEntries_MapsToEmptyList()
    {
        var entries = new List<HealthCheckEntry>();
        var service = ServiceHealth.Create("api", HealthStatus.Healthy,
            "c123", "api-container", null, null, entries, 50);
        var selfHealth = SelfHealth.Create(new[] { service });
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, _envId);
        var serviceDto = dto.Self.Services.Single();

        serviceDto.HealthCheckEntries.Should().NotBeNull();
        serviceDto.HealthCheckEntries.Should().BeEmpty();
        serviceDto.ResponseTimeMs.Should().Be(50);
    }

    [Fact]
    public void MapServiceToDto_MapsEntriesCorrectly()
    {
        var entries = new List<HealthCheckEntry>
        {
            HealthCheckEntry.Create("check1", HealthStatus.Healthy, "OK"),
        };
        var service = ServiceHealth.Create("api", HealthStatus.Healthy,
            "c123", "api-container", null, null, entries, 25);

        var dto = HealthSnapshotMapper.MapServiceToDto(service);

        dto.Name.Should().Be("api");
        dto.HealthCheckEntries.Should().HaveCount(1);
        dto.HealthCheckEntries![0].Name.Should().Be("check1");
        dto.ResponseTimeMs.Should().Be(25);
    }

    [Fact]
    public void MapToStackHealthDto_EntryDataDictionary_MappedCorrectly()
    {
        var data = new Dictionary<string, string>
        {
            { "key1", "value1" },
            { "key2", "value2" }
        };
        var entries = new List<HealthCheckEntry>
        {
            HealthCheckEntry.Create("check", HealthStatus.Healthy, data: data)
        };
        var service = ServiceHealth.Create("api", HealthStatus.Healthy,
            healthCheckEntries: entries);
        var selfHealth = SelfHealth.Create(new[] { service });
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, _envId);
        var entryDto = dto.Self.Services.Single().HealthCheckEntries!.Single();

        entryDto.Data.Should().HaveCount(2);
        entryDto.Data!["key1"].Should().Be("value1");
        entryDto.Data!["key2"].Should().Be("value2");
    }

    [Fact]
    public void MapToStackHealthDto_EntryTags_MappedCorrectly()
    {
        var tags = new List<string> { "critical", "db", "infrastructure" };
        var entries = new List<HealthCheckEntry>
        {
            HealthCheckEntry.Create("check", HealthStatus.Healthy, tags: tags)
        };
        var service = ServiceHealth.Create("api", HealthStatus.Healthy,
            healthCheckEntries: entries);
        var selfHealth = SelfHealth.Create(new[] { service });
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, _envId);
        var entryDto = dto.Self.Services.Single().HealthCheckEntries!.Single();

        entryDto.Tags.Should().HaveCount(3);
        entryDto.Tags.Should().Contain("critical");
        entryDto.Tags.Should().Contain("db");
        entryDto.Tags.Should().Contain("infrastructure");
    }

    #endregion
}
