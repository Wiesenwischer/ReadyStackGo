using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Infrastructure.Services;

namespace ReadyStackGo.UnitTests.Services;

/// <summary>
/// Unit tests for HealthMonitoringService.
/// Verifies RestartCount is only fetched for unhealthy containers.
/// </summary>
public class HealthMonitoringServiceTests
{
    private readonly Mock<IDockerService> _dockerServiceMock;
    private readonly Mock<IDeploymentRepository> _deploymentRepoMock;
    private readonly Mock<IHealthSnapshotRepository> _healthSnapshotRepoMock;
    private readonly Mock<ILogger<HealthMonitoringService>> _loggerMock;
    private readonly HealthMonitoringService _service;

    private readonly OrganizationId _orgId = OrganizationId.NewId();
    private readonly EnvironmentId _envId = EnvironmentId.NewId();
    private readonly DeploymentId _deploymentId = DeploymentId.NewId();

    public HealthMonitoringServiceTests()
    {
        _dockerServiceMock = new Mock<IDockerService>();
        _deploymentRepoMock = new Mock<IDeploymentRepository>();
        _healthSnapshotRepoMock = new Mock<IHealthSnapshotRepository>();
        _loggerMock = new Mock<ILogger<HealthMonitoringService>>();

        _service = new HealthMonitoringService(
            _dockerServiceMock.Object,
            _deploymentRepoMock.Object,
            _healthSnapshotRepoMock.Object,
            _loggerMock.Object);
    }

    private ContainerDto CreateContainer(
        string id,
        string name,
        string state = "running",
        string healthStatus = "healthy",
        string stackLabel = "test-stack")
    {
        return new ContainerDto
        {
            Id = id,
            Name = name,
            Image = "test-image:latest",
            State = state,
            Status = state == "running" ? "Up 1 hour" : "Exited (1) 5 minutes ago",
            Created = DateTime.UtcNow.AddHours(-1),
            HealthStatus = healthStatus,
            Labels = new Dictionary<string, string>
            {
                ["com.docker.compose.project"] = stackLabel
            }
        };
    }

    #region RestartCount Tests

