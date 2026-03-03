using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.StopProductContainers;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using UserId = ReadyStackGo.Domain.Deployment.UserId;

namespace ReadyStackGo.UnitTests.Application.Deployments;

public class StopProductContainersHandlerTests
{
    private readonly Mock<IProductDeploymentRepository> _repositoryMock;
    private readonly Mock<IDockerService> _dockerServiceMock;
    private readonly Mock<INotificationService> _inAppNotificationMock;
    private readonly Mock<ILogger<StopProductContainersHandler>> _loggerMock;
    private readonly StopProductContainersHandler _handler;

    private static readonly string TestEnvironmentId = Guid.NewGuid().ToString();

    public StopProductContainersHandlerTests()
    {
        _repositoryMock = new Mock<IProductDeploymentRepository>();
        _dockerServiceMock = new Mock<IDockerService>();
        _inAppNotificationMock = new Mock<INotificationService>();
        _loggerMock = new Mock<ILogger<StopProductContainersHandler>>();

        _handler = new StopProductContainersHandler(
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

        // Complete first stack
        deployment.StartStack(stacks[0].StackName, DeploymentId.NewId());
        deployment.CompleteStack(stacks[0].StackName);

        // Fail second stack
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

    private void SetupAllStopsSucceed(int containersPerStack = 3)
    {
        _dockerServiceMock
            .Setup(d => d.StopStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, containersPerStack).Select(i => $"container-{i}").ToList());
    }

    private StopProductContainersCommand CreateCommand(
        ProductDeployment deployment, List<string>? stackNames = null)
    {
        return new StopProductContainersCommand(
            TestEnvironmentId,
            deployment.Id.Value.ToString(),
            stackNames);
    }

    #endregion

    #region Happy Path

    [Fact]
    public async Task Handle_AllStacksStopped_ReturnsSuccess()
    {
        var deployment = CreateRunningDeployment(3);
        SetupDeploymentFound(deployment);
        SetupAllStopsSucceed();

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ProductDeploymentId.Should().NotBeNullOrEmpty();
        result.ProductName.Should().Be("test-product");
        result.TotalStacks.Should().Be(3);
        result.StoppedStacks.Should().Be(3);
        result.FailedStacks.Should().Be(0);
        result.Results.Should().HaveCount(3);
        result.Results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
    }

    [Fact]
    public async Task Handle_SingleStackProduct_Succeeds()
    {
        var deployment = CreateRunningDeployment(1);
        SetupDeploymentFound(deployment);
        SetupAllStopsSucceed(2);

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TotalStacks.Should().Be(1);
        result.Results.Should().HaveCount(1);
        result.Results[0].ContainersStopped.Should().Be(2);
    }

    [Fact]
    public async Task Handle_CallsStopForEachStack_WithCorrectDeploymentStackName()
    {
        var deployment = CreateRunningDeployment(2);
        SetupDeploymentFound(deployment);
        SetupAllStopsSucceed();

        var stoppedStackNames = new List<string>();
        _dockerServiceMock
            .Setup(d => d.StopStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, stackName, _) =>
                stoppedStackNames.Add(stackName))
            .ReturnsAsync(new List<string> { "c1" });

        await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        stoppedStackNames.Should().HaveCount(2);
        // DeploymentStackName is derived from deployment name + stack name
        var stacks = deployment.GetStacksInDeployOrder();
        stoppedStackNames.Should().Contain(stacks[0].DeploymentStackName!);
        stoppedStackNames.Should().Contain(stacks[1].DeploymentStackName!);
    }

