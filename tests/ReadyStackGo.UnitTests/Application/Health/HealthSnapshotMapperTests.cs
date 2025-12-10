using FluentAssertions;
using ReadyStackGo.Application.UseCases.Health;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.IdentityAccess.Organizations;

namespace ReadyStackGo.UnitTests.Application.Health;

/// <summary>
/// Unit tests for HealthSnapshotMapper.
/// Ensures proper mapping from domain HealthSnapshot to DTOs.
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

        dto.StatusMessage.Should().Contain("maintenance");
    }

    [Fact]
    public void MapToStackHealthDto_GeneratesStatusMessage_ForMigratingMode()
    {
        var selfHealth = CreateSelfHealth(("api", HealthStatus.Healthy));
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Migrating,
            currentVersion: "1.0.0",
            targetVersion: "2.0.0",
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, _envId);

        dto.StatusMessage.Should().Contain("1.0.0");
        dto.StatusMessage.Should().Contain("2.0.0");
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

    #endregion

    #region MapToStackHealthSummary

    [Fact]
    public void MapToStackHealthSummary_MapsBasicProperties()
    {
        var selfHealth = CreateSelfHealth(("api", HealthStatus.Healthy), ("db", HealthStatus.Healthy));
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            currentVersion: "1.0.0",
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthSummary(snapshot);

        dto.DeploymentId.Should().Be(_deploymentId.Value.ToString());
        dto.StackName.Should().Be("my-stack");
        dto.CurrentVersion.Should().Be("1.0.0");
        dto.OperationMode.Should().Be("Normal");
        dto.OverallStatus.Should().Be("Healthy");
    }

    [Fact]
    public void MapToStackHealthSummary_MapsServiceCounts()
    {
        var selfHealth = CreateSelfHealth(
            ("api", HealthStatus.Healthy),
            ("db", HealthStatus.Healthy),
            ("cache", HealthStatus.Unhealthy));
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthSummary(snapshot);

        dto.HealthyServices.Should().Be(2);
        dto.TotalServices.Should().Be(3);
    }

    [Fact]
    public void MapToStackHealthSummary_DoesNotContainServiceDetails()
    {
        // Summary DTO intentionally does not include service list
        var selfHealth = CreateSelfHealth(
            ("api", HealthStatus.Healthy),
            ("db", HealthStatus.Degraded));
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthSummary(snapshot);

        // StackHealthSummaryDto does not have Services property - verify counts only
        dto.HealthyServices.Should().Be(1);
        dto.TotalServices.Should().Be(2);
    }

    [Fact]
    public void MapToStackHealthSummary_MapsRequiresAttention()
    {
        var selfHealth = CreateSelfHealth(("api", HealthStatus.Degraded));
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthSummary(snapshot);

        dto.RequiresAttention.Should().BeTrue();
    }

    [Fact]
    public void MapToStackHealthSummary_MapsStatusMessage()
    {
        var selfHealth = CreateSelfHealth(("api", HealthStatus.Healthy));
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Maintenance,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthSummary(snapshot);

        dto.StatusMessage.Should().Contain("Maintenance");
    }

    [Fact]
    public void MapToStackHealthSummary_MapsCapturedAtUtc()
    {
        var selfHealth = CreateSelfHealth(("api", HealthStatus.Healthy));
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        var dto = HealthSnapshotMapper.MapToStackHealthSummary(snapshot);

        dto.CapturedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region Consistency between StackHealthDto and StackHealthSummaryDto

    [Fact]
    public void BothMappings_ShouldHaveConsistentOverallStatus()
    {
        var selfHealth = CreateSelfHealth(
            ("api", HealthStatus.Healthy),
            ("db", HealthStatus.Degraded));
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        var detailedDto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, _envId);
        var summaryDto = HealthSnapshotMapper.MapToStackHealthSummary(snapshot);

        detailedDto.OverallStatus.Should().Be(summaryDto.OverallStatus);
        detailedDto.OperationMode.Should().Be(summaryDto.OperationMode);
        detailedDto.StackName.Should().Be(summaryDto.StackName);
        detailedDto.CurrentVersion.Should().Be(summaryDto.CurrentVersion);
    }

    [Fact]
    public void BothMappings_ShouldHaveConsistentServiceCounts()
    {
        var selfHealth = CreateSelfHealth(
            ("api", HealthStatus.Healthy),
            ("db", HealthStatus.Healthy),
            ("cache", HealthStatus.Unhealthy));
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        var detailedDto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, _envId);
        var summaryDto = HealthSnapshotMapper.MapToStackHealthSummary(snapshot);

        detailedDto.Self.HealthyCount.Should().Be(summaryDto.HealthyServices);
        detailedDto.Self.TotalCount.Should().Be(summaryDto.TotalServices);
    }

    #endregion
}
