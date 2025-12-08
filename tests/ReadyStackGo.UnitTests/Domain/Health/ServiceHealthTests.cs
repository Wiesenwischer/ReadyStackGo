using FluentAssertions;
using ReadyStackGo.Domain.Deployment.Health;

namespace ReadyStackGo.UnitTests.Domain.Health;

/// <summary>
/// Unit tests for ServiceHealth value object.
/// </summary>
public class ServiceHealthTests
{
    #region Create

    [Fact]
    public void Create_WithValidData_CreatesServiceHealth()
    {
        var service = ServiceHealth.Create(
            "web-server",
            HealthStatus.Healthy,
            "abc123",
            "myapp-web-1",
            null,
            0);

        service.Name.Should().Be("web-server");
        service.Status.Should().Be(HealthStatus.Healthy);
        service.ContainerId.Should().Be("abc123");
        service.ContainerName.Should().Be("myapp-web-1");
        service.Reason.Should().BeNull();
        service.RestartCount.Should().Be(0);
    }

    [Fact]
    public void Create_WithReason_StoresReason()
    {
        var service = ServiceHealth.Create(
            "database",
            HealthStatus.Unhealthy,
            "xyz789",
            "myapp-db-1",
            "Connection refused",
            5);

        service.Status.Should().Be(HealthStatus.Unhealthy);
        service.Reason.Should().Be("Connection refused");
        service.RestartCount.Should().Be(5);
    }

    [Fact]
    public void Create_WithEmptyName_ThrowsArgumentException()
    {
        var act = () => ServiceHealth.Create(
            "",
            HealthStatus.Healthy,
            "abc123",
            "container",
            null,
            0);

        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Equality

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var service1 = ServiceHealth.Create("web", HealthStatus.Healthy, "abc", "web-1", null, 0);
        var service2 = ServiceHealth.Create("web", HealthStatus.Healthy, "abc", "web-1", null, 0);

        service1.Should().Be(service2);
        (service1 == service2).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentStatus_AreNotEqual()
    {
        var service1 = ServiceHealth.Create("web", HealthStatus.Healthy, "abc", "web-1", null, 0);
        var service2 = ServiceHealth.Create("web", HealthStatus.Unhealthy, "abc", "web-1", null, 0);

        service1.Should().NotBe(service2);
        (service1 != service2).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentRestartCount_AreNotEqual()
    {
        var service1 = ServiceHealth.Create("web", HealthStatus.Healthy, "abc", "web-1", null, 0);
        var service2 = ServiceHealth.Create("web", HealthStatus.Healthy, "abc", "web-1", null, 5);

        service1.Should().NotBe(service2);
    }

    #endregion
}

/// <summary>
/// Unit tests for SelfHealth value object.
/// </summary>
public class SelfHealthTests
{
    #region Create

    [Fact]
    public void Create_WithHealthyServices_CalculatesCorrectCounts()
    {
        var services = new[]
        {
            ServiceHealth.Create("web", HealthStatus.Healthy, "1", "web-1", null, 0),
            ServiceHealth.Create("api", HealthStatus.Healthy, "2", "api-1", null, 0),
            ServiceHealth.Create("worker", HealthStatus.Healthy, "3", "worker-1", null, 0)
        };

        var selfHealth = SelfHealth.Create(services);

        selfHealth.TotalCount.Should().Be(3);
        selfHealth.HealthyCount.Should().Be(3);
        selfHealth.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public void Create_WithMixedServices_CalculatesCorrectCounts()
    {
        var services = new[]
        {
            ServiceHealth.Create("web", HealthStatus.Healthy, "1", "web-1", null, 0),
            ServiceHealth.Create("api", HealthStatus.Degraded, "2", "api-1", "High latency", 0),
            ServiceHealth.Create("worker", HealthStatus.Unhealthy, "3", "worker-1", "Crashed", 3)
        };

        var selfHealth = SelfHealth.Create(services);

        selfHealth.TotalCount.Should().Be(3);
        selfHealth.HealthyCount.Should().Be(1);
        selfHealth.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public void Create_WithDegradedOnly_StatusIsDegraded()
    {
        var services = new[]
        {
            ServiceHealth.Create("web", HealthStatus.Healthy, "1", "web-1", null, 0),
            ServiceHealth.Create("api", HealthStatus.Degraded, "2", "api-1", "Slow", 0)
        };

        var selfHealth = SelfHealth.Create(services);

        selfHealth.Status.Should().Be(HealthStatus.Degraded);
        selfHealth.HealthyCount.Should().Be(1);
    }

    #endregion

    #region Empty

    [Fact]
    public void Empty_ReturnsEmptySelfHealth()
    {
        var selfHealth = SelfHealth.Empty();

        selfHealth.TotalCount.Should().Be(0);
        selfHealth.HealthyCount.Should().Be(0);
        selfHealth.Services.Should().BeEmpty();
        selfHealth.Status.Should().Be(HealthStatus.Unknown);
    }

    #endregion

    #region Services Collection

    [Fact]
    public void Services_IsReadOnly()
    {
        var services = new[]
        {
            ServiceHealth.Create("web", HealthStatus.Healthy, "1", "web-1", null, 0)
        };

        var selfHealth = SelfHealth.Create(services);

        // Services should be read-only collection
        selfHealth.Services.Should().HaveCount(1);
        selfHealth.Services.Should().BeAssignableTo<IReadOnlyList<ServiceHealth>>();
    }

    #endregion

    #region Equality

    [Fact]
    public void Equality_SameServices_AreEqual()
    {
        var services1 = new[]
        {
            ServiceHealth.Create("web", HealthStatus.Healthy, "1", "web-1", null, 0)
        };
        var services2 = new[]
        {
            ServiceHealth.Create("web", HealthStatus.Healthy, "1", "web-1", null, 0)
        };

        var selfHealth1 = SelfHealth.Create(services1);
        var selfHealth2 = SelfHealth.Create(services2);

        selfHealth1.Status.Should().Be(selfHealth2.Status);
        selfHealth1.TotalCount.Should().Be(selfHealth2.TotalCount);
        selfHealth1.HealthyCount.Should().Be(selfHealth2.HealthyCount);
    }

    #endregion
}
