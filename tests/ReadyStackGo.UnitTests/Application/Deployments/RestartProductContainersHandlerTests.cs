using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.RestartProductContainers;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using UserId = ReadyStackGo.Domain.Deployment.UserId;

namespace ReadyStackGo.UnitTests.Application.Deployments;

public class RestartProductContainersHandlerTests
{
    private readonly Mock<IProductDeploymentRepository> _repositoryMock;
    private readonly Mock<IDockerService> _dockerServiceMock;
    private readonly Mock<INotificationService> _inAppNotificationMock;
    private readonly Mock<ILogger<RestartProductContainersHandler>> _loggerMock;
    private readonly RestartProductContainersHandler _handler;

    private static readonly string TestEnvironmentId = Guid.NewGuid().ToString();

    public RestartProductContainersHandlerTests()
    {
        _repositoryMock = new Mock<IProductDeploymentRepository>();
        _dockerServiceMock = new Mock<IDockerService>();
        _inAppNotificationMock = new Mock<INotificationService>();
        _loggerMock = new Mock<ILogger<RestartProductContainersHandler>>();

        _handler = new RestartProductContainersHandler(
            _repositoryMock.Object,
            _dockerServiceMock.Object,
            _loggerMock.Object,
            _inAppNotificationMock.Object);
    }

    #region Test Helpers

    private static ProductDeployment CreateRunningDeployment(
        int stackCount = 3, string name = "test-product", string version = "1.0.0")
    {
        var stackConfigs = Enumerable.Range(0, stackCount).Select(i =>
            new StackDeploymentConfig(
                $"stack-{i}", $"Stack {i}", $"stacks:{name}:{version}:stack-{i}",
                2, new Dictionary<string, string> { [$"VAR_{i}"] = $"value-{i}" }))
            .ToList();

        var deployment = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(),
            new EnvironmentId(Guid.Parse(TestEnvironmentId)),
            $"stacks:{name}", $"stacks:{name}:{version}",
            name, $"Test Product {name}", version,
            UserId.Create(),
            "test-deployment",
            stackConfigs,
            new Dictionary<string, string> { ["SHARED"] = "shared-value" });

        foreach (var stack in deployment.GetStacksInDeployOrder())
        {
            var depId = DeploymentId.NewId();
            deployment.StartStack(stack.StackName, depId);
            deployment.CompleteStack(stack.StackName);
        }

