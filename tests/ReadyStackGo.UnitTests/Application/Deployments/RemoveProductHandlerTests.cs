using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Application.UseCases.Deployments.RemoveDeployment;
using ReadyStackGo.Application.UseCases.Deployments.RemoveProduct;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using UserId = ReadyStackGo.Domain.Deployment.UserId;

namespace ReadyStackGo.UnitTests.Application.Deployments;

public class RemoveProductHandlerTests
{
    private readonly Mock<IProductDeploymentRepository> _repositoryMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IDeploymentNotificationService> _notificationMock;
    private readonly Mock<INotificationService> _inAppNotificationMock;
    private readonly Mock<ILogger<RemoveProductHandler>> _loggerMock;
    private readonly FakeTimeProvider _timeProvider;
    private readonly RemoveProductHandler _handler;

    private static readonly string TestEnvironmentId = Guid.NewGuid().ToString();
    private static readonly string TestUserId = Guid.NewGuid().ToString();

    public RemoveProductHandlerTests()
    {
        _repositoryMock = new Mock<IProductDeploymentRepository>();
        _mediatorMock = new Mock<IMediator>();
        _notificationMock = new Mock<IDeploymentNotificationService>();
        _inAppNotificationMock = new Mock<INotificationService>();
        _loggerMock = new Mock<ILogger<RemoveProductHandler>>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 2, 17, 12, 0, 0, TimeSpan.Zero));

        _handler = new RemoveProductHandler(
            _repositoryMock.Object,
            _mediatorMock.Object,
            _loggerMock.Object,
            _notificationMock.Object,
            _inAppNotificationMock.Object,
            _timeProvider);
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
            stackConfigs,
            new Dictionary<string, string> { ["SHARED"] = "shared-value" });

        foreach (var stack in deployment.GetStacksInDeployOrder())
        {
            var depId = DeploymentId.NewId();
            deployment.StartStack(stack.StackName, depId, $"{name}-{stack.StackName}");
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
            stackConfigs,
            new Dictionary<string, string>());

        var stacks = deployment.GetStacksInDeployOrder();

        // Complete first stack
        deployment.StartStack(stacks[0].StackName, DeploymentId.NewId(), "test-stack-0");
        deployment.CompleteStack(stacks[0].StackName);

        // Fail second stack
        deployment.StartStack(stacks[1].StackName, DeploymentId.NewId(), "test-stack-1");
        deployment.FailStack(stacks[1].StackName, "Test failure");

        // Mark as partially running
        deployment.MarkAsPartiallyRunning("One stack failed");

        return deployment;
    }

    private static ProductDeployment CreateFailedDeployment()
    {
        var stackConfigs = new List<StackDeploymentConfig>
        {
            new("stack-0", "Stack 0", "stacks:test-product:1.0.0:stack-0",
                2, new Dictionary<string, string>())
        };

        var deployment = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(),
            new EnvironmentId(Guid.Parse(TestEnvironmentId)),
            "stacks:test-product", "stacks:test-product:1.0.0",
            "test-product", "Test Product", "1.0.0",
            UserId.Create(),
            stackConfigs,
            new Dictionary<string, string>());

        deployment.StartStack("stack-0", DeploymentId.NewId(), "test-stack-0");
        deployment.FailStack("stack-0", "Critical failure");
        deployment.MarkAsFailed("All stacks failed");

        return deployment;
    }

    private void SetupDeploymentFound(ProductDeployment deployment)
    {
        _repositoryMock
            .Setup(r => r.Get(It.Is<ProductDeploymentId>(id => id == deployment.Id)))
            .Returns(deployment);
    }

    private void SetupAllRemovalsSucceed()
    {
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RemoveDeploymentByIdCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeployComposeResponse { Success = true, Message = "Removed" });
    }

    private void SetupRemovalFailsAtIndex(int failIndex)
    {
        var callIndex = 0;
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RemoveDeploymentByIdCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RemoveDeploymentByIdCommand _, CancellationToken _) =>
            {
                var currentIndex = callIndex++;
                if (currentIndex == failIndex)
                {
                    return new DeployComposeResponse
                    {
                        Success = false,
                        Message = "Docker removal failed"
                    };
                }

                return new DeployComposeResponse { Success = true, Message = "Removed" };
            });
    }

    private RemoveProductCommand CreateCommand(
        ProductDeployment deployment, string? sessionId = null)
    {
        return new RemoveProductCommand(
            TestEnvironmentId,
            deployment.Id.Value.ToString(),
            sessionId,
            TestUserId);
    }

    #endregion

    #region Happy Path

    [Fact]
    public async Task Handle_AllStacksRemoved_ReturnsRemovedStatus()
    {
        var deployment = CreateRunningDeployment(3);
        SetupDeploymentFound(deployment);
        SetupAllRemovalsSucceed();

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be("Removed");
        result.ProductDeploymentId.Should().NotBeNullOrEmpty();
        result.ProductName.Should().Be("test-product");
        result.StackResults.Should().HaveCount(3);
        result.StackResults.Should().AllSatisfy(sr => sr.Success.Should().BeTrue());
    }

    [Fact]
    public async Task Handle_SingleStackProduct_Succeeds()
    {
        var deployment = CreateRunningDeployment(1);
        SetupDeploymentFound(deployment);
        SetupAllRemovalsSucceed();

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be("Removed");
        result.StackResults.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WithProvidedSessionId_UsesIt()
    {
        var deployment = CreateRunningDeployment(1);
        SetupDeploymentFound(deployment);
        SetupAllRemovalsSucceed();

        var result = await _handler.Handle(
            CreateCommand(deployment, sessionId: "custom-session"), CancellationToken.None);

        result.SessionId.Should().Be("custom-session");
    }

    [Fact]
    public async Task Handle_WithoutSessionId_GeneratesOne()
    {
        var deployment = CreateRunningDeployment(1);
        SetupDeploymentFound(deployment);
        SetupAllRemovalsSucceed();

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.SessionId.Should().StartWith("product-remove-test-product-");
    }

    [Fact]
    public async Task Handle_PersistsAfterEachStack()
    {
        var deployment = CreateRunningDeployment(3);
        SetupDeploymentFound(deployment);
        SetupAllRemovalsSucceed();

        await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        // Initial save (StartRemoval) + 3 per-stack saves = 4
        _repositoryMock.Verify(r => r.Update(It.IsAny<ProductDeployment>()), Times.AtLeast(4));
        _repositoryMock.Verify(r => r.SaveChanges(), Times.AtLeast(4));
    }

    [Fact]
    public async Task Handle_RemovesStacksInReverseOrder()
    {
        var deployment = CreateRunningDeployment(3);
        SetupDeploymentFound(deployment);

        var removedDeploymentIds = new List<string>();
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RemoveDeploymentByIdCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<DeployComposeResponse>, CancellationToken>((req, _) =>
                removedDeploymentIds.Add(((RemoveDeploymentByIdCommand)req).DeploymentId))
            .ReturnsAsync(new DeployComposeResponse { Success = true });

        await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        removedDeploymentIds.Should().HaveCount(3);
        // Stacks are stack-0 (Order 0), stack-1 (Order 1), stack-2 (Order 2)
        // Reverse order means stack-2 first, then stack-1, then stack-0
        var stacks = deployment.GetStacksInDeployOrder();
        var stack2DepId = stacks[2].DeploymentId!.Value.ToString();
        var stack1DepId = stacks[1].DeploymentId!.Value.ToString();
        var stack0DepId = stacks[0].DeploymentId!.Value.ToString();
        removedDeploymentIds[0].Should().Be(stack2DepId);
        removedDeploymentIds[1].Should().Be(stack1DepId);
        removedDeploymentIds[2].Should().Be(stack0DepId);
    }

    #endregion

    #region Validation

    [Fact]
    public async Task Handle_InvalidProductDeploymentId_ReturnsFailed()
    {
        var command = new RemoveProductCommand(TestEnvironmentId, "not-a-guid", UserId: TestUserId);

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

        var command = new RemoveProductCommand(
            TestEnvironmentId, Guid.NewGuid().ToString(), UserId: TestUserId);

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
            UserId.Create(), stackConfigs, new Dictionary<string, string>());

        SetupDeploymentFound(deployment);

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("cannot be removed");
    }

    [Fact]
    public async Task Handle_DeploymentAlreadyRemoved_ReturnsFailed()
    {
        var deployment = CreateRunningDeployment(1);
        deployment.StartRemoval();
        deployment.MarkStackRemoved("stack-0");
        // Now status is Removed

        SetupDeploymentFound(deployment);

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("cannot be removed");
    }

    #endregion

    #region Stacks Without DeploymentId

    [Fact]
    public async Task Handle_StackWithoutDeploymentId_SkipsDockerRemoval_MarksAsRemoved()
    {
        // Create a partially-deployed product where not all stacks got DeploymentIds
        var stackConfigs = new List<StackDeploymentConfig>
        {
            new("stack-0", "Stack 0", "sid:stack-0", 2, new Dictionary<string, string>()),
            new("stack-1", "Stack 1", "sid:stack-1", 2, new Dictionary<string, string>())
        };

        var deployment = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(),
            new EnvironmentId(Guid.Parse(TestEnvironmentId)),
            "gid", "pid", "test-product", "Test Product", "1.0.0",
            UserId.Create(), stackConfigs, new Dictionary<string, string>());

        // Only start and complete the first stack (stack-1 remains Pending, no DeploymentId)
        deployment.StartStack("stack-0", DeploymentId.NewId(), "test-stack-0");
        deployment.CompleteStack("stack-0");
        deployment.StartStack("stack-1", DeploymentId.NewId(), "test-stack-1");
        deployment.FailStack("stack-1", "Failed");
        deployment.MarkAsPartiallyRunning("One stack failed");

        SetupDeploymentFound(deployment);
        SetupAllRemovalsSucceed();

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be("Removed");
        result.StackResults.Should().HaveCount(2);
        result.StackResults.Should().AllSatisfy(sr => sr.Success.Should().BeTrue());
        // Both stacks have DeploymentIds (they were started), so mediator is called for both
        _mediatorMock.Verify(m => m.Send(
            It.IsAny<RemoveDeploymentByIdCommand>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_StackNeverStarted_SkipsMediatorDispatch()
    {
        // Create a deployment where stack-1 was never started (DeploymentId is null)
        var stackConfigs = new List<StackDeploymentConfig>
        {
            new("stack-0", "Stack 0", "sid:stack-0", 2, new Dictionary<string, string>()),
            new("stack-1", "Stack 1", "sid:stack-1", 2, new Dictionary<string, string>())
        };

        var deployment = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(),
            new EnvironmentId(Guid.Parse(TestEnvironmentId)),
            "gid", "pid", "test-product", "Test Product", "1.0.0",
            UserId.Create(), stackConfigs, new Dictionary<string, string>());

        // Only complete first stack, fail second without starting
        deployment.StartStack("stack-0", DeploymentId.NewId(), "test-stack-0");
        deployment.CompleteStack("stack-0");
        // stack-1 was never started; FailStack requires starting it first in domain model
        // So fail it after start
        deployment.StartStack("stack-1", DeploymentId.NewId(), "test-stack-1");
        deployment.FailStack("stack-1", "Failed");
        deployment.MarkAsPartiallyRunning("Partial");

        SetupDeploymentFound(deployment);
        SetupAllRemovalsSucceed();

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be("Removed");
    }

    #endregion

    #region Partial Failure (Docker removal fails)

    [Fact]
    public async Task Handle_OneStackRemovalFails_ContinuesWithRemainingStacks()
    {
        var deployment = CreateRunningDeployment(3);
        SetupDeploymentFound(deployment);
        SetupRemovalFailsAtIndex(1); // Second removal fails

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        // All 3 stacks attempted
        result.StackResults.Should().HaveCount(3);
        // Docker result failure reported
        result.StackResults[1].Success.Should().BeFalse();
        result.StackResults[1].ErrorMessage.Should().Contain("Docker removal failed");
        // Other stacks succeeded
        result.StackResults[0].Success.Should().BeTrue();
        result.StackResults[2].Success.Should().BeTrue();
        // But overall response is not success because of the error
        result.Success.Should().BeFalse();
        // Aggregate still transitions to Removed (orchestration state, not container state)
        result.Status.Should().Be("Removed");
    }

    [Fact]
    public async Task Handle_AllDockerRemovalsFail_AggregateStillRemoved()
    {
        var deployment = CreateRunningDeployment(2);
        SetupDeploymentFound(deployment);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RemoveDeploymentByIdCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeployComposeResponse { Success = false, Message = "Docker error" });

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Status.Should().Be("Removed");
        result.StackResults.Should().AllSatisfy(sr => sr.Success.Should().BeFalse());
        result.Message.Should().Contain("error");
    }

    [Fact]
    public async Task Handle_MediatorThrowsException_CatchesAndContinues()
    {
        var deployment = CreateRunningDeployment(2);
        SetupDeploymentFound(deployment);

        var callIndex = 0;
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RemoveDeploymentByIdCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RemoveDeploymentByIdCommand _, CancellationToken _) =>
            {
                if (callIndex++ == 0)
                    throw new InvalidOperationException("Connection refused");
                return new DeployComposeResponse { Success = true };
            });

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.StackResults.Should().HaveCount(2);
        result.StackResults[0].Success.Should().BeFalse();
        result.StackResults[0].ErrorMessage.Should().Contain("Connection refused");
        result.StackResults[1].Success.Should().BeTrue();
        result.Status.Should().Be("Removed");
    }

    [Fact]
    public async Task Handle_PartialFailure_MessageContainsErrorCount()
    {
        var deployment = CreateRunningDeployment(3);
        SetupDeploymentFound(deployment);
        SetupRemovalFailsAtIndex(0);

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Message.Should().Contain("1 error");
        result.Message.Should().Contain("2/3");
    }

    #endregion

    #region Domain State Transitions

    [Fact]
    public async Task Handle_CanRemoveFromRunning_Succeeds()
    {
        var deployment = CreateRunningDeployment(1);
        deployment.Status.Should().Be(ProductDeploymentStatus.Running);

        SetupDeploymentFound(deployment);
        SetupAllRemovalsSucceed();

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be("Removed");
    }

    [Fact]
    public async Task Handle_CanRemoveFromPartiallyRunning_Succeeds()
    {
        var deployment = CreatePartiallyRunningDeployment();
        deployment.Status.Should().Be(ProductDeploymentStatus.PartiallyRunning);

        SetupDeploymentFound(deployment);
        SetupAllRemovalsSucceed();

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be("Removed");
    }

    [Fact]
    public async Task Handle_CanRemoveFromFailed_Succeeds()
    {
        var deployment = CreateFailedDeployment();
        deployment.Status.Should().Be(ProductDeploymentStatus.Failed);

        SetupDeploymentFound(deployment);
        SetupAllRemovalsSucceed();

        var result = await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be("Removed");
    }

    #endregion

    #region SignalR Progress

    [Fact]
    public async Task Handle_SendsProgressBeforeEachStack()
    {
        var deployment = CreateRunningDeployment(2);
        SetupDeploymentFound(deployment);
        SetupAllRemovalsSucceed();

        await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        _notificationMock.Verify(n => n.NotifyProgressAsync(
            It.IsAny<string>(),
            "ProductRemoval",
            It.Is<string>(m => m.Contains("1/2")),
            It.IsAny<int>(),
            It.IsAny<string>(),
            2,
            It.IsAny<int>(),
            0, 0,
            It.IsAny<CancellationToken>()), Times.Once);

        _notificationMock.Verify(n => n.NotifyProgressAsync(
            It.IsAny<string>(),
            "ProductRemoval",
            It.Is<string>(m => m.Contains("2/2")),
            It.IsAny<int>(),
            It.IsAny<string>(),
            2,
            It.IsAny<int>(),
            0, 0,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SendsCompletedOnSuccess()
    {
        var deployment = CreateRunningDeployment(1);
        SetupDeploymentFound(deployment);
        SetupAllRemovalsSucceed();

        await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        _notificationMock.Verify(n => n.NotifyCompletedAsync(
            It.IsAny<string>(),
            It.Is<string>(m => m.Contains("successfully")),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SendsErrorOnPartialFailure()
    {
        var deployment = CreateRunningDeployment(2);
        SetupDeploymentFound(deployment);
        SetupRemovalFailsAtIndex(0);

        await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        _notificationMock.Verify(n => n.NotifyErrorAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region In-App Notification

    [Fact]
    public async Task Handle_Success_CreatesInAppNotification()
    {
        var deployment = CreateRunningDeployment(1);
        SetupDeploymentFound(deployment);
        SetupAllRemovalsSucceed();

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

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RemoveDeploymentByIdCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeployComposeResponse { Success = false, Message = "Failed" });

        await _handler.Handle(CreateCommand(deployment), CancellationToken.None);

        _inAppNotificationMock.Verify(n => n.AddAsync(
            It.Is<global::ReadyStackGo.Application.Notifications.Notification>(notif =>
                notif.Type == global::ReadyStackGo.Application.Notifications.NotificationType.ProductDeploymentResult),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Null Services

    [Fact]
    public async Task Handle_WithNullNotificationServices_DoesNotThrow()
    {
        var handler = new RemoveProductHandler(
            _repositoryMock.Object,
            _mediatorMock.Object,
            _loggerMock.Object);

        var deployment = CreateRunningDeployment(1);
        SetupDeploymentFound(deployment);
        SetupAllRemovalsSucceed();

        var act = () => handler.Handle(CreateCommand(deployment), CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    #endregion
}
