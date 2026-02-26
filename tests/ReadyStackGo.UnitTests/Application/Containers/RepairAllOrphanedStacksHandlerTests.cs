using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Application.UseCases.Containers.RepairAllOrphanedStacks;
using ReadyStackGo.Application.UseCases.Containers.RepairOrphanedStack;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.UnitTests.Application.Containers;

public class RepairAllOrphanedStacksHandlerTests
{
    private readonly Mock<IDockerService> _dockerServiceMock;
    private readonly Mock<IDeploymentRepository> _deploymentRepoMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly RepairAllOrphanedStacksHandler _handler;

    private static readonly string EnvId = Guid.NewGuid().ToString();
    private static readonly string UserId = Guid.NewGuid().ToString();

    public RepairAllOrphanedStacksHandlerTests()
    {
        _dockerServiceMock = new Mock<IDockerService>();
        _deploymentRepoMock = new Mock<IDeploymentRepository>();
        _mediatorMock = new Mock<IMediator>();
        _handler = new RepairAllOrphanedStacksHandler(
            _dockerServiceMock.Object,
            _deploymentRepoMock.Object,
            _mediatorMock.Object,
            Mock.Of<ILogger<RepairAllOrphanedStacksHandler>>());
    }

    private static ContainerDto MakeContainer(string id, string name, string stackLabel) =>
        new()
        {
            Id = id,
            Name = name,
            Image = "test:latest",
            State = "running",
            Status = "Up 5 minutes",
            Labels = new Dictionary<string, string> { ["rsgo.stack"] = stackLabel }
        };

    #region Happy Path

