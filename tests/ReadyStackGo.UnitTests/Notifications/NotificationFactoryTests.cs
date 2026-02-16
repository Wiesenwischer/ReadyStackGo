using FluentAssertions;
using ReadyStackGo.Application.Notifications;

namespace ReadyStackGo.UnitTests.Notifications;

public class NotificationFactoryTests
{
    // --- CreateSyncResult ---

    [Fact]
    public void CreateSyncResult_Success_ReturnsSuccessSeverity()
    {
        var notification = NotificationFactory.CreateSyncResult(
            success: true, stacksLoaded: 5, sourcesSynced: 2,
            errors: [], warnings: []);

        notification.Severity.Should().Be(NotificationSeverity.Success);
        notification.Title.Should().Be("Source Sync Complete");
        notification.Message.Should().Contain("5 stack(s) loaded");
    }

    [Fact]
    public void CreateSyncResult_SuccessNoChanges_ReturnsInfoSeverity()
    {
        var notification = NotificationFactory.CreateSyncResult(
            success: true, stacksLoaded: 0, sourcesSynced: 1,
            errors: [], warnings: []);

        notification.Severity.Should().Be(NotificationSeverity.Info);
        notification.Message.Should().Contain("no changes");
    }

    [Fact]
    public void CreateSyncResult_SuccessWithWarnings_ReturnsWarningSeverity()
    {
        var notification = NotificationFactory.CreateSyncResult(
            success: true, stacksLoaded: 3, sourcesSynced: 1,
            errors: [], warnings: ["Some config ignored"]);

        notification.Severity.Should().Be(NotificationSeverity.Warning);
        notification.Title.Should().Contain("Warnings");
        notification.Message.Should().Contain("Some config ignored");
    }

    [Fact]
    public void CreateSyncResult_Failure_ReturnsErrorSeverity()
    {
        var notification = NotificationFactory.CreateSyncResult(
            success: false, stacksLoaded: 0, sourcesSynced: 0,
            errors: ["Connection refused"], warnings: []);

        notification.Severity.Should().Be(NotificationSeverity.Error);
        notification.Title.Should().Be("Source Sync Failed");
        notification.Message.Should().Contain("Connection refused");
    }

    [Fact]
    public void CreateSyncResult_WithSourceName_IncludesSourceInMessage()
    {
        var notification = NotificationFactory.CreateSyncResult(
            success: true, stacksLoaded: 2, sourcesSynced: 1,
            errors: [], warnings: [], sourceName: "my-registry");

        notification.Message.Should().Contain("'my-registry'");
    }

    [Fact]
    public void CreateSyncResult_WithoutSourceName_UsesSourceCount()
    {
        var notification = NotificationFactory.CreateSyncResult(
            success: true, stacksLoaded: 2, sourcesSynced: 3,
            errors: [], warnings: []);

        notification.Message.Should().Contain("3 source(s)");
    }

    [Fact]
    public void CreateSyncResult_SetsCorrectType()
    {
        var notification = NotificationFactory.CreateSyncResult(
            success: true, stacksLoaded: 1, sourcesSynced: 1,
            errors: [], warnings: []);

        notification.Type.Should().Be(NotificationType.SourceSyncResult);
    }

    [Fact]
    public void CreateSyncResult_SetsActionUrl()
    {
        var notification = NotificationFactory.CreateSyncResult(
            success: true, stacksLoaded: 1, sourcesSynced: 1,
            errors: [], warnings: []);

        notification.ActionUrl.Should().Be("/stack-sources");
        notification.ActionLabel.Should().Be("View Sources");
    }

    [Fact]
    public void CreateSyncResult_IncludesMetadata()
    {
        var notification = NotificationFactory.CreateSyncResult(
            success: true, stacksLoaded: 5, sourcesSynced: 2,
            errors: [], warnings: []);

        notification.Metadata["stacksLoaded"].Should().Be("5");
        notification.Metadata["sourcesSynced"].Should().Be("2");
    }

    // --- CreateDeploymentResult ---

    [Fact]
    public void CreateDeploymentResult_Success_ReturnsSuccessSeverity()
    {
        var notification = NotificationFactory.CreateDeploymentResult(
            success: true, operation: "deploy", stackName: "my-stack");

        notification.Severity.Should().Be(NotificationSeverity.Success);
        notification.Title.Should().Be("Deploy Successful");
        notification.Message.Should().Contain("successfully deployed");
    }

    [Fact]
    public void CreateDeploymentResult_Failure_ReturnsErrorSeverity()
    {
        var notification = NotificationFactory.CreateDeploymentResult(
            success: false, operation: "deploy", stackName: "my-stack");

        notification.Severity.Should().Be(NotificationSeverity.Error);
        notification.Title.Should().Be("Deploy Failed");
        notification.Message.Should().Contain("Failed to deploy");
    }

    [Fact]
    public void CreateDeploymentResult_Upgrade_FormatsCorrectly()
    {
        var notification = NotificationFactory.CreateDeploymentResult(
            success: true, operation: "upgrade", stackName: "web-app");

        notification.Title.Should().Be("Upgrade Successful");
        notification.Message.Should().Contain("successfully upgraded");
        notification.Message.Should().Contain("'web-app'");
    }

    [Fact]
    public void CreateDeploymentResult_Rollback_FormatsCorrectly()
    {
        var notification = NotificationFactory.CreateDeploymentResult(
            success: true, operation: "rollback", stackName: "web-app");

        notification.Title.Should().Be("Rollback Successful");
        notification.Message.Should().Contain("rolled back");
    }

    [Fact]
    public void CreateDeploymentResult_Remove_FormatsCorrectly()
    {
        var notification = NotificationFactory.CreateDeploymentResult(
            success: true, operation: "remove", stackName: "web-app");

        notification.Title.Should().Be("Remove Successful");
        notification.Message.Should().Contain("removed");
    }

    [Fact]
    public void CreateDeploymentResult_CustomMessage_OverridesDefault()
    {
        var notification = NotificationFactory.CreateDeploymentResult(
            success: true, operation: "deploy", stackName: "my-stack",
            message: "Custom message here");

        notification.Message.Should().Be("Custom message here");
    }

    [Fact]
    public void CreateDeploymentResult_WithDeploymentId_SetsActionUrl()
    {
        var notification = NotificationFactory.CreateDeploymentResult(
            success: true, operation: "deploy", stackName: "my-stack",
            deploymentId: "dep-123");

        notification.ActionUrl.Should().Be("/deployments/dep-123");
        notification.ActionLabel.Should().Be("View Deployment");
        notification.Metadata["deploymentId"].Should().Be("dep-123");
    }

    [Fact]
    public void CreateDeploymentResult_WithoutDeploymentId_NoActionUrl()
    {
        var notification = NotificationFactory.CreateDeploymentResult(
            success: true, operation: "remove", stackName: "my-stack");

        notification.ActionUrl.Should().BeNull();
        notification.ActionLabel.Should().BeNull();
    }

    [Fact]
    public void CreateDeploymentResult_SetsCorrectType()
    {
        var notification = NotificationFactory.CreateDeploymentResult(
            success: true, operation: "deploy", stackName: "my-stack");

        notification.Type.Should().Be(NotificationType.DeploymentResult);
    }

    [Fact]
    public void CreateDeploymentResult_IncludesMetadata()
    {
        var notification = NotificationFactory.CreateDeploymentResult(
            success: true, operation: "deploy", stackName: "my-stack");

        notification.Metadata["operation"].Should().Be("deploy");
        notification.Metadata["stackName"].Should().Be("my-stack");
    }
}
