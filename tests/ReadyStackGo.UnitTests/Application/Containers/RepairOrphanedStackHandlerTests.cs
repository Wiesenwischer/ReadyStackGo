using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Application.UseCases.Containers.RepairOrphanedStack;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.UnitTests.Application.Containers;

public class RepairOrphanedStackHandlerTests
{
    private readonly Mock<IDockerService> _dockerServiceMock;
    private readonly Mock<IDeploymentRepository> _deploymentRepoMock;
    private readonly Mock<IProductCache> _productCacheMock;
    private readonly RepairOrphanedStackHandler _handler;

    private static readonly string EnvId = Guid.NewGuid().ToString();
    private static readonly string UserId = Guid.NewGuid().ToString();

    public RepairOrphanedStackHandlerTests()
    {
        _dockerServiceMock = new Mock<IDockerService>();
        _deploymentRepoMock = new Mock<IDeploymentRepository>();
        _productCacheMock = new Mock<IProductCache>();

        _deploymentRepoMock
            .Setup(r => r.NextIdentity())
            .Returns(DeploymentId.NewId());

        _productCacheMock
            .Setup(c => c.GetAllStacks())
            .Returns(Enumerable.Empty<StackDefinition>());

        _handler = new RepairOrphanedStackHandler(
            _dockerServiceMock.Object,
            _deploymentRepoMock.Object,
            _productCacheMock.Object,
            Mock.Of<ILogger<RepairOrphanedStackHandler>>());
    }

    private static ContainerDto MakeServiceContainer(
        string id, string name, string stackLabel,
        string state = "running", string? context = null, string? lifecycle = null)
    {
        var labels = new Dictionary<string, string> { ["rsgo.stack"] = stackLabel };
        if (context != null) labels["rsgo.context"] = context;
        if (lifecycle != null) labels["rsgo.lifecycle"] = lifecycle;
        return new()
        {
            Id = id,
            Name = name,
            Image = $"{name}:latest",
            State = state,
            Status = "Up 5 minutes",
            Labels = labels
        };
    }

    #region Happy Path — Catalog Match

