using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Application.UseCases.Containers.ListContainers;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;

namespace ReadyStackGo.UnitTests.UseCases.Containers;

public class ListContainersHandlerTests
{
    private readonly Mock<IDockerService> _dockerServiceMock;
    private readonly Mock<IHealthSnapshotRepository> _healthSnapshotRepoMock;
    private readonly Mock<ILogger<ListContainersHandler>> _loggerMock;
    private readonly ListContainersHandler _handler;

    private readonly EnvironmentId _envId = EnvironmentId.NewId();

    public ListContainersHandlerTests()
    {
        _dockerServiceMock = new Mock<IDockerService>();
        _healthSnapshotRepoMock = new Mock<IHealthSnapshotRepository>();
        _loggerMock = new Mock<ILogger<ListContainersHandler>>();

        _handler = new ListContainersHandler(
            _dockerServiceMock.Object,
            _healthSnapshotRepoMock.Object,
            _loggerMock.Object);
    }

    private static ContainerDto CreateContainer(string name, string state = "running", string healthStatus = "none")
    {
        return new ContainerDto
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Name = name,
            Image = "test:latest",
            State = state,
            Status = "Up 1 hour",
            HealthStatus = healthStatus,
            Labels = new Dictionary<string, string>()
        };
    }

    private static HealthSnapshot CreateSnapshot(
        EnvironmentId envId,
        string stackName,
        params ServiceHealth[] services)
    {
        return HealthSnapshot.Capture(
            OrganizationId.NewId(),
            envId,
            DeploymentId.NewId(),
            stackName,
            OperationMode.Normal,
            self: SelfHealth.Create(services));
    }

    [Fact]
    public async Task Handle_EnrichesContainersWithHealthSnapshotData()
    {
        // Arrange
        var containers = new[]
        {
            CreateContainer("memo-api", healthStatus: "unhealthy"),
            CreateContainer("memo-web", healthStatus: "unhealthy")
        };

        _dockerServiceMock
            .Setup(d => d.ListContainersAsync(_envId.Value.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        var snapshot = CreateSnapshot(_envId, "Memo",
            ServiceHealth.Create("memo-api", HealthStatus.Healthy, containerName: "memo-api"),
            ServiceHealth.Create("memo-web", HealthStatus.Healthy, containerName: "memo-web"));

        _healthSnapshotRepoMock
            .Setup(r => r.GetLatestForEnvironment(_envId))
            .Returns(new[] { snapshot });

        // Act
        var result = await _handler.Handle(
            new ListContainersQuery(_envId.Value.ToString()), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Containers.Should().AllSatisfy(c =>
            c.HealthStatus.Should().Be("healthy"));
    }

    [Fact]
    public async Task Handle_MultipleSnapshots_LastWriteWins()
    {
        // Stale deployments are removed during product upgrade (UpgradeProductHandler),
        // so under normal operation only one active deployment monitors each container.
        // When multiple snapshots exist, the last one in the iteration wins.
        var containers = new[] { CreateContainer("memo-web") };

        _dockerServiceMock
            .Setup(d => d.ListContainersAsync(_envId.Value.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        var firstSnapshot = CreateSnapshot(_envId, "Memo",
            ServiceHealth.Create("memo-web", HealthStatus.Unhealthy, containerName: "memo-web"));
        var secondSnapshot = CreateSnapshot(_envId, "Memo",
            ServiceHealth.Create("memo-web", HealthStatus.Healthy, containerName: "memo-web"));

        _healthSnapshotRepoMock
            .Setup(r => r.GetLatestForEnvironment(_envId))
            .Returns(new[] { firstSnapshot, secondSnapshot });

        var result = await _handler.Handle(
            new ListContainersQuery(_envId.Value.ToString()), CancellationToken.None);

        result.Containers.Single().HealthStatus.Should().Be("healthy",
            "last snapshot in the collection wins");
    }

    [Fact]
    public async Task Handle_NoSnapshots_ReturnsOriginalContainerData()
    {
        // Arrange
        var containers = new[] { CreateContainer("api", healthStatus: "unhealthy") };

        _dockerServiceMock
            .Setup(d => d.ListContainersAsync(_envId.Value.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        _healthSnapshotRepoMock
            .Setup(r => r.GetLatestForEnvironment(_envId))
            .Returns(Enumerable.Empty<HealthSnapshot>());

        // Act
        var result = await _handler.Handle(
            new ListContainersQuery(_envId.Value.ToString()), CancellationToken.None);

        // Assert - Original Docker health status preserved
        result.Containers.Single().HealthStatus.Should().Be("unhealthy");
    }

    [Fact]
    public async Task Handle_NonGuidEnvironmentId_SkipsEnrichment()
    {
        // Arrange
        var containers = new[] { CreateContainer("api", healthStatus: "unhealthy") };

        _dockerServiceMock
            .Setup(d => d.ListContainersAsync("not-a-guid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        // Act
        var result = await _handler.Handle(
            new ListContainersQuery("not-a-guid"), CancellationToken.None);

        // Assert
        result.Containers.Single().HealthStatus.Should().Be("unhealthy");
        _healthSnapshotRepoMock.Verify(
            r => r.GetLatestForEnvironment(It.IsAny<EnvironmentId>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SnapshotRepositoryThrows_ReturnsOriginalData()
    {
        // Arrange
        var containers = new[] { CreateContainer("api", healthStatus: "none") };

        _dockerServiceMock
            .Setup(d => d.ListContainersAsync(_envId.Value.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        _healthSnapshotRepoMock
            .Setup(r => r.GetLatestForEnvironment(_envId))
            .Throws(new InvalidOperationException("DB error"));

        // Act
        var result = await _handler.Handle(
            new ListContainersQuery(_envId.Value.ToString()), CancellationToken.None);

        // Assert - Graceful degradation
        result.Success.Should().BeTrue();
        result.Containers.Single().HealthStatus.Should().Be("none");
    }

    [Fact]
    public async Task Handle_ContainerNotInSnapshot_KeepsOriginalStatus()
    {
        // Arrange
        var containers = new[]
        {
            CreateContainer("memo-api", healthStatus: "unhealthy"),
            CreateContainer("readystackgo", healthStatus: "none")  // RSGO itself, not in snapshots
        };

        _dockerServiceMock
            .Setup(d => d.ListContainersAsync(_envId.Value.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        var snapshot = CreateSnapshot(_envId, "Memo",
            ServiceHealth.Create("memo-api", HealthStatus.Healthy, containerName: "memo-api"));

        _healthSnapshotRepoMock
            .Setup(r => r.GetLatestForEnvironment(_envId))
            .Returns(new[] { snapshot });

        // Act
        var result = await _handler.Handle(
            new ListContainersQuery(_envId.Value.ToString()), CancellationToken.None);

        // Assert
        var resultList = result.Containers.ToList();
        resultList.Single(c => c.Name == "memo-api").HealthStatus.Should().Be("healthy");
        resultList.Single(c => c.Name == "readystackgo").HealthStatus.Should().Be("none");
    }

    [Fact]
    public async Task Handle_BothDeploymentsUnhealthy_ShowsUnhealthy()
    {
        // Arrange - Both stale and current deployments report unhealthy
        var containers = new[] { CreateContainer("broken-api") };

        _dockerServiceMock
            .Setup(d => d.ListContainersAsync(_envId.Value.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        var snapshot1 = CreateSnapshot(_envId, "OldStack",
            ServiceHealth.Create("broken-api", HealthStatus.Unhealthy, containerName: "broken-api"));
        var snapshot2 = CreateSnapshot(_envId, "NewStack",
            ServiceHealth.Create("broken-api", HealthStatus.Unhealthy, containerName: "broken-api"));

        _healthSnapshotRepoMock
            .Setup(r => r.GetLatestForEnvironment(_envId))
            .Returns(new[] { snapshot1, snapshot2 });

        // Act
        var result = await _handler.Handle(
            new ListContainersQuery(_envId.Value.ToString()), CancellationToken.None);

        // Assert - Both unhealthy → still unhealthy
        result.Containers.Single().HealthStatus.Should().Be("unhealthy");
    }
}
