using FluentAssertions;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;

namespace ReadyStackGo.UnitTests.Domain.Health;

/// <summary>
/// Unit tests for HealthSnapshot aggregate root.
/// Tests capture behavior and overall status calculation.
/// </summary>
public class HealthSnapshotTests
{
    private readonly OrganizationId _orgId = OrganizationId.NewId();
    private readonly EnvironmentId _envId = EnvironmentId.NewId();
    private readonly DeploymentId _deploymentId = DeploymentId.NewId();

    private SelfHealth CreateSelfHealth(params HealthStatus[] serviceStatuses)
    {
        var services = serviceStatuses.Select((status, i) =>
            ServiceHealth.Create(
                $"service-{i}",
                status,
                $"container-{i}",
                $"container-name-{i}",
                status == HealthStatus.Healthy ? null : "Service issue",
                0))
            .ToList();

        return SelfHealth.Create(services);
    }

    #region Capture

    [Fact]
    public void Capture_WithValidData_CreatesSnapshot()
    {
        var selfHealth = CreateSelfHealth(HealthStatus.Healthy, HealthStatus.Healthy);

        var snapshot = HealthSnapshot.Capture(
            _orgId,
            _envId,
            _deploymentId,
            "my-stack",
            OperationMode.Normal,
            "1.0.0");

        snapshot.OrganizationId.Should().Be(_orgId);
        snapshot.EnvironmentId.Should().Be(_envId);
        snapshot.DeploymentId.Should().Be(_deploymentId);
        snapshot.StackName.Should().Be("my-stack");
        snapshot.OperationMode.Should().Be(OperationMode.Normal);
        snapshot.CurrentVersion.Should().Be("1.0.0");
        snapshot.CapturedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Capture_RaisesHealthSnapshotCapturedEvent()
    {
        var snapshot = HealthSnapshot.Capture(
            _orgId,
            _envId,
            _deploymentId,
            "my-stack",
            OperationMode.Normal);

        snapshot.DomainEvents.Should().ContainSingle(e => e is HealthSnapshotCaptured);
        var evt = (HealthSnapshotCaptured)snapshot.DomainEvents.Single();
        evt.SnapshotId.Should().Be(snapshot.Id);
        evt.DeploymentId.Should().Be(_deploymentId);
    }

    [Fact]
    public void Capture_WithEmptyStackName_ThrowsArgumentException()
    {
        var act = () => HealthSnapshot.Capture(
            _orgId,
            _envId,
            _deploymentId,
            "",
            OperationMode.Normal);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Capture_WithNullOrganizationId_ThrowsArgumentException()
    {
        var act = () => HealthSnapshot.Capture(
            null!,
            _envId,
            _deploymentId,
            "my-stack",
            OperationMode.Normal);

        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Overall Status Calculation

    [Fact]
    public void Overall_AllServicesHealthy_IsHealthy()
    {
        var selfHealth = CreateSelfHealth(HealthStatus.Healthy, HealthStatus.Healthy, HealthStatus.Healthy);

        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        snapshot.Overall.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public void Overall_WithDegradedService_IsDegraded()
    {
        var selfHealth = CreateSelfHealth(HealthStatus.Healthy, HealthStatus.Degraded, HealthStatus.Healthy);

        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        snapshot.Overall.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public void Overall_WithUnhealthyService_IsUnhealthy()
    {
        var selfHealth = CreateSelfHealth(HealthStatus.Healthy, HealthStatus.Unhealthy);

        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        snapshot.Overall.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public void Overall_MigratingMode_AtLeastDegraded()
    {
        var selfHealth = CreateSelfHealth(HealthStatus.Healthy, HealthStatus.Healthy);

        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Migrating,
            self: selfHealth);

        // Even with all healthy services, migrating mode means at least degraded
        snapshot.Overall.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public void Overall_FailedMode_AtLeastUnhealthy()
    {
        var selfHealth = CreateSelfHealth(HealthStatus.Healthy, HealthStatus.Healthy);

        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Failed,
            self: selfHealth);

        // Failed mode forces unhealthy
        snapshot.Overall.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public void Overall_MaintenanceMode_AtLeastDegraded()
    {
        var selfHealth = CreateSelfHealth(HealthStatus.Healthy);

        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Maintenance,
            self: selfHealth);

        snapshot.Overall.Should().Be(HealthStatus.Degraded);
    }

    #endregion

    #region IsHealthy

    [Fact]
    public void IsHealthy_HealthyAndNormal_ReturnsTrue()
    {
        var selfHealth = CreateSelfHealth(HealthStatus.Healthy);

        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        snapshot.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void IsHealthy_HealthyButMigrating_ReturnsFalse()
    {
        var selfHealth = CreateSelfHealth(HealthStatus.Healthy);

        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Migrating,
            self: selfHealth);

        snapshot.IsHealthy.Should().BeFalse();
    }

    [Fact]
    public void IsHealthy_UnhealthyService_ReturnsFalse()
    {
        var selfHealth = CreateSelfHealth(HealthStatus.Unhealthy);

        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        snapshot.IsHealthy.Should().BeFalse();
    }

    #endregion

    #region RequiresAttention

    [Fact]
    public void RequiresAttention_Healthy_ReturnsFalse()
    {
        var selfHealth = CreateSelfHealth(HealthStatus.Healthy);

        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        snapshot.RequiresAttention.Should().BeFalse();
    }

    [Fact]
    public void RequiresAttention_Degraded_ReturnsTrue()
    {
        var selfHealth = CreateSelfHealth(HealthStatus.Degraded);

        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        snapshot.RequiresAttention.Should().BeTrue();
    }

    [Fact]
    public void RequiresAttention_FailedMode_ReturnsTrue()
    {
        var selfHealth = CreateSelfHealth(HealthStatus.Healthy);

        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Failed,
            self: selfHealth);

        snapshot.RequiresAttention.Should().BeTrue();
    }

    #endregion

    #region GetStatusMessage

    [Fact]
    public void GetStatusMessage_Migrating_IndicatesMigration()
    {
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Migrating,
            currentVersion: "1.0.0",
            targetVersion: "2.0.0");

        snapshot.GetStatusMessage().Should().Contain("Migration");
        snapshot.GetStatusMessage().Should().Contain("1.0.0");
        snapshot.GetStatusMessage().Should().Contain("2.0.0");
    }

    [Fact]
    public void GetStatusMessage_Maintenance_IndicatesMaintenance()
    {
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Maintenance);

        snapshot.GetStatusMessage().Should().Contain("Maintenance");
    }

    [Fact]
    public void GetStatusMessage_Stopped_IndicatesStopped()
    {
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Stopped);

        snapshot.GetStatusMessage().Should().Contain("stopped");
    }

    [Fact]
    public void GetStatusMessage_Failed_IndicatesIntervention()
    {
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Failed);

        snapshot.GetStatusMessage().Should().Contain("intervention");
    }

