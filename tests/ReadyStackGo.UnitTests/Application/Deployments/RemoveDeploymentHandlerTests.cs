using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Notifications;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Application.UseCases.Deployments.RemoveDeployment;
using Xunit;

namespace ReadyStackGo.UnitTests.Application.Deployments;

/// <summary>
/// Verifies that per-stack in-app notifications are suppressed when a stack removal is part of
/// a product removal (SuppressNotification=true) — the parent RemoveProductHandler emits a
/// single aggregated product notification, so per-stack ones would just spam the bell.
/// </summary>
public class RemoveDeploymentHandlerTests
{
    private readonly Mock<IDeploymentService> _deploymentService = new();
    private readonly Mock<IDeploymentNotificationService> _signalR = new();
    private readonly Mock<INotificationService> _inApp = new();

    private RemoveDeploymentByIdHandler CreateSut() => new(
        _deploymentService.Object, _signalR.Object,
        new Mock<ILogger<RemoveDeploymentByIdHandler>>().Object, _inApp.Object);

    private void SetupRemove() =>
        _deploymentService
            .Setup(s => s.RemoveDeploymentByIdAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new DeployComposeResponse { Success = true, StackName = "ams-project-memo", Message = "removed" });

    [Fact]
    public async Task SuppressNotification_True_DoesNotCreateInAppNotification()
    {
        SetupRemove();
        var cmd = new RemoveDeploymentByIdCommand("env", "dep", SessionId: null, SuppressNotification: true);

        await CreateSut().Handle(cmd, CancellationToken.None);

        _inApp.Verify(n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never,
            "product removal suppresses per-stack notifications to avoid bell spam");
    }

    [Fact]
    public async Task SuppressNotification_False_CreatesOneInAppNotification()
    {
        SetupRemove();
        var cmd = new RemoveDeploymentByIdCommand("env", "dep", SessionId: null, SuppressNotification: false);

        await CreateSut().Handle(cmd, CancellationToken.None);

        _inApp.Verify(n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once,
            "a standalone single-stack removal still notifies once");
    }
}
