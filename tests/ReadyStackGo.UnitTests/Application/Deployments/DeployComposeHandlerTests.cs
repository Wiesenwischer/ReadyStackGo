using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Application.UseCases.Deployments.DeployCompose;

namespace ReadyStackGo.UnitTests.Application.Deployments;

/// <summary>
/// Unit tests for DeployComposeHandler.
/// Verifies progress notifications are sent during deployment.
/// </summary>
public class DeployComposeHandlerTests
{
    private readonly Mock<IDeploymentService> _deploymentServiceMock;
    private readonly Mock<IDeploymentNotificationService> _notificationServiceMock;
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly DeployComposeHandler _handler;

    public DeployComposeHandlerTests()
    {
        _deploymentServiceMock = new Mock<IDeploymentService>();
        _notificationServiceMock = new Mock<IDeploymentNotificationService>();
        _fakeTimeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 6, 15, 10, 30, 45, 123, TimeSpan.Zero));

        _handler = new DeployComposeHandler(
            _deploymentServiceMock.Object,
            _notificationServiceMock.Object,
            _fakeTimeProvider);
    }

    [Fact]
    public async Task Handle_SuccessfulDeployment_SendsCompletionNotification()
    {
        // Arrange
        var command = new DeployComposeCommand(
            EnvironmentId: "env-123",
            StackName: "test-stack",
            YamlContent: "version: '3'\nservices:\n  web:\n    image: nginx",
            Variables: new Dictionary<string, string>());

        var response = new DeployComposeResponse
        {
            Success = true,
            Message = "Successfully deployed test-stack",
            DeploymentId = "dep-456",
            StackName = "test-stack",
            Services = new List<DeployedServiceInfo>
            {
                new() { ServiceName = "web", Status = "running" }
            }
        };

        _deploymentServiceMock
            .Setup(s => s.DeployComposeAsync(
                "env-123",
                It.IsAny<DeployComposeRequest>(),
                It.IsAny<DeploymentServiceProgressCallback>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.DeploymentSessionId.Should().Be("test-stack-20240615103045123");

        _notificationServiceMock.Verify(
            n => n.NotifyCompletedAsync(
                "test-stack-20240615103045123",
                It.IsAny<string>(),
                1,
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should send completion notification with correct session ID");
    }

    [Fact]
    public async Task Handle_FailedDeployment_SendsErrorNotification()
    {
        // Arrange
        var command = new DeployComposeCommand(
            EnvironmentId: "env-123",
            StackName: "failing-stack",
            YamlContent: "version: '3'\nservices:\n  web:\n    image: invalid",
            Variables: new Dictionary<string, string>());

        var response = new DeployComposeResponse
        {
            Success = false,
            Message = "Failed to pull image",
            Errors = new List<string> { "Image not found" }
        };

        _deploymentServiceMock
            .Setup(s => s.DeployComposeAsync(
                "env-123",
                It.IsAny<DeployComposeRequest>(),
                It.IsAny<DeploymentServiceProgressCallback>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.DeploymentSessionId.Should().Be("failing-stack-20240615103045123");

        _notificationServiceMock.Verify(
            n => n.NotifyErrorAsync(
                "failing-stack-20240615103045123",
                "Failed to pull image",
                null,
                0,
                0,
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should send error notification");
    }

    [Fact]
    public async Task Handle_WithProgressCallback_InvokesProgressNotifications()
    {
        // Arrange
        var command = new DeployComposeCommand(
            EnvironmentId: "env-123",
            StackName: "progress-stack",
            YamlContent: "version: '3'\nservices:\n  web:\n    image: nginx",
            Variables: new Dictionary<string, string>());

        DeploymentServiceProgressCallback? capturedCallback = null;

        _deploymentServiceMock
            .Setup(s => s.DeployComposeAsync(
                "env-123",
                It.IsAny<DeployComposeRequest>(),
                It.IsAny<DeploymentServiceProgressCallback>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, DeployComposeRequest, DeploymentServiceProgressCallback?, CancellationToken>(
                (envId, req, callback, ct) =>
                {
                    capturedCallback = callback;
                    // Simulate progress callbacks
                    callback?.Invoke("Pulling", "Pulling nginx:latest", 50, "web", 1, 0).Wait();
                })
            .ReturnsAsync(new DeployComposeResponse
            {
                Success = true,
                Message = "Deployed",
                Services = new List<DeployedServiceInfo>()
            });

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedCallback.Should().NotBeNull("Progress callback should be provided to service");

        _notificationServiceMock.Verify(
            n => n.NotifyProgressAsync(
                "progress-stack-20240615103045123",
                "Pulling",
                "Pulling nginx:latest",
                50,
                "web",
                1,
                0,
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should forward progress updates to notification service");
    }

    [Fact]
    public async Task Handle_WithoutNotificationService_StillSucceeds()
    {
        // Arrange - Handler without notification service
        var handlerWithoutNotifications = new DeployComposeHandler(
            _deploymentServiceMock.Object,
            null,
            _fakeTimeProvider);

        var command = new DeployComposeCommand(
            EnvironmentId: "env-123",
            StackName: "no-notify-stack",
            YamlContent: "version: '3'\nservices:\n  web:\n    image: nginx",
            Variables: new Dictionary<string, string>());

        _deploymentServiceMock
            .Setup(s => s.DeployComposeAsync(
                "env-123",
                It.IsAny<DeployComposeRequest>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeployComposeResponse
            {
                Success = true,
                Message = "Deployed"
            });

        // Act
        var result = await handlerWithoutNotifications.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.DeploymentSessionId.Should().Be("no-notify-stack-20240615103045123");
    }

    [Fact]
    public async Task Handle_GeneratesUniqueSessionId_WhenTimeAdvances()
    {
        // Arrange
        var command = new DeployComposeCommand(
            EnvironmentId: "env-123",
            StackName: "unique-id-stack",
            YamlContent: "version: '3'\nservices:\n  web:\n    image: nginx",
            Variables: new Dictionary<string, string>());

        // Return new instances each time to avoid the same object being reused
        _deploymentServiceMock
            .Setup(s => s.DeployComposeAsync(
                It.IsAny<string>(),
                It.IsAny<DeployComposeRequest>(),
                It.IsAny<DeploymentServiceProgressCallback>(),
                It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(new DeployComposeResponse { Success = true }));

        // Act
        var result1 = await _handler.Handle(command, CancellationToken.None);

        // Advance time by 100ms - deterministic, no flaky Task.Delay needed
        _fakeTimeProvider.Advance(TimeSpan.FromMilliseconds(100));

        var result2 = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result1.DeploymentSessionId.Should().Be("unique-id-stack-20240615103045123");
        result2.DeploymentSessionId.Should().Be("unique-id-stack-20240615103045223");
        result1.DeploymentSessionId.Should().NotBe(result2.DeploymentSessionId,
            "Each deployment should have a unique session ID");
    }

    [Fact]
    public async Task Handle_SessionId_ContainsStackNameAndTimestamp()
    {
        // Arrange
        var specificTime = new DateTimeOffset(2025, 12, 31, 23, 59, 59, 999, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(specificTime);
        var handler = new DeployComposeHandler(
            _deploymentServiceMock.Object,
            _notificationServiceMock.Object,
            timeProvider);

        var command = new DeployComposeCommand(
            EnvironmentId: "env-123",
            StackName: "my-app",
            YamlContent: "version: '3'\nservices:\n  web:\n    image: nginx",
            Variables: new Dictionary<string, string>());

        _deploymentServiceMock
            .Setup(s => s.DeployComposeAsync(
                It.IsAny<string>(),
                It.IsAny<DeployComposeRequest>(),
                It.IsAny<DeploymentServiceProgressCallback>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeployComposeResponse { Success = true });

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert - Format: {stackName}-{yyyyMMddHHmmssfff}
        result.DeploymentSessionId.Should().Be("my-app-20251231235959999");
    }
}