    [Fact]
    public async Task Handle_PassesCorrectEnvironmentId()
    {
        var deployment = CreateRunningDeployment(1);
        SetupDeploymentFound(deployment);
        SetupAllStopsSucceed();

        await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        _dockerServiceMock.Verify(d => d.StopStackContainersAsync(
            deployment.EnvironmentId.Value.ToString(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReportsContainersStoppedCount()
    {
        var deployment = CreateRunningDeployment(1);
        SetupDeploymentFound(deployment);
        SetupAllStopsSucceed(5);

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Results[0].ContainersStopped.Should().Be(5);
    }

    #endregion

    #region Selective Stack Stop

    [Fact]
    public async Task Handle_WithSpecificStackNames_OnlyStopsNamedStacks()
    {
        var deployment = CreateRunningDeployment(3);
        SetupDeploymentFound(deployment);
        SetupAllStopsSucceed();

        var result = await _handler.Handle(
            CreateCommand(deployment, stackNames: new List<string> { "stack-1" }),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TotalStacks.Should().Be(1);
        result.Results.Should().HaveCount(1);
        result.Results[0].StackName.Should().Be("stack-1");

        _dockerServiceMock.Verify(d => d.StopStackContainersAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithMultipleSpecificStackNames_StopsAllNamed()
    {
        var deployment = CreateRunningDeployment(3);
        SetupDeploymentFound(deployment);
        SetupAllStopsSucceed();

        var result = await _handler.Handle(
            CreateCommand(deployment, stackNames: new List<string> { "stack-0", "stack-2" }),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TotalStacks.Should().Be(2);
        result.Results.Should().HaveCount(2);
        result.Results.Select(r => r.StackName).Should().BeEquivalentTo(new[] { "stack-0", "stack-2" });
    }

    [Fact]
    public async Task Handle_StackNamesCaseInsensitive()
    {
        var deployment = CreateRunningDeployment(2);
        SetupDeploymentFound(deployment);
        SetupAllStopsSucceed();

        var result = await _handler.Handle(
            CreateCommand(deployment, stackNames: new List<string> { "STACK-0" }),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TotalStacks.Should().Be(1);
        result.Results[0].StackName.Should().Be("stack-0");
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
        result.Message.Should().Contain("stack-0");
        result.Message.Should().Contain("stack-1");
    }

    [Fact]
    public async Task Handle_MixedKnownAndUnknownStackNames_ReturnsFailed()
    {
        var deployment = CreateRunningDeployment(2);
        SetupDeploymentFound(deployment);

        var result = await _handler.Handle(
            CreateCommand(deployment, stackNames: new List<string> { "stack-0", "bogus" }),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("bogus");
    }

    [Fact]
    public async Task Handle_EmptyStackNamesList_StopsAllStacks()
    {
        var deployment = CreateRunningDeployment(2);
        SetupDeploymentFound(deployment);
        SetupAllStopsSucceed();

        var result = await _handler.Handle(
            CreateCommand(deployment, stackNames: new List<string>()),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TotalStacks.Should().Be(2);
    }

    #endregion

    #region Validation

    [Fact]
    public async Task Handle_InvalidProductDeploymentId_ReturnsFailed()
    {
        var command = new StopProductContainersCommand(TestEnvironmentId, "not-a-guid");

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

        var command = new StopProductContainersCommand(
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
        result.Message.Should().Contain("Cannot stop containers");
        result.Message.Should().Contain("Deploying");
    }

    [Fact]
    public async Task Handle_DeploymentInRemovingStatus_ReturnsFailed()
    {
        var deployment = CreateRunningDeployment(1);
        deployment.StartRemoval();

        SetupDeploymentFound(deployment);

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Cannot stop containers");
        result.Message.Should().Contain("Removing");
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
            UserId.Create(),
            "test-deployment",
            stackConfigs,
            new Dictionary<string, string>());

        deployment.StartStack("stack-0", DeploymentId.NewId());
        deployment.FailStack("stack-0", "Critical failure");
        deployment.MarkAsFailed("All stacks failed");

        SetupDeploymentFound(deployment);

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Cannot stop containers");
        result.Message.Should().Contain("Failed");
    }

    #endregion

    #region Stacks Without DeploymentStackName

    [Fact]
    public async Task Handle_StackWithoutDeploymentStackName_SkipsAndReportsZeroContainers()
    {
        var deployment = CreatePartiallyRunningDeployment(3);
        SetupDeploymentFound(deployment);
        SetupAllStopsSucceed();

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Success.Should().BeTrue();
        // The third stack (index 2) was never started, so DeploymentStackName is null
        // But we only target stacks with DeploymentStackName != null when no specific names given
        result.Results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
    }

    #endregion

    #region Partial Failure (Docker stop fails)

    [Fact]
    public async Task Handle_OneStackStopFails_ContinuesWithRemainingStacks()
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
                return (IReadOnlyList<string>)new List<string> { "c1", "c2" };
            });

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Results.Should().HaveCount(3);
        result.Results[0].Success.Should().BeTrue();
        result.Results[1].Success.Should().BeFalse();
        result.Results[1].Error.Should().Contain("Docker daemon error");
        result.Results[2].Success.Should().BeTrue();
        result.Success.Should().BeFalse();
        result.StoppedStacks.Should().Be(2);
        result.FailedStacks.Should().Be(1);
    }

    [Fact]
    public async Task Handle_AllDockerStopsFail_ReturnsFailure()
    {
        var deployment = CreateRunningDeployment(2);
        SetupDeploymentFound(deployment);

        _dockerServiceMock
            .Setup(d => d.StopStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection refused"));

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.StoppedStacks.Should().Be(0);
        result.FailedStacks.Should().Be(2);
        result.Results.Should().AllSatisfy(r =>
        {
            r.Success.Should().BeFalse();
            r.Error.Should().Contain("Connection refused");
        });
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

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Message.Should().Contain("1 error");
        result.Message.Should().Contain("2/3");
    }

    #endregion

    #region State Machine Transitions

    [Fact]
    public async Task Handle_AllStopsSuceeded_TransitionsToStopped()
    {
        var deployment = CreateRunningDeployment(2);
        SetupDeploymentFound(deployment);
        SetupAllStopsSucceed();

        await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        deployment.Status.Should().Be(ProductDeploymentStatus.Stopped);
        _repositoryMock.Verify(r => r.Update(deployment), Times.Once);
        _repositoryMock.Verify(r => r.SaveChanges(), Times.Once);
    }

    [Fact]
    public async Task Handle_PartiallyRunningDeployment_TransitionsToStopped()
    {
        var deployment = CreatePartiallyRunningDeployment(3);
        deployment.Status.Should().Be(ProductDeploymentStatus.PartiallyRunning);
        SetupDeploymentFound(deployment);
        SetupAllStopsSucceed();

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Success.Should().BeTrue();
        deployment.Status.Should().Be(ProductDeploymentStatus.Stopped);
    }

    [Fact]
    public async Task Handle_PartialFailure_DoesNotChangeStatus()
    {
        var deployment = CreateRunningDeployment(2);
        var statusBefore = deployment.Status;
        SetupDeploymentFound(deployment);

        // First stack succeeds, second fails
        var callCount = 0;
        _dockerServiceMock
            .Setup(d => d.StopStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount > 1) throw new Exception("Failed");
                return new List<string> { "container1" };
            });

        await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        deployment.Status.Should().Be(statusBefore);
        _repositoryMock.Verify(r => r.Update(It.IsAny<ProductDeployment>()), Times.Never);
    }

    #endregion

    #region In-App Notification

    [Fact]
    public async Task Handle_Success_CreatesInAppNotification()
    {
        var deployment = CreateRunningDeployment(1);
        SetupDeploymentFound(deployment);
        SetupAllStopsSucceed();

        await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        _inAppNotificationMock.Verify(n => n.AddAsync(
            It.Is<global::ReadyStackGo.Application.Notifications.Notification>(notif =>
                notif.Type == global::ReadyStackGo.Application.Notifications.NotificationType.ProductDeploymentResult),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Failure_CreatesInAppNotification()
    {
        var deployment = CreateRunningDeployment(1);
        SetupDeploymentFound(deployment);

        _dockerServiceMock
            .Setup(d => d.StopStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Failed"));

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
        var handler = new StopProductContainersHandler(
            _repositoryMock.Object,
            _dockerServiceMock.Object,
            _loggerMock.Object);

        var deployment = CreateRunningDeployment(1);
        SetupDeploymentFound(deployment);
        SetupAllStopsSucceed();

        var act = () => handler.Handle(CreateCommand(deployment), CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    #endregion
}
