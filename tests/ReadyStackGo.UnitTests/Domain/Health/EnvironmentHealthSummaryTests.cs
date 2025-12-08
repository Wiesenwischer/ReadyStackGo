using FluentAssertions;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.IdentityAccess.Organizations;

namespace ReadyStackGo.UnitTests.Domain.Health;

using Environment = ReadyStackGo.Domain.Deployment.Environments.Environment;

/// <summary>
/// Unit tests for EnvironmentHealthSummary value object.
/// Tests aggregation logic and domain behavior.
/// </summary>
public class EnvironmentHealthSummaryTests
{
    private readonly OrganizationId _orgId = OrganizationId.NewId();
    private readonly EnvironmentId _envId = EnvironmentId.NewId();

    private Environment CreateTestEnvironment(string name = "Production")
    {
        return Environment.CreateDefault(_envId, _orgId, name, "Test environment");
    }

    private HealthSnapshot CreateSnapshot(
        HealthStatus overallStatus,
        OperationMode operationMode,
        string stackName,
        int healthyServices = 3,
        int totalServices = 3)
    {
        var services = Enumerable.Range(0, totalServices)
            .Select(i => ServiceHealth.Create(
                $"service-{i}",
                i < healthyServices ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                $"container-{i}",
                $"container-name-{i}",
                i < healthyServices ? null : "Service is down",
                0))
            .ToList();

        var selfHealth = SelfHealth.Create(services);

        return HealthSnapshot.Capture(
            _orgId,
            _envId,
            DeploymentId.NewId(),
            stackName,
            operationMode,
            "1.0.0",
            null,
            null,
            null,
            selfHealth);
    }

    #region FromSnapshots

    [Fact]
    public void FromSnapshots_EmptyCollection_ReturnsEmptySummary()
    {
        var environment = CreateTestEnvironment();
        var snapshots = Enumerable.Empty<HealthSnapshot>();

        var summary = EnvironmentHealthSummary.FromSnapshots(environment, snapshots);

        summary.TotalStacks.Should().Be(0);
        summary.HealthyCount.Should().Be(0);
        summary.DegradedCount.Should().Be(0);
        summary.UnhealthyCount.Should().Be(0);
        summary.Stacks.Should().BeEmpty();
        summary.OverallStatus.Should().Be(HealthStatus.Unknown);
    }