    [Fact]
    public async Task Handle_WithCatalogMatch_CreatesDeploymentWithRealStackId()
    {
        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), "myapp-webapp"))
            .Returns((Deployment?)null);

        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                MakeServiceContainer("c1", "frontend", "myapp-webapp", context: "frontend"),
                MakeServiceContainer("c2", "backend", "myapp-webapp", context: "backend"),
            });

        // Catalog has a stack named "webapp" — matches suffix of "myapp-webapp"
        var stackDef = new StackDefinition("source1", "webapp", ProductId.FromName("myproduct"));
        _productCacheMock
            .Setup(c => c.GetAllStacks())
            .Returns(new[] { stackDef });

        var result = await _handler.Handle(
            new RepairOrphanedStackCommand(EnvId, "myapp-webapp", UserId), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.DeploymentId.Should().NotBeNullOrEmpty();
        result.CatalogMatched.Should().BeTrue();

        _deploymentRepoMock.Verify(r => r.Add(It.IsAny<Deployment>()), Times.Once);
        _deploymentRepoMock.Verify(r => r.SaveChanges(), Times.Once);
    }

    [Fact]
    public async Task Handle_ExactStackNameMatch_CatalogMatched()
    {
        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), "webapp"))
            .Returns((Deployment?)null);

        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                MakeServiceContainer("c1", "app", "webapp", context: "app"),
            });

        var stackDef = new StackDefinition("source1", "webapp", ProductId.FromName("myproduct"));
        _productCacheMock
            .Setup(c => c.GetAllStacks())
            .Returns(new[] { stackDef });

        var result = await _handler.Handle(
            new RepairOrphanedStackCommand(EnvId, "webapp", UserId), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CatalogMatched.Should().BeTrue();
    }

    #endregion

    #region Happy Path — No Catalog Match

    [Fact]
    public async Task Handle_NoCatalogMatch_CreatesSyntheticStackId()
    {
        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), "unknown-stack"))
            .Returns((Deployment?)null);

        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                MakeServiceContainer("c1", "app", "unknown-stack", context: "app"),
            });

        var result = await _handler.Handle(
            new RepairOrphanedStackCommand(EnvId, "unknown-stack", UserId), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.DeploymentId.Should().NotBeNullOrEmpty();
        result.CatalogMatched.Should().BeFalse();

        _deploymentRepoMock.Verify(r => r.Add(It.Is<Deployment>(d =>
            d.StackId == "orphan:unknown-stack")), Times.Once);
    }

    #endregion

    #region Service Discovery

    [Fact]
    public async Task Handle_UsesContextLabelAsServiceName()
    {
        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), "my-stack"))
            .Returns((Deployment?)null);

        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                MakeServiceContainer("c1", "container-name", "my-stack", context: "my-service"),
            });

        var result = await _handler.Handle(
            new RepairOrphanedStackCommand(EnvId, "my-stack", UserId), CancellationToken.None);

        result.Success.Should().BeTrue();
        _deploymentRepoMock.Verify(r => r.Add(It.Is<Deployment>(d =>
            d.Services.Any(s => s.ServiceName == "my-service"))), Times.Once);
    }

    [Fact]
    public async Task Handle_FallsBackToContainerNameWhenNoContextLabel()
    {
        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), "my-stack"))
            .Returns((Deployment?)null);

        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                MakeServiceContainer("c1", "my-container", "my-stack"),
            });

        var result = await _handler.Handle(
            new RepairOrphanedStackCommand(EnvId, "my-stack", UserId), CancellationToken.None);

        result.Success.Should().BeTrue();
        _deploymentRepoMock.Verify(r => r.Add(It.Is<Deployment>(d =>
            d.Services.Any(s => s.ServiceName == "my-container"))), Times.Once);
    }

    [Fact]
    public async Task Handle_InitContainersAreExcluded()
    {
        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), "my-stack"))
            .Returns((Deployment?)null);

        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                MakeServiceContainer("c1", "app", "my-stack", context: "app"),
                MakeServiceContainer("c2", "init-db", "my-stack", context: "init-db", lifecycle: "init"),
                MakeServiceContainer("c3", "init-setup", "my-stack", context: "setup", lifecycle: "Init"),
            });

        var result = await _handler.Handle(
            new RepairOrphanedStackCommand(EnvId, "my-stack", UserId), CancellationToken.None);

        result.Success.Should().BeTrue();
        _deploymentRepoMock.Verify(r => r.Add(It.Is<Deployment>(d =>
            d.Services.Count == 1 && d.Services.First().ServiceName == "app")), Times.Once);
    }

    [Fact]
    public async Task Handle_AllContainersAreInit_ReturnsError()
    {
        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), "my-stack"))
            .Returns((Deployment?)null);

        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                MakeServiceContainer("c1", "init-db", "my-stack", lifecycle: "init"),
            });

        var result = await _handler.Handle(
            new RepairOrphanedStackCommand(EnvId, "my-stack", UserId), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No service containers");
    }

    #endregion

    #region Validation

    [Fact]
    public async Task Handle_InvalidEnvironmentId_ReturnsError()
    {
        var result = await _handler.Handle(
            new RepairOrphanedStackCommand("not-a-guid", "my-stack", UserId), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid environment ID");
    }

    [Fact]
    public async Task Handle_InvalidUserId_ReturnsError()
    {
        var result = await _handler.Handle(
            new RepairOrphanedStackCommand(EnvId, "my-stack", "not-a-guid"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid user ID");
    }

    [Fact]
    public async Task Handle_StackNotOrphaned_ReturnsError()
    {
        var environmentId = new EnvironmentId(Guid.Parse(EnvId));
        var deployment = Deployment.StartInstallation(
            DeploymentId.NewId(), environmentId, "stack:id", "my-stack", "my-stack",
            ReadyStackGo.Domain.Deployment.UserId.NewId());

        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), "my-stack"))
            .Returns(deployment);

        var result = await _handler.Handle(
            new RepairOrphanedStackCommand(EnvId, "my-stack", UserId), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not orphaned");
    }

    [Fact]
    public async Task Handle_NoContainersFound_ReturnsError()
    {
        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), "my-stack"))
            .Returns((Deployment?)null);

        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ContainerDto>());

        var result = await _handler.Handle(
            new RepairOrphanedStackCommand(EnvId, "my-stack", UserId), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No service containers");
    }

    #endregion

    #region Deployment State

    [Fact]
    public async Task Handle_CreatedDeploymentIsInRunningState()
    {
        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), "my-stack"))
            .Returns((Deployment?)null);

        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                MakeServiceContainer("c1", "app", "my-stack", context: "app"),
            });

        var result = await _handler.Handle(
            new RepairOrphanedStackCommand(EnvId, "my-stack", UserId), CancellationToken.None);

        result.Success.Should().BeTrue();
        _deploymentRepoMock.Verify(r => r.Add(It.Is<Deployment>(d =>
            d.Status == DeploymentStatus.Running)), Times.Once);
    }

    [Fact]
    public async Task Handle_StackNameMatchIsCaseInsensitive()
    {
        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), "my-stack"))
            .Returns((Deployment?)null);

        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                MakeServiceContainer("c1", "app", "MY-STACK", context: "app"),
            });

        var result = await _handler.Handle(
            new RepairOrphanedStackCommand(EnvId, "my-stack", UserId), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.DeploymentId.Should().NotBeNullOrEmpty();
    }

    #endregion
}