        return deployment;
    }

    private static ProductDeployment CreatePartiallyRunningDeployment(int stackCount = 3)
    {
        var stackConfigs = Enumerable.Range(0, stackCount).Select(i =>
            new StackDeploymentConfig(
                $"stack-{i}", $"Stack {i}", $"stacks:test-product:1.0.0:stack-{i}",
                2, new Dictionary<string, string>()))
            .ToList();

        var deployment = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(),
            new EnvironmentId(Guid.Parse(TestEnvironmentId)),
            "stacks:test-product", "stacks:test-product:1.0.0",
            "test-product", "Test Product", "1.0.0",
            UserId.Create(),
            "test-deployment",
            stackConfigs,
            new Dictionary<string, string>());

        var stacks = deployment.GetStacksInDeployOrder();

        deployment.StartStack(stacks[0].StackName, DeploymentId.NewId());
        deployment.CompleteStack(stacks[0].StackName);

        deployment.StartStack(stacks[1].StackName, DeploymentId.NewId());
        deployment.FailStack(stacks[1].StackName, "Test failure");

        deployment.MarkAsPartiallyRunning("One stack failed");

        return deployment;
    }

    private void SetupDeploymentFound(ProductDeployment deployment)
    {
        _repositoryMock
            .Setup(r => r.Get(It.Is<ProductDeploymentId>(id => id == deployment.Id)))
            .Returns(deployment);
    }

    private void SetupAllRestartsSucceed(int containersPerStack = 3)
    {
        var containerIds = Enumerable.Range(0, containersPerStack).Select(i => $"container-{i}").ToList();

        _dockerServiceMock
            .Setup(d => d.StopStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containerIds);

        _dockerServiceMock
            .Setup(d => d.StartStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containerIds);
    }

    private RestartProductContainersCommand CreateCommand(
        ProductDeployment deployment, List<string>? stackNames = null)
    {
        return new RestartProductContainersCommand(
            TestEnvironmentId,
            deployment.Id.Value.ToString(),
            stackNames);
    }

    #endregion

    #region Happy Path

    [Fact]
    public async Task Handle_AllStacksRestarted_ReturnsSuccess()
    {
        var deployment = CreateRunningDeployment(3);
        SetupDeploymentFound(deployment);
        SetupAllRestartsSucceed();

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ProductDeploymentId.Should().NotBeNullOrEmpty();
        result.ProductName.Should().Be("test-product");
        result.TotalStacks.Should().Be(3);
        result.RestartedStacks.Should().Be(3);
        result.FailedStacks.Should().Be(0);
        result.Results.Should().HaveCount(3);
        result.Results.Should().AllSatisfy(r =>
        {
            r.Success.Should().BeTrue();
            r.ContainersStopped.Should().Be(3);
            r.ContainersStarted.Should().Be(3);
        });
    }

    [Fact]
    public async Task Handle_SingleStackProduct_Succeeds()
    {
        var deployment = CreateRunningDeployment(1);
        SetupDeploymentFound(deployment);
        SetupAllRestartsSucceed(2);

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TotalStacks.Should().Be(1);
        result.Results.Should().HaveCount(1);
        result.Results[0].ContainersStopped.Should().Be(2);
        result.Results[0].ContainersStarted.Should().Be(2);
    }

    [Fact]
    public async Task Handle_CallsStopThenStartForEachStack()
    {
        var deployment = CreateRunningDeployment(2);
        SetupDeploymentFound(deployment);
        SetupAllRestartsSucceed();

        var operations = new List<(string Operation, string StackName)>();

        _dockerServiceMock
            .Setup(d => d.StopStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, stackName, _) =>
                operations.Add(("Stop", stackName)))
            .ReturnsAsync(new List<string> { "c1" });

        _dockerServiceMock
            .Setup(d => d.StartStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, stackName, _) =>
                operations.Add(("Start", stackName)))
            .ReturnsAsync(new List<string> { "c1" });

        await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        var stacks = deployment.GetStacksInDeployOrder();
        // Expect: Stop(stack-0), Start(stack-0), Stop(stack-1), Start(stack-1)
        operations.Should().HaveCount(4);
        operations[0].Should().Be(("Stop", stacks[0].DeploymentStackName!));
        operations[1].Should().Be(("Start", stacks[0].DeploymentStackName!));
        operations[2].Should().Be(("Stop", stacks[1].DeploymentStackName!));
        operations[3].Should().Be(("Start", stacks[1].DeploymentStackName!));
    }

    #endregion

    #region Selective Stack Restart

    [Fact]
    public async Task Handle_WithSpecificStackNames_OnlyRestartsNamedStacks()
    {
        var deployment = CreateRunningDeployment(3);
        SetupDeploymentFound(deployment);
        SetupAllRestartsSucceed();

        var result = await _handler.Handle(
            CreateCommand(deployment, stackNames: new List<string> { "stack-1" }),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TotalStacks.Should().Be(1);
        result.Results.Should().HaveCount(1);
        result.Results[0].StackName.Should().Be("stack-1");

        _dockerServiceMock.Verify(d => d.StopStackContainersAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _dockerServiceMock.Verify(d => d.StartStackContainersAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UnknownStackName_ReturnsFailed()
    {
        var deployment = CreateRunningDeployment(2);
        SetupDeploymentFound(deployment);

        var result = await _handler.Handle(
            CreateCommand(deployment, stackNames: new List<string> { "nonexistent" }),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Unknown stack name");
        result.Message.Should().Contain("nonexistent");
    }

    #endregion

    #region Validation

    [Fact]
    public async Task Handle_InvalidProductDeploymentId_ReturnsFailed()
    {
        var command = new RestartProductContainersCommand(TestEnvironmentId, "not-a-guid");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid product deployment ID");
    }

    [Fact]
    public async Task Handle_ProductDeploymentNotFound_ReturnsFailed()
    {
        _repositoryMock
            .Setup(r => r.Get(It.IsAny<ProductDeploymentId>()))
            .Returns((ProductDeployment?)null);

        var command = new RestartProductContainersCommand(
            TestEnvironmentId, Guid.NewGuid().ToString());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_DeploymentInDeployingStatus_ReturnsFailed()
    {
        var stackConfigs = new List<StackDeploymentConfig>
        {
            new("s", "S", "sid", 1, new Dictionary<string, string>())
        };
        var deployment = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(),
            new EnvironmentId(Guid.Parse(TestEnvironmentId)),
            "gid", "pid", "test", "Test", "1.0.0",
            UserId.Create(), "test-deployment", stackConfigs, new Dictionary<string, string>());

        SetupDeploymentFound(deployment);

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Cannot restart containers");
        result.Message.Should().Contain("Deploying");
    }

    [Fact]
    public async Task Handle_DeploymentInFailedStatus_ReturnsFailed()
    {
        var stackConfigs = new List<StackDeploymentConfig>
        {
            new("stack-0", "Stack 0", "stacks:test:1.0.0:stack-0",
                2, new Dictionary<string, string>())
        };

        var deployment = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(),
            new EnvironmentId(Guid.Parse(TestEnvironmentId)),
            "stacks:test", "stacks:test:1.0.0",
            "test-product", "Test Product", "1.0.0",
            UserId.Create(), "test-deployment", stackConfigs, new Dictionary<string, string>());

        deployment.StartStack("stack-0", DeploymentId.NewId());
        deployment.FailStack("stack-0", "Critical failure");
        deployment.MarkAsFailed("All stacks failed");

        SetupDeploymentFound(deployment);

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Cannot restart containers");
    }

    #endregion

    #region Stop Failure Prevents Start

    [Fact]
    public async Task Handle_StopFails_StartNotCalledForThatStack()
    {
        var deployment = CreateRunningDeployment(2);
        SetupDeploymentFound(deployment);

        var stacks = deployment.GetStacksInDeployOrder();
        var firstStackName = stacks[0].DeploymentStackName!;

        // First stack: Stop throws → Start should NOT be called
        _dockerServiceMock
            .Setup(d => d.StopStackContainersAsync(
                It.IsAny<string>(), firstStackName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Docker error"));

        // Second stack: Stop + Start succeed
        _dockerServiceMock
            .Setup(d => d.StopStackContainersAsync(
                It.IsAny<string>(),
                It.Is<string>(s => s != firstStackName),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "c1" });
        _dockerServiceMock
            .Setup(d => d.StartStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "c1" });

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Results.Should().HaveCount(2);
        result.Results[0].Success.Should().BeFalse();
        result.Results[0].Error.Should().Contain("Docker error");
        result.Results[1].Success.Should().BeTrue();

        // Start should only be called for the second stack, not the first
        _dockerServiceMock.Verify(d => d.StartStackContainersAsync(
            It.IsAny<string>(), firstStackName, It.IsAny<CancellationToken>()), Times.Never);
        _dockerServiceMock.Verify(d => d.StartStackContainersAsync(
            It.IsAny<string>(),
            It.Is<string>(s => s != firstStackName),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_StartFailsAfterStop_ReportsFailure()
    {
        var deployment = CreateRunningDeployment(1);
        SetupDeploymentFound(deployment);

        _dockerServiceMock
            .Setup(d => d.StopStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "c1", "c2" });

        _dockerServiceMock
            .Setup(d => d.StartStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Start failed"));

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Results[0].Success.Should().BeFalse();
        result.Results[0].ContainersStopped.Should().Be(2);
        result.Results[0].ContainersStarted.Should().Be(0);
        result.Results[0].Error.Should().Contain("Start failed");
    }

    #endregion

    #region Partial Failure

    [Fact]
    public async Task Handle_OneStackRestartFails_ContinuesWithRemainingStacks()
    {
        var deployment = CreateRunningDeployment(3);
        SetupDeploymentFound(deployment);

        var callIndex = 0;
        _dockerServiceMock
            .Setup(d => d.StopStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, CancellationToken _) =>
            {
                if (callIndex++ == 1)
                    throw new InvalidOperationException("Docker daemon error");
                return (IReadOnlyList<string>)new List<string> { "c1" };
            });

        _dockerServiceMock
            .Setup(d => d.StartStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "c1" });

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Results.Should().HaveCount(3);
        result.Results[0].Success.Should().BeTrue();
        result.Results[1].Success.Should().BeFalse();
        result.Results[2].Success.Should().BeTrue();
        result.Success.Should().BeFalse();
        result.RestartedStacks.Should().Be(2);
        result.FailedStacks.Should().Be(1);
    }

    [Fact]
    public async Task Handle_PartialFailure_MessageContainsErrorCount()
    {
        var deployment = CreateRunningDeployment(3);
        SetupDeploymentFound(deployment);

        var callIndex = 0;
        _dockerServiceMock
            .Setup(d => d.StopStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, CancellationToken _) =>
            {
                if (callIndex++ == 0)
                    throw new Exception("Failure");
                return (IReadOnlyList<string>)new List<string> { "c1" };
            });

        _dockerServiceMock
            .Setup(d => d.StartStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "c1" });

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Message.Should().Contain("1 error");
        result.Message.Should().Contain("2/3");
    }

    #endregion

    #region State Machine Transitions

    [Fact]
    public async Task Handle_FromRunning_DoesNotChangeStatus()
    {
        var deployment = CreateRunningDeployment(2);
        var statusBefore = deployment.Status;
        SetupDeploymentFound(deployment);
        SetupAllRestartsSucceed();

        await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        deployment.Status.Should().Be(statusBefore);
        _repositoryMock.Verify(r => r.Update(It.IsAny<ProductDeployment>()), Times.Never);
        _repositoryMock.Verify(r => r.SaveChanges(), Times.Never);
    }

    [Fact]
    public async Task Handle_PartiallyRunningDeployment_DoesNotChangeStatus()
    {
        var deployment = CreatePartiallyRunningDeployment(3);
        deployment.Status.Should().Be(ProductDeploymentStatus.PartiallyRunning);
        SetupDeploymentFound(deployment);
        SetupAllRestartsSucceed();

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Success.Should().BeTrue();
        deployment.Status.Should().Be(ProductDeploymentStatus.PartiallyRunning);
    }

    [Fact]
    public async Task Handle_FromStopped_TransitionsToRunning()
    {
        var deployment = CreateRunningDeployment(2);
        deployment.MarkAsStopped("Stopped for test");
        SetupDeploymentFound(deployment);
        SetupAllRestartsSucceed();

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Success.Should().BeTrue();
        deployment.Status.Should().Be(ProductDeploymentStatus.Running);
        _repositoryMock.Verify(r => r.Update(deployment), Times.Once);
        _repositoryMock.Verify(r => r.SaveChanges(), Times.Once);
    }

    #endregion

    #region In-App Notification

    [Fact]
    public async Task Handle_Success_CreatesInAppNotification()
    {
        var deployment = CreateRunningDeployment(1);
        SetupDeploymentFound(deployment);
        SetupAllRestartsSucceed();

        await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        _inAppNotificationMock.Verify(n => n.AddAsync(
            It.Is<global::ReadyStackGo.Application.Notifications.Notification>(notif =>
                notif.Type == global::ReadyStackGo.Application.Notifications.NotificationType.ProductDeploymentResult),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Null Services

    [Fact]
    public async Task Handle_WithNullNotificationService_DoesNotThrow()
    {
        var handler = new RestartProductContainersHandler(
            _repositoryMock.Object,
            _dockerServiceMock.Object,
            _loggerMock.Object);

        var deployment = CreateRunningDeployment(1);
        SetupDeploymentFound(deployment);
        SetupAllRestartsSucceed();

        var act = () => handler.Handle(CreateCommand(deployment), CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    #endregion
}