    [Fact]
    public async Task CaptureHealthSnapshot_HealthyContainers_DoesNotFetchRestartCount()
    {
        // Arrange
        var containers = new[]
        {
            CreateContainer("c1", "web", "running", "healthy"),
            CreateContainer("c2", "api", "running", "healthy")
        };

        _dockerServiceMock
            .Setup(d => d.ListContainersAsync(_envId.Value.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        _deploymentRepoMock
            .Setup(r => r.Get(_deploymentId))
            .Returns((Deployment?)null);

        // Act
        var snapshot = await _service.CaptureHealthSnapshotAsync(
            _orgId, _envId, _deploymentId, "test-stack", "1.0.0", serviceHealthConfigs: null);

        // Assert
        _dockerServiceMock.Verify(
            d => d.GetContainerRestartCountAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "RestartCount should NOT be fetched for healthy containers");

        snapshot.Self.Services.Should().AllSatisfy(s => s.RestartCount.Should().BeNull());
    }

    [Fact]
    public async Task CaptureHealthSnapshot_UnhealthyContainer_FetchesRestartCount()
    {
        // Arrange
        var containers = new[]
        {
            CreateContainer("c1", "web", "running", "healthy"),
            CreateContainer("c2", "api", "running", "unhealthy")  // Unhealthy!
        };

        _dockerServiceMock
            .Setup(d => d.ListContainersAsync(_envId.Value.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        _dockerServiceMock
            .Setup(d => d.GetContainerRestartCountAsync(_envId.Value.ToString(), "c2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        _deploymentRepoMock
            .Setup(r => r.Get(_deploymentId))
            .Returns((Deployment?)null);

        // Act
        var snapshot = await _service.CaptureHealthSnapshotAsync(
            _orgId, _envId, _deploymentId, "test-stack", "1.0.0", serviceHealthConfigs: null);

        // Assert
        _dockerServiceMock.Verify(
            d => d.GetContainerRestartCountAsync(_envId.Value.ToString(), "c2", It.IsAny<CancellationToken>()),
            Times.Once,
            "RestartCount should be fetched for unhealthy container");

        _dockerServiceMock.Verify(
            d => d.GetContainerRestartCountAsync(_envId.Value.ToString(), "c1", It.IsAny<CancellationToken>()),
            Times.Never,
            "RestartCount should NOT be fetched for healthy container");

        var unhealthyService = snapshot.Self.Services.Single(s => s.Name == "api");
        unhealthyService.RestartCount.Should().Be(5);

        var healthyService = snapshot.Self.Services.Single(s => s.Name == "web");
        healthyService.RestartCount.Should().BeNull();
    }

    [Fact]
    public async Task CaptureHealthSnapshot_ExitedContainer_FetchesRestartCount()
    {
        // Arrange - "exited" state = Unhealthy
        var containers = new[]
        {
            CreateContainer("c1", "web", "exited", "none")
        };

        _dockerServiceMock
            .Setup(d => d.ListContainersAsync(_envId.Value.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        _dockerServiceMock
            .Setup(d => d.GetContainerRestartCountAsync(_envId.Value.ToString(), "c1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        _deploymentRepoMock
            .Setup(r => r.Get(_deploymentId))
            .Returns((Deployment?)null);

        // Act
        var snapshot = await _service.CaptureHealthSnapshotAsync(
            _orgId, _envId, _deploymentId, "test-stack", "1.0.0", serviceHealthConfigs: null);

        // Assert
        _dockerServiceMock.Verify(
            d => d.GetContainerRestartCountAsync(_envId.Value.ToString(), "c1", It.IsAny<CancellationToken>()),
            Times.Once,
            "RestartCount should be fetched for exited container");

        var service = snapshot.Self.Services.Single();
        service.RestartCount.Should().Be(3);
        service.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CaptureHealthSnapshot_DegradedContainer_FetchesRestartCount()
    {
        // Arrange - "restarting" state = Degraded
        var containers = new[]
        {
            CreateContainer("c1", "web", "restarting", "none")
        };

        _dockerServiceMock
            .Setup(d => d.ListContainersAsync(_envId.Value.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        _dockerServiceMock
            .Setup(d => d.GetContainerRestartCountAsync(_envId.Value.ToString(), "c1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        _deploymentRepoMock
            .Setup(r => r.Get(_deploymentId))
            .Returns((Deployment?)null);

        // Act
        var snapshot = await _service.CaptureHealthSnapshotAsync(
            _orgId, _envId, _deploymentId, "test-stack", "1.0.0", serviceHealthConfigs: null);

        // Assert
        _dockerServiceMock.Verify(
            d => d.GetContainerRestartCountAsync(_envId.Value.ToString(), "c1", It.IsAny<CancellationToken>()),
            Times.Once,
            "RestartCount should be fetched for degraded (restarting) container");

        var service = snapshot.Self.Services.Single();
        service.RestartCount.Should().Be(10);
        service.Status.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public async Task CaptureHealthSnapshot_MixedContainers_OnlyFetchesRestartCountForUnhealthy()
    {
        // Arrange - Mix of healthy, unhealthy, and degraded
        var containers = new[]
        {
            CreateContainer("c1", "web", "running", "healthy"),      // Healthy - no fetch
            CreateContainer("c2", "api", "running", "unhealthy"),    // Unhealthy - fetch
            CreateContainer("c3", "worker", "running", "healthy"),   // Healthy - no fetch
            CreateContainer("c4", "db", "exited", "none"),           // Unhealthy - fetch
            CreateContainer("c5", "cache", "restarting", "none")     // Degraded - fetch
        };

        _dockerServiceMock
            .Setup(d => d.ListContainersAsync(_envId.Value.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        _dockerServiceMock
            .Setup(d => d.GetContainerRestartCountAsync(_envId.Value.ToString(), "c2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _dockerServiceMock
            .Setup(d => d.GetContainerRestartCountAsync(_envId.Value.ToString(), "c4", It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _dockerServiceMock
            .Setup(d => d.GetContainerRestartCountAsync(_envId.Value.ToString(), "c5", It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        _deploymentRepoMock
            .Setup(r => r.Get(_deploymentId))
            .Returns((Deployment?)null);

        // Act
        var snapshot = await _service.CaptureHealthSnapshotAsync(
            _orgId, _envId, _deploymentId, "test-stack", "1.0.0", serviceHealthConfigs: null);

        // Assert - Only 3 calls for unhealthy/degraded containers
        _dockerServiceMock.Verify(
            d => d.GetContainerRestartCountAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3),
            "RestartCount should only be fetched for 3 non-healthy containers");

        // Verify specific containers
        _dockerServiceMock.Verify(
            d => d.GetContainerRestartCountAsync(_envId.Value.ToString(), "c1", It.IsAny<CancellationToken>()),
            Times.Never);
        _dockerServiceMock.Verify(
            d => d.GetContainerRestartCountAsync(_envId.Value.ToString(), "c3", It.IsAny<CancellationToken>()),
            Times.Never);

        // Check RestartCount values
        snapshot.Self.Services.Single(s => s.Name == "web").RestartCount.Should().BeNull();
        snapshot.Self.Services.Single(s => s.Name == "api").RestartCount.Should().Be(2);
        snapshot.Self.Services.Single(s => s.Name == "worker").RestartCount.Should().BeNull();
        snapshot.Self.Services.Single(s => s.Name == "db").RestartCount.Should().Be(1);
        snapshot.Self.Services.Single(s => s.Name == "cache").RestartCount.Should().Be(7);
    }

    [Fact]
    public async Task CaptureHealthSnapshot_StartingHealthCheck_FetchesRestartCount()
    {
        // Arrange - "starting" health status = Degraded
        var containers = new[]
        {
            CreateContainer("c1", "web", "running", "starting")
        };

        _dockerServiceMock
            .Setup(d => d.ListContainersAsync(_envId.Value.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        _dockerServiceMock
            .Setup(d => d.GetContainerRestartCountAsync(_envId.Value.ToString(), "c1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _deploymentRepoMock
            .Setup(r => r.Get(_deploymentId))
            .Returns((Deployment?)null);

        // Act
        var snapshot = await _service.CaptureHealthSnapshotAsync(
            _orgId, _envId, _deploymentId, "test-stack", "1.0.0", serviceHealthConfigs: null);

        // Assert
        _dockerServiceMock.Verify(
            d => d.GetContainerRestartCountAsync(_envId.Value.ToString(), "c1", It.IsAny<CancellationToken>()),
            Times.Once,
            "RestartCount should be fetched for container with starting health check");

        var service = snapshot.Self.Services.Single();
        service.RestartCount.Should().Be(0);
        service.Status.Should().Be(HealthStatus.Degraded);
    }

    #endregion
}