    [Fact]
    public async Task Handle_MultipleOrphanedStacks_RepairsAll()
    {
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                MakeContainer("c1", "app1", "stack-a"),
                MakeContainer("c2", "app2", "stack-b"),
                MakeContainer("c3", "app3", "stack-c"),
            });

        // All stacks are orphaned
        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), It.IsAny<string>()))
            .Returns((Deployment?)null);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RepairOrphanedStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepairOrphanedStackResult(true, Guid.NewGuid().ToString()));

        var result = await _handler.Handle(
            new RepairAllOrphanedStacksCommand(EnvId, UserId), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RepairedCount.Should().Be(3);
        result.FailedCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_NoOrphanedStacks_ReturnsZeroCounts()
    {
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                MakeContainer("c1", "app1", "stack-a"),
            });

        // Stack has a deployment record → not orphaned
        var environmentId = new EnvironmentId(Guid.Parse(EnvId));
        var deployment = Deployment.StartInstallation(
            DeploymentId.NewId(), environmentId, "stack:id", "stack-a", "stack-a",
            ReadyStackGo.Domain.Deployment.UserId.NewId());

        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), "stack-a"))
            .Returns(deployment);

        var result = await _handler.Handle(
            new RepairAllOrphanedStacksCommand(EnvId, UserId), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RepairedCount.Should().Be(0);
        result.FailedCount.Should().Be(0);

        _mediatorMock.Verify(
            m => m.Send(It.IsAny<RepairOrphanedStackCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_NoContainersAtAll_ReturnsZeroCounts()
    {
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ContainerDto>());

        var result = await _handler.Handle(
            new RepairAllOrphanedStacksCommand(EnvId, UserId), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RepairedCount.Should().Be(0);
        result.FailedCount.Should().Be(0);
    }

    #endregion

    #region Mixed Scenarios

    [Fact]
    public async Task Handle_MixedOrphanedAndManaged_OnlyRepairsOrphaned()
    {
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                MakeContainer("c1", "app1", "orphaned-stack"),
                MakeContainer("c2", "app2", "managed-stack"),
            });

        var environmentId = new EnvironmentId(Guid.Parse(EnvId));
        var deployment = Deployment.StartInstallation(
            DeploymentId.NewId(), environmentId, "stack:id", "managed-stack", "managed-stack",
            ReadyStackGo.Domain.Deployment.UserId.NewId());

        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), "managed-stack"))
            .Returns(deployment);
        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), "orphaned-stack"))
            .Returns((Deployment?)null);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RepairOrphanedStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepairOrphanedStackResult(true, Guid.NewGuid().ToString()));

        var result = await _handler.Handle(
            new RepairAllOrphanedStacksCommand(EnvId, UserId), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RepairedCount.Should().Be(1);
        result.FailedCount.Should().Be(0);

        _mediatorMock.Verify(
            m => m.Send(It.Is<RepairOrphanedStackCommand>(c => c.StackName == "orphaned-stack"),
                It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.Verify(
            m => m.Send(It.Is<RepairOrphanedStackCommand>(c => c.StackName == "managed-stack"),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SomeRepairsFail_ReturnsPartialSuccess()
    {
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                MakeContainer("c1", "app1", "stack-a"),
                MakeContainer("c2", "app2", "stack-b"),
                MakeContainer("c3", "app3", "stack-c"),
            });

        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), It.IsAny<string>()))
            .Returns((Deployment?)null);

        // stack-a succeeds, stack-b fails, stack-c succeeds
        _mediatorMock
            .Setup(m => m.Send(
                It.Is<RepairOrphanedStackCommand>(c => c.StackName == "stack-a"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepairOrphanedStackResult(true, Guid.NewGuid().ToString()));
        _mediatorMock
            .Setup(m => m.Send(
                It.Is<RepairOrphanedStackCommand>(c => c.StackName == "stack-b"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepairOrphanedStackResult(false, ErrorMessage: "No service containers found"));
        _mediatorMock
            .Setup(m => m.Send(
                It.Is<RepairOrphanedStackCommand>(c => c.StackName == "stack-c"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepairOrphanedStackResult(true, Guid.NewGuid().ToString()));

        var result = await _handler.Handle(
            new RepairAllOrphanedStacksCommand(EnvId, UserId), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RepairedCount.Should().Be(2);
        result.FailedCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DuplicateStackNames_DeduplicatedByCase()
    {
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                MakeContainer("c1", "app1", "my-stack"),
                MakeContainer("c2", "app2", "MY-STACK"),
                MakeContainer("c3", "app3", "My-Stack"),
            });

        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), It.IsAny<string>()))
            .Returns((Deployment?)null);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RepairOrphanedStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepairOrphanedStackResult(true, Guid.NewGuid().ToString()));

        var result = await _handler.Handle(
            new RepairAllOrphanedStacksCommand(EnvId, UserId), CancellationToken.None);

        result.Success.Should().BeTrue();
        // Should be deduplicated to a single stack
        result.RepairedCount.Should().Be(1);
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<RepairOrphanedStackCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Validation

    [Fact]
    public async Task Handle_InvalidEnvironmentId_ReturnsError()
    {
        var result = await _handler.Handle(
            new RepairAllOrphanedStacksCommand("not-a-guid", UserId), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid environment ID");
    }

    [Fact]
    public async Task Handle_ContainersWithoutStackLabel_AreIgnored()
    {
        var unlabeled = new ContainerDto
        {
            Id = "c1",
            Name = "random",
            Image = "test:latest",
            State = "running",
            Status = "Up",
            Labels = new Dictionary<string, string>()
        };

        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { unlabeled });

        var result = await _handler.Handle(
            new RepairAllOrphanedStacksCommand(EnvId, UserId), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RepairedCount.Should().Be(0);
    }

    #endregion

    #region Error Handling

    [Fact]
    public async Task Handle_DockerServiceThrows_ReturnsError()
    {
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Docker daemon unavailable"));

        var result = await _handler.Handle(
            new RepairAllOrphanedStacksCommand(EnvId, UserId), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Docker daemon unavailable");
    }

    [Fact]
    public async Task Handle_PassesCorrectUserIdToRepairCommands()
    {
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeContainer("c1", "app", "orphan") });

        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), "orphan"))
            .Returns((Deployment?)null);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RepairOrphanedStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepairOrphanedStackResult(true, Guid.NewGuid().ToString()));

        await _handler.Handle(
            new RepairAllOrphanedStacksCommand(EnvId, UserId), CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<RepairOrphanedStackCommand>(c =>
                c.EnvironmentId == EnvId && c.UserId == UserId && c.StackName == "orphan"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