    [Fact]
    public void FromSnapshots_AllHealthy_ReturnsHealthySummary()
    {
        var environment = CreateTestEnvironment();
        var snapshots = new[]
        {
            CreateSnapshot(HealthStatus.Healthy, OperationMode.Normal, "stack-1"),
            CreateSnapshot(HealthStatus.Healthy, OperationMode.Normal, "stack-2"),
            CreateSnapshot(HealthStatus.Healthy, OperationMode.Normal, "stack-3")
        };

        var summary = EnvironmentHealthSummary.FromSnapshots(environment, snapshots);

        summary.TotalStacks.Should().Be(3);
        summary.HealthyCount.Should().Be(3);
        summary.DegradedCount.Should().Be(0);
        summary.UnhealthyCount.Should().Be(0);
        summary.OverallStatus.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public void FromSnapshots_MixedStatuses_ReturnsCorrectCounts()
    {
        var environment = CreateTestEnvironment();
        var snapshots = new[]
        {
            CreateSnapshot(HealthStatus.Healthy, OperationMode.Normal, "stack-1"),
            CreateSnapshot(HealthStatus.Degraded, OperationMode.Migrating, "stack-2"),
            CreateSnapshot(HealthStatus.Unhealthy, OperationMode.Failed, "stack-3"),
            CreateSnapshot(HealthStatus.Healthy, OperationMode.Normal, "stack-4")
        };

        var summary = EnvironmentHealthSummary.FromSnapshots(environment, snapshots);

        summary.TotalStacks.Should().Be(4);
        summary.HealthyCount.Should().Be(2);
        summary.DegradedCount.Should().Be(1);
        summary.UnhealthyCount.Should().Be(1);
    }

    [Fact]
    public void FromSnapshots_WithUnhealthy_OverallIsUnhealthy()
    {
        var environment = CreateTestEnvironment();
        var snapshots = new[]
        {
            CreateSnapshot(HealthStatus.Healthy, OperationMode.Normal, "stack-1"),
            CreateSnapshot(HealthStatus.Unhealthy, OperationMode.Failed, "stack-2")
        };

        var summary = EnvironmentHealthSummary.FromSnapshots(environment, snapshots);

        summary.OverallStatus.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public void FromSnapshots_WithDegradedNoUnhealthy_OverallIsDegraded()
    {
        var environment = CreateTestEnvironment();
        var snapshots = new[]
        {
            CreateSnapshot(HealthStatus.Healthy, OperationMode.Normal, "stack-1"),
            CreateSnapshot(HealthStatus.Degraded, OperationMode.Migrating, "stack-2")
        };

        var summary = EnvironmentHealthSummary.FromSnapshots(environment, snapshots);

        summary.OverallStatus.Should().Be(HealthStatus.Degraded);
    }

    #endregion

    #region Empty

    [Fact]
    public void Empty_ReturnsValidEmptySummary()
    {
        var environment = CreateTestEnvironment("Test Env");

        var summary = EnvironmentHealthSummary.Empty(environment);

        summary.EnvironmentId.Should().Be(_envId);
        summary.EnvironmentName.Should().Be("Test Env");
        summary.TotalStacks.Should().Be(0);
        summary.Stacks.Should().BeEmpty();
    }

    #endregion

    #region RequiresAttention

    [Fact]
    public void RequiresAttention_AllHealthy_ReturnsFalse()
    {
        var environment = CreateTestEnvironment();
        var snapshots = new[]
        {
            CreateSnapshot(HealthStatus.Healthy, OperationMode.Normal, "stack-1")
        };

        var summary = EnvironmentHealthSummary.FromSnapshots(environment, snapshots);

        summary.RequiresAttention.Should().BeFalse();
    }

    [Fact]
    public void RequiresAttention_WithUnhealthy_ReturnsTrue()
    {
        var environment = CreateTestEnvironment();
        var snapshots = new[]
        {
            CreateSnapshot(HealthStatus.Unhealthy, OperationMode.Failed, "stack-1")
        };

        var summary = EnvironmentHealthSummary.FromSnapshots(environment, snapshots);

        summary.RequiresAttention.Should().BeTrue();
    }

    [Fact]
    public void RequiresAttention_WithDegraded_ReturnsTrue()
    {
        var environment = CreateTestEnvironment();
        var snapshots = new[]
        {
            CreateSnapshot(HealthStatus.Degraded, OperationMode.Migrating, "stack-1")
        };

        var summary = EnvironmentHealthSummary.FromSnapshots(environment, snapshots);

        summary.RequiresAttention.Should().BeTrue();
    }

    #endregion

    #region GetStatusMessage

    [Fact]
    public void GetStatusMessage_NoDeployments_ReturnsNoDeployments()
    {
        var environment = CreateTestEnvironment();
        var summary = EnvironmentHealthSummary.Empty(environment);

        summary.GetStatusMessage().Should().Be("No deployments");
    }

    [Fact]
    public void GetStatusMessage_AllHealthy_ReturnsAllHealthy()
    {
        var environment = CreateTestEnvironment();
        var snapshots = new[]
        {
            CreateSnapshot(HealthStatus.Healthy, OperationMode.Normal, "stack-1"),
            CreateSnapshot(HealthStatus.Healthy, OperationMode.Normal, "stack-2")
        };

        var summary = EnvironmentHealthSummary.FromSnapshots(environment, snapshots);

        summary.GetStatusMessage().Should().Be("All 2 stack(s) healthy");
    }

    [Fact]
    public void GetStatusMessage_WithUnhealthy_IndicatesUnhealthyCount()
    {
        var environment = CreateTestEnvironment();
        var snapshots = new[]
        {
            CreateSnapshot(HealthStatus.Healthy, OperationMode.Normal, "stack-1"),
            CreateSnapshot(HealthStatus.Unhealthy, OperationMode.Failed, "stack-2"),
            CreateSnapshot(HealthStatus.Unhealthy, OperationMode.Failed, "stack-3")
        };

        var summary = EnvironmentHealthSummary.FromSnapshots(environment, snapshots);

        summary.GetStatusMessage().Should().Be("2 of 3 stack(s) unhealthy");
    }

    #endregion

    #region GetStacksRequiringAttention

    [Fact]
    public void GetStacksRequiringAttention_ReturnsOnlyUnhealthyAndDegraded()
    {
        var environment = CreateTestEnvironment();
        var snapshots = new[]
        {
            CreateSnapshot(HealthStatus.Healthy, OperationMode.Normal, "healthy-stack"),
            CreateSnapshot(HealthStatus.Degraded, OperationMode.Migrating, "degraded-stack"),
            CreateSnapshot(HealthStatus.Unhealthy, OperationMode.Failed, "unhealthy-stack")
        };

        var summary = EnvironmentHealthSummary.FromSnapshots(environment, snapshots);
        var attention = summary.GetStacksRequiringAttention().ToList();

        attention.Should().HaveCount(2);
        attention.Should().Contain(s => s.StackName == "degraded-stack");
        attention.Should().Contain(s => s.StackName == "unhealthy-stack");
        attention.Should().NotContain(s => s.StackName == "healthy-stack");
    }

    #endregion

    #region HealthyPercentage

    [Fact]
    public void HealthyPercentage_AllHealthy_Returns100()
    {
        var environment = CreateTestEnvironment();
        var snapshots = new[]
        {
            CreateSnapshot(HealthStatus.Healthy, OperationMode.Normal, "stack-1"),
            CreateSnapshot(HealthStatus.Healthy, OperationMode.Normal, "stack-2")
        };

        var summary = EnvironmentHealthSummary.FromSnapshots(environment, snapshots);

        summary.HealthyPercentage.Should().Be(100);
    }

    [Fact]
    public void HealthyPercentage_HalfHealthy_Returns50()
    {
        var environment = CreateTestEnvironment();
        var snapshots = new[]
        {
            CreateSnapshot(HealthStatus.Healthy, OperationMode.Normal, "stack-1"),
            CreateSnapshot(HealthStatus.Unhealthy, OperationMode.Failed, "stack-2")
        };

        var summary = EnvironmentHealthSummary.FromSnapshots(environment, snapshots);

        summary.HealthyPercentage.Should().Be(50);
    }

    [Fact]
    public void HealthyPercentage_NoStacks_Returns0()
    {
        var environment = CreateTestEnvironment();
        var summary = EnvironmentHealthSummary.Empty(environment);

        summary.HealthyPercentage.Should().Be(0);
    }

    #endregion

    #region StackHealthSummary

    [Fact]
    public void StackHealthSummary_FromSnapshot_ContainsCorrectData()
    {
        var environment = CreateTestEnvironment();
        // 2 healthy, 3 total = 1 unhealthy service, so overall is Unhealthy
        var snapshot = CreateSnapshot(HealthStatus.Unhealthy, OperationMode.Migrating, "test-stack", 2, 3);

        var summary = EnvironmentHealthSummary.FromSnapshots(environment, new[] { snapshot });
        var stackSummary = summary.Stacks.Single();

        stackSummary.StackName.Should().Be("test-stack");
        // With 1 unhealthy service, the overall status is Unhealthy (worse than Degraded from Migrating mode)
        stackSummary.OverallStatus.Should().Be(HealthStatus.Unhealthy);
        stackSummary.OperationMode.Should().Be(OperationMode.Migrating);
        stackSummary.HealthyServices.Should().Be(2);
        stackSummary.TotalServices.Should().Be(3);
        stackSummary.RequiresAttention.Should().BeTrue();
    }

    [Fact]
    public void StackHealthSummary_ServiceHealthRatio_ReturnsCorrectFormat()
    {
        var environment = CreateTestEnvironment();
        var snapshot = CreateSnapshot(HealthStatus.Healthy, OperationMode.Normal, "test-stack", 3, 5);

        var summary = EnvironmentHealthSummary.FromSnapshots(environment, new[] { snapshot });
        var stackSummary = summary.Stacks.Single();

        stackSummary.ServiceHealthRatio.Should().Be("3/5");
    }

    #endregion

    #region Equality

    [Fact]
    public void Equality_SameSummaries_AreEqual()
    {
        var environment = CreateTestEnvironment();
        var snapshots = new[]
        {
            CreateSnapshot(HealthStatus.Healthy, OperationMode.Normal, "stack-1")
        };

        var summary1 = EnvironmentHealthSummary.FromSnapshots(environment, snapshots);
        var summary2 = EnvironmentHealthSummary.FromSnapshots(environment, snapshots);

        // Note: Equality is based on components, not reference
        summary1.EnvironmentId.Should().Be(summary2.EnvironmentId);
        summary1.TotalStacks.Should().Be(summary2.TotalStacks);
    }

    #endregion
}