    [Fact]
    public void GetStatusMessage_AllHealthy_IndicatesOperational()
    {
        var selfHealth = CreateSelfHealth(HealthStatus.Healthy);

        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            self: selfHealth);

        snapshot.GetStatusMessage().Should().Contain("operational");
    }

    #endregion

    #region Age and Staleness

    [Fact]
    public void Age_ReturnsCorrectDuration()
    {
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal);

        // Age should be very small since we just created it
        snapshot.Age.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void IsStale_FreshSnapshot_ReturnsFalse()
    {
        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal);

        snapshot.IsStale(TimeSpan.FromMinutes(5)).Should().BeFalse();
    }

    #endregion

    #region With Bus and Infra Health

    [Fact]
    public void Overall_WithUnhealthyBus_IsUnhealthy()
    {
        var selfHealth = CreateSelfHealth(HealthStatus.Healthy);
        var busHealth = BusHealth.Create(
            HealthStatus.Unhealthy,
            "rabbitmq",
            hasCriticalError: true,
            "Connection lost");

        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            bus: busHealth,
            self: selfHealth);

        snapshot.Overall.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public void Overall_WithDegradedInfra_IsDegraded()
    {
        var selfHealth = CreateSelfHealth(HealthStatus.Healthy);
        var infraHealth = InfraHealth.Create(
            new[] { DatabaseHealth.Create("db1", HealthStatus.Degraded, 500, null) },
            Enumerable.Empty<DiskHealth>(),
            Enumerable.Empty<ExternalServiceHealth>());

        var snapshot = HealthSnapshot.Capture(
            _orgId, _envId, _deploymentId, "my-stack",
            OperationMode.Normal,
            infra: infraHealth,
            self: selfHealth);

        snapshot.Overall.Should().Be(HealthStatus.Degraded);
    }

    #endregion
}
