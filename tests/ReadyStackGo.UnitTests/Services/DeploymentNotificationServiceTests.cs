using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Api.Hubs;
using ReadyStackGo.Api.Services;

namespace ReadyStackGo.UnitTests.Services;

/// <summary>
/// Unit tests for DeploymentNotificationService.
/// Verifies SignalR notifications are sent correctly.
/// </summary>
public class DeploymentNotificationServiceTests
{
    private readonly Mock<IHubContext<DeploymentHub>> _hubContextMock;
    private readonly Mock<IHubClients> _hubClientsMock;
    private readonly Mock<IClientProxy> _clientProxyMock;
    private readonly Mock<ILogger<DeploymentNotificationService>> _loggerMock;
    private readonly DeploymentNotificationService _service;
    private readonly List<object[]> _capturedMessages = new();

    public DeploymentNotificationServiceTests()
    {
        _hubContextMock = new Mock<IHubContext<DeploymentHub>>();
        _hubClientsMock = new Mock<IHubClients>();
        _clientProxyMock = new Mock<IClientProxy>();
        _loggerMock = new Mock<ILogger<DeploymentNotificationService>>();

        _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
        _hubClientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxyMock.Object);

        // Capture all sent messages
        _clientProxyMock
            .Setup(c => c.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, object[], CancellationToken>((method, args, ct) => _capturedMessages.Add(args))
            .Returns(Task.CompletedTask);

        _service = new DeploymentNotificationService(
            _hubContextMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task NotifyProgressAsync_SendsToDeploymentGroup()
    {
        // Arrange
        var deploymentId = "test-deployment-123";
        var phase = "Pulling";
        var message = "Pulling image nginx:latest";
        var progressPercent = 25;
        var currentService = "web";
        var totalServices = 4;
        var completedServices = 1;

        // Act
        await _service.NotifyProgressAsync(
            deploymentId,
            phase,
            message,
            progressPercent,
            currentService,
            totalServices,
            completedServices);

        // Assert - Verify sent to deployment-specific group
        _hubClientsMock.Verify(
            c => c.Group($"deployment:{deploymentId}"),
            Times.Once,
            "Should send to deployment-specific group");

        // Assert - Verify sent to all deployments group
        _hubClientsMock.Verify(
            c => c.Group("deployments:all"),
            Times.Once,
            "Should also send to all deployments group");

        // Assert - Verify message content
        _capturedMessages.Should().HaveCount(2);
        var update = _capturedMessages[0][0] as DeploymentProgressUpdate;
        update.Should().NotBeNull();
        update!.DeploymentId.Should().Be(deploymentId);
        update.Phase.Should().Be(phase);
        update.Message.Should().Be(message);
        update.ProgressPercent.Should().Be(progressPercent);
        update.CurrentService.Should().Be(currentService);
        update.TotalServices.Should().Be(totalServices);
        update.CompletedServices.Should().Be(completedServices);
        update.IsComplete.Should().BeFalse();
        update.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task NotifyCompletedAsync_SendsCompletionMessage()
    {
        // Arrange
        var deploymentId = "test-deployment-456";
        var message = "Successfully deployed mystack";
        var totalServices = 3;

        // Act
        await _service.NotifyCompletedAsync(
            deploymentId,
            message,
            totalServices);

        // Assert
        _capturedMessages.Should().HaveCount(2);
        var update = _capturedMessages[0][0] as DeploymentProgressUpdate;
        update.Should().NotBeNull();
        update!.DeploymentId.Should().Be(deploymentId);
        update.Phase.Should().Be("Complete");
        update.Message.Should().Be(message);
        update.ProgressPercent.Should().Be(100);
        update.TotalServices.Should().Be(totalServices);
        update.CompletedServices.Should().Be(totalServices);
        update.IsComplete.Should().BeTrue();
        update.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task NotifyErrorAsync_SendsErrorMessage()
    {
        // Arrange
        var deploymentId = "test-deployment-789";
        var errorMessage = "Failed to pull image: unauthorized";
        var currentService = "api";
        var totalServices = 5;
        var completedServices = 2;

        // Act
        await _service.NotifyErrorAsync(
            deploymentId,
            errorMessage,
            currentService,
            totalServices,
            completedServices);

        // Assert
        _capturedMessages.Should().HaveCount(2);
        var update = _capturedMessages[0][0] as DeploymentProgressUpdate;
        update.Should().NotBeNull();
        update!.DeploymentId.Should().Be(deploymentId);
        update.Phase.Should().Be("Error");
        update.Message.Should().Be("Deployment failed");
        update.ErrorMessage.Should().Be(errorMessage);
        update.CurrentService.Should().Be(currentService);
        update.TotalServices.Should().Be(totalServices);
        update.CompletedServices.Should().Be(completedServices);
        update.IsComplete.Should().BeTrue();
        update.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task NotifyErrorAsync_CalculatesProgressPercent()
    {
        // Arrange
        var deploymentId = "test-deployment-calc";
        var errorMessage = "Container failed to start";
        var totalServices = 10;
        var completedServices = 4;

        // Act
        await _service.NotifyErrorAsync(
            deploymentId,
            errorMessage,
            null,
            totalServices,
            completedServices);

        // Assert - Progress should be 40% (4/10 * 100)
        _capturedMessages.Should().HaveCount(2);
        var update = _capturedMessages[0][0] as DeploymentProgressUpdate;
        update.Should().NotBeNull();
        update!.ProgressPercent.Should().Be(40);
    }

    [Fact]
    public async Task NotifyErrorAsync_ZeroTotalServices_ReturnsZeroProgress()
    {
        // Arrange
        var deploymentId = "test-deployment-zero";
        var errorMessage = "Validation failed";

        // Act
        await _service.NotifyErrorAsync(
            deploymentId,
            errorMessage,
            null,
            totalServices: 0,
            completedServices: 0);

        // Assert - Progress should be 0% when totalServices is 0
        _capturedMessages.Should().HaveCount(2);
        var update = _capturedMessages[0][0] as DeploymentProgressUpdate;
        update.Should().NotBeNull();
        update!.ProgressPercent.Should().Be(0);
    }

    [Fact]
    public async Task NotifyProgressAsync_NullCurrentService_SetsNull()
    {
        // Arrange
        var deploymentId = "test-deployment-null-svc";

        // Act
        await _service.NotifyProgressAsync(
            deploymentId,
            "Initializing",
            "Starting deployment",
            5,
            currentService: null,
            totalServices: 0,
            completedServices: 0);

        // Assert
        _capturedMessages.Should().HaveCount(2);
        var update = _capturedMessages[0][0] as DeploymentProgressUpdate;
        update.Should().NotBeNull();
        update!.CurrentService.Should().BeNull();
    }
}
