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
    private readonly Mock<IDeploymentRepository> _deploymentRepoMock;
    private readonly Mock<IHealthSnapshotRepository> _healthSnapshotRepoMock;
    private readonly Mock<ILogger<ListContainersHandler>> _loggerMock;
    private readonly ListContainersHandler _handler;

    private readonly EnvironmentId _envId = EnvironmentId.NewId();

    public ListContainersHandlerTests()
    {
        _dockerServiceMock = new Mock<IDockerService>();
        _deploymentRepoMock = new Mock<IDeploymentRepository>();
        _healthSnapshotRepoMock = new Mock<IHealthSnapshotRepository>();
        _loggerMock = new Mock<ILogger<ListContainersHandler>>();

        _handler = new ListContainersHandler(
            _dockerServiceMock.Object,
            _deploymentRepoMock.Object,
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

    private static (HealthSnapshot Snapshot, Deployment Deployment) CreateSnapshotWithDeployment(
        EnvironmentId envId,
        string stackName,
        DeploymentStatus deploymentStatus,
        params ServiceHealth[] services)
    {
        var deploymentId = DeploymentId.NewId();
        var deployment = Deployment.StartInstallation(
            deploymentId, envId, "stack-1", stackName, stackName,
            new UserId());
        if (deploymentStatus == DeploymentStatus.Running)
            deployment.MarkAsRunning();
        else if (deploymentStatus == DeploymentStatus.Removed)
        {
            deployment.MarkAsRunning();
            deployment.MarkAsRemoved();
        }

        var snapshot = HealthSnapshot.Capture(
            OrganizationId.NewId(),
            envId,
            deploymentId,
            stackName,
            OperationMode.Normal,
            self: SelfHealth.Create(services));

        return (snapshot, deployment);
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

    private void SetupDeployments(params Deployment[] deployments)
    {
        _deploymentRepoMock
            .Setup(r => r.GetByEnvironment(_envId))
            .Returns(deployments);
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

        var (snapshot, deployment) = CreateSnapshotWithDeployment(_envId, "Memo", DeploymentStatus.Running,
            ServiceHealth.Create("memo-api", HealthStatus.Healthy, containerName: "memo-api"),
            ServiceHealth.Create("memo-web", HealthStatus.Healthy, containerName: "memo-web"));

        SetupDeployments(deployment);

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
    public async Task Handle_RemovedDeploymentSnapshots_AreFilteredOut()
    {
        // Arrange - stale snapshot from removed deployment shows unhealthy,
        // but active deployment shows healthy. Container list must use active only.
        var containers = new[] { CreateContainer("memo-api", healthStatus: "unhealthy") };

        _dockerServiceMock
            .Setup(d => d.ListContainersAsync(_envId.Value.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        var (staleSnapshot, removedDeployment) = CreateSnapshotWithDeployment(
            _envId, "Memo-Old", DeploymentStatus.Removed,
            ServiceHealth.Create("memo-api", HealthStatus.Unhealthy, containerName: "memo-api"));

        var (activeSnapshot, activeDeployment) = CreateSnapshotWithDeployment(
            _envId, "Memo", DeploymentStatus.Running,
            ServiceHealth.Create("memo-api", HealthStatus.Healthy, containerName: "memo-api"));

        SetupDeployments(removedDeployment, activeDeployment);

        _healthSnapshotRepoMock
            .Setup(r => r.GetLatestForEnvironment(_envId))
            .Returns(new[] { staleSnapshot, activeSnapshot });

        // Act
        var result = await _handler.Handle(
            new ListContainersQuery(_envId.Value.ToString()), CancellationToken.None);

        // Assert - active snapshot wins, stale removed snapshot is filtered out
        result.Containers.Single().HealthStatus.Should().Be("healthy",
            "stale snapshots from removed deployments must not affect container health status");
    }

    [Fact]
    public async Task Handle_StaleSnapshotLastInCollection_StillFiltered()
    {
        // Arrange - stale unhealthy snapshot comes AFTER active healthy snapshot in collection.
        // Without filtering, last-write-wins would show unhealthy.
        var containers = new[] { CreateContainer("memo-api") };

        _dockerServiceMock
            .Setup(d => d.ListContainersAsync(_envId.Value.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        var (activeSnapshot, activeDeployment) = CreateSnapshotWithDeployment(
            _envId, "Memo", DeploymentStatus.Running,
            ServiceHealth.Create("memo-api", HealthStatus.Healthy, containerName: "memo-api"));

        var (staleSnapshot, removedDeployment) = CreateSnapshotWithDeployment(
            _envId, "Memo-Old", DeploymentStatus.Removed,
            ServiceHealth.Create("memo-api", HealthStatus.Unhealthy, containerName: "memo-api"));

        SetupDeployments(activeDeployment, removedDeployment);

        // Stale snapshot comes LAST — would overwrite healthy with unhealthy without filtering
        _healthSnapshotRepoMock
            .Setup(r => r.GetLatestForEnvironment(_envId))
            .Returns(new[] { activeSnapshot, staleSnapshot });

        // Act
        var result = await _handler.Handle(
            new ListContainersQuery(_envId.Value.ToString()), CancellationToken.None);

        // Assert
        result.Containers.Single().HealthStatus.Should().Be("healthy",
            "removed deployment snapshots must be filtered before building lookup");
    }

    [Fact]
    public async Task Handle_MultipleActiveSnapshots_LastWriteWins()
    {
        var containers = new[] { CreateContainer("memo-web") };

        _dockerServiceMock
            .Setup(d => d.ListContainersAsync(_envId.Value.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        var (firstSnapshot, firstDeployment) = CreateSnapshotWithDeployment(
            _envId, "Memo-1", DeploymentStatus.Running,
            ServiceHealth.Create("memo-web", HealthStatus.Unhealthy, containerName: "memo-web"));
        var (secondSnapshot, secondDeployment) = CreateSnapshotWithDeployment(
            _envId, "Memo-2", DeploymentStatus.Running,
            ServiceHealth.Create("memo-web", HealthStatus.Healthy, containerName: "memo-web"));

        SetupDeployments(firstDeployment, secondDeployment);

        _healthSnapshotRepoMock
            .Setup(r => r.GetLatestForEnvironment(_envId))
            .Returns(new[] { firstSnapshot, secondSnapshot });

        var result = await _handler.Handle(
            new ListContainersQuery(_envId.Value.ToString()), CancellationToken.None);

        result.Containers.Single().HealthStatus.Should().Be("healthy",
            "last active snapshot in the collection wins");
    }

    [Fact]
    public async Task Handle_NoSnapshots_ReturnsOriginalContainerData()
    {
        // Arrange — no snapshots → healthLookup is empty → early return, Docker status preserved
        var containers = new[] { CreateContainer("api", healthStatus: "unhealthy") };

        _dockerServiceMock
            .Setup(d => d.ListContainersAsync(_envId.Value.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        SetupDeployments();

        _healthSnapshotRepoMock
            .Setup(r => r.GetLatestForEnvironment(_envId))
            .Returns(Enumerable.Empty<HealthSnapshot>());

        // Act
        var result = await _handler.Handle(
            new ListContainersQuery(_envId.Value.ToString()), CancellationToken.None);

        // Assert - No snapshots → skip enrichment → Docker status passed through as-is
        result.Containers.Single().HealthStatus.Should().Be("unhealthy");
    }

    [Fact]
    public async Task Handle_RunningContainerNotInSnapshot_DockerHealthCheckIgnored()
    {
        // Arrange - container runs fine but Docker HEALTHCHECK reports "unhealthy"
        // (e.g., curl/wget not available in image). When snapshots exist but this
        // container is not tracked, fall back to state-based determination (running → healthy).
        var containers = new[]
        {
            CreateContainer("memo-api", state: "running", healthStatus: "unhealthy"),
            CreateContainer("untracked-worker", state: "running", healthStatus: "unhealthy")
        };

        _dockerServiceMock
            .Setup(d => d.ListContainersAsync(_envId.Value.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        // Only memo-api is in a snapshot
        var (snapshot, deployment) = CreateSnapshotWithDeployment(_envId, "Memo", DeploymentStatus.Running,
            ServiceHealth.Create("memo-api", HealthStatus.Healthy, containerName: "memo-api"));

        SetupDeployments(deployment);

        _healthSnapshotRepoMock
            .Setup(r => r.GetLatestForEnvironment(_envId))
            .Returns(new[] { snapshot });

        // Act
        var result = await _handler.Handle(
            new ListContainersQuery(_envId.Value.ToString()), CancellationToken.None);

        // Assert - both running containers show as healthy (Docker HEALTHCHECK ignored)
        result.Containers.Should().AllSatisfy(c =>
            c.HealthStatus.Should().Be("healthy",
                "running containers must not show Docker HEALTHCHECK 'unhealthy'"));
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
    public async Task Handle_ContainerNotInSnapshot_UsesStateFallback()
    {
        // Arrange - readystackgo is not tracked in any deployment snapshot
        var containers = new[]
        {
            CreateContainer("memo-api", state: "running", healthStatus: "unhealthy"),
            CreateContainer("readystackgo", state: "running", healthStatus: "none")
        };

        _dockerServiceMock
            .Setup(d => d.ListContainersAsync(_envId.Value.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        var (snapshot, deployment) = CreateSnapshotWithDeployment(_envId, "Memo", DeploymentStatus.Running,
            ServiceHealth.Create("memo-api", HealthStatus.Healthy, containerName: "memo-api"));

        SetupDeployments(deployment);

        _healthSnapshotRepoMock
            .Setup(r => r.GetLatestForEnvironment(_envId))
            .Returns(new[] { snapshot });

        // Act
        var result = await _handler.Handle(
            new ListContainersQuery(_envId.Value.ToString()), CancellationToken.None);

        // Assert - memo-api enriched from snapshot; readystackgo state=running → healthy
        var resultList = result.Containers.ToList();
        resultList.Single(c => c.Name == "memo-api").HealthStatus.Should().Be("healthy");
        resultList.Single(c => c.Name == "readystackgo").HealthStatus.Should().Be("healthy",
            "state=running containers not in any snapshot are treated as healthy");
    }

    [Fact]
    public async Task Handle_BothDeploymentsUnhealthy_ShowsUnhealthy()
    {
        // Arrange - Both active deployments report unhealthy
        var containers = new[] { CreateContainer("broken-api") };

        _dockerServiceMock
            .Setup(d => d.ListContainersAsync(_envId.Value.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        var (snapshot1, deployment1) = CreateSnapshotWithDeployment(
            _envId, "OldStack", DeploymentStatus.Running,
            ServiceHealth.Create("broken-api", HealthStatus.Unhealthy, containerName: "broken-api"));
        var (snapshot2, deployment2) = CreateSnapshotWithDeployment(
            _envId, "NewStack", DeploymentStatus.Running,
            ServiceHealth.Create("broken-api", HealthStatus.Unhealthy, containerName: "broken-api"));

        SetupDeployments(deployment1, deployment2);

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
