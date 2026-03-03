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

        notification.ActionUrl.Should().Be("/settings/stack-sources");
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

        notification.ActionUrl.Should().Be("/deployments/my-stack");
        notification.ActionLabel.Should().Be("View Deployment");
        notification.Metadata["deploymentId"].Should().Be("dep-123");
    }

    [Fact]
    public void CreateDeploymentResult_WithDeploymentId_UsesStackNameInActionUrl()
    {
        var notification = NotificationFactory.CreateDeploymentResult(
            success: true, operation: "deploy", stackName: "my stack/special",
            deploymentId: "dep-123");

        notification.ActionUrl.Should().Be("/deployments/my%20stack%2Fspecial");
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

    // --- CreateHealthChangeNotification ---

    [Fact]
    public void CreateHealthChangeNotification_Unhealthy_ReturnsErrorSeverity()
    {
        var notification = NotificationFactory.CreateHealthChangeNotification(
            stackName: "web-app", serviceName: "api",
            previousStatus: "Healthy", currentStatus: "Unhealthy");

        notification.Severity.Should().Be(NotificationSeverity.Error);
        notification.Title.Should().Be("Service Health Changed");
        notification.Message.Should().Contain("Healthy").And.Contain("Unhealthy");
    }

    [Fact]
    public void CreateHealthChangeNotification_NotFound_ReturnsErrorSeverity()
    {
        var notification = NotificationFactory.CreateHealthChangeNotification(
            stackName: "web-app", serviceName: "api",
            previousStatus: "Healthy", currentStatus: "NotFound");

        notification.Severity.Should().Be(NotificationSeverity.Error);
    }

    [Fact]
    public void CreateHealthChangeNotification_Degraded_ReturnsWarningSeverity()
    {
        var notification = NotificationFactory.CreateHealthChangeNotification(
            stackName: "web-app", serviceName: "api",
            previousStatus: "Healthy", currentStatus: "Degraded");

        notification.Severity.Should().Be(NotificationSeverity.Warning);
    }

    [Fact]
    public void CreateHealthChangeNotification_Recovery_ReturnsInfoSeverityAndRecoveryTitle()
    {
        var notification = NotificationFactory.CreateHealthChangeNotification(
            stackName: "web-app", serviceName: "api",
            previousStatus: "Unhealthy", currentStatus: "Healthy");

        notification.Severity.Should().Be(NotificationSeverity.Info);
        notification.Title.Should().Be("Service Recovered");
    }

    [Fact]
    public void CreateHealthChangeNotification_SetsCorrectType()
    {
        var notification = NotificationFactory.CreateHealthChangeNotification(
            stackName: "web-app", serviceName: "api",
            previousStatus: "Healthy", currentStatus: "Unhealthy");

        notification.Type.Should().Be(NotificationType.HealthChange);
    }

    [Fact]
    public void CreateHealthChangeNotification_WithDeploymentId_SetsActionUrl()
    {
        var notification = NotificationFactory.CreateHealthChangeNotification(
            stackName: "web-app", serviceName: "api",
            previousStatus: "Healthy", currentStatus: "Unhealthy",
            deploymentId: "dep-123");

        notification.ActionUrl.Should().Be("/deployments/web-app");
        notification.ActionLabel.Should().Be("View Deployment");
        notification.Metadata["deploymentId"].Should().Be("dep-123");
    }

    [Fact]
    public void CreateHealthChangeNotification_WithoutDeploymentId_FallsBackToHealthUrl()
    {
        var notification = NotificationFactory.CreateHealthChangeNotification(
            stackName: "web-app", serviceName: "api",
            previousStatus: "Healthy", currentStatus: "Unhealthy");

        notification.ActionUrl.Should().Be("/health");
        notification.ActionLabel.Should().Be("View Health");
    }

    [Fact]
    public void CreateHealthChangeNotification_IncludesMetadata()
    {
        var notification = NotificationFactory.CreateHealthChangeNotification(
            stackName: "web-app", serviceName: "api",
            previousStatus: "Healthy", currentStatus: "Unhealthy",
            deploymentId: "dep-123");

        notification.Metadata["serviceKey"].Should().Be("dep-123:api");
        notification.Metadata["stackName"].Should().Be("web-app");
        notification.Metadata["serviceName"].Should().Be("api");
        notification.Metadata["previousStatus"].Should().Be("Healthy");
        notification.Metadata["currentStatus"].Should().Be("Unhealthy");
    }

    [Fact]
    public void CreateHealthChangeNotification_SpecialCharsInStackName_EscapesInActionUrl()
    {
        var notification = NotificationFactory.CreateHealthChangeNotification(
            stackName: "my stack/special", serviceName: "api",
            previousStatus: "Healthy", currentStatus: "Unhealthy",
            deploymentId: "dep-123");

        notification.ActionUrl.Should().Be("/deployments/my%20stack%2Fspecial");
    }

    // --- CreateApiKeyFirstUseNotification ---

    [Fact]
    public void CreateApiKeyFirstUseNotification_ReturnsInfoSeverity()
    {
        var notification = NotificationFactory.CreateApiKeyFirstUseNotification(
            keyName: "CI Pipeline", keyPrefix: "rsgo_abc");

        notification.Severity.Should().Be(NotificationSeverity.Info);
    }

    [Fact]
    public void CreateApiKeyFirstUseNotification_SetsCorrectType()
    {
        var notification = NotificationFactory.CreateApiKeyFirstUseNotification(
            keyName: "CI Pipeline", keyPrefix: "rsgo_abc");

        notification.Type.Should().Be(NotificationType.ApiKeyFirstUse);
    }

    [Fact]
    public void CreateApiKeyFirstUseNotification_ContainsKeyNameAndPrefix()
    {
        var notification = NotificationFactory.CreateApiKeyFirstUseNotification(
            keyName: "CI Pipeline", keyPrefix: "rsgo_abc");

        notification.Title.Should().Be("API Key First Used");
        notification.Message.Should().Contain("CI Pipeline").And.Contain("rsgo_abc");
    }

    [Fact]
    public void CreateApiKeyFirstUseNotification_SetsActionUrl()
    {
        var notification = NotificationFactory.CreateApiKeyFirstUseNotification(
            keyName: "CI Pipeline", keyPrefix: "rsgo_abc");

        notification.ActionUrl.Should().Be("/settings/api-keys");
        notification.ActionLabel.Should().Be("View API Keys");
    }

    [Fact]
    public void CreateApiKeyFirstUseNotification_IncludesMetadata()
    {
        var notification = NotificationFactory.CreateApiKeyFirstUseNotification(
            keyName: "CI Pipeline", keyPrefix: "rsgo_abc");

        notification.Metadata["keyName"].Should().Be("CI Pipeline");
        notification.Metadata["keyPrefix"].Should().Be("rsgo_abc");
    }

    // --- CreateCertificateExpiryNotification ---

    [Theory]
    [InlineData(30)]
    [InlineData(14)]
    [InlineData(8)]
    public void CreateCertificateExpiryNotification_WarningThresholds_ReturnsWarningSeverity(
        int daysRemaining)
    {
        var notification = NotificationFactory.CreateCertificateExpiryNotification(
            subject: "*.example.com", thumbprint: "ABC123",
            expiresAt: DateTime.UtcNow.AddDays(daysRemaining), daysRemaining: daysRemaining);

        notification.Severity.Should().Be(NotificationSeverity.Warning);
        notification.Title.Should().Be("Certificate Expiring Soon");
    }

    [Theory]
    [InlineData(7)]
    [InlineData(3)]
    [InlineData(1)]
    public void CreateCertificateExpiryNotification_ErrorThresholds_ReturnsErrorSeverity(
        int daysRemaining)
    {
        var notification = NotificationFactory.CreateCertificateExpiryNotification(
            subject: "*.example.com", thumbprint: "ABC123",
            expiresAt: DateTime.UtcNow.AddDays(daysRemaining), daysRemaining: daysRemaining);

        notification.Severity.Should().Be(NotificationSeverity.Error);
        notification.Title.Should().Be("Certificate Expiring Soon");
    }

    [Fact]
    public void CreateCertificateExpiryNotification_Expired_ReturnsErrorWithExpiredTitle()
    {
        var notification = NotificationFactory.CreateCertificateExpiryNotification(
            subject: "*.example.com", thumbprint: "ABC123",
            expiresAt: DateTime.UtcNow.AddDays(-1), daysRemaining: 0);

        notification.Severity.Should().Be(NotificationSeverity.Error);
        notification.Title.Should().Be("Certificate Expired!");
        notification.Message.Should().Contain("has expired");
    }

    [Fact]
    public void CreateCertificateExpiryNotification_NotExpired_ContainsDaysInMessage()
    {
        var notification = NotificationFactory.CreateCertificateExpiryNotification(
            subject: "*.example.com", thumbprint: "ABC123",
            expiresAt: DateTime.UtcNow.AddDays(14), daysRemaining: 14);

        notification.Message.Should().Contain("14 day(s)");
    }

    [Fact]
    public void CreateCertificateExpiryNotification_SetsCorrectType()
    {
        var notification = NotificationFactory.CreateCertificateExpiryNotification(
            subject: "*.example.com", thumbprint: "ABC123",
            expiresAt: DateTime.UtcNow.AddDays(30), daysRemaining: 30);

        notification.Type.Should().Be(NotificationType.CertificateExpiry);
    }

    [Fact]
    public void CreateCertificateExpiryNotification_SetsActionUrl()
    {
        var notification = NotificationFactory.CreateCertificateExpiryNotification(
            subject: "*.example.com", thumbprint: "ABC123",
            expiresAt: DateTime.UtcNow.AddDays(30), daysRemaining: 30);

        notification.ActionUrl.Should().Be("/settings/tls");
        notification.ActionLabel.Should().Be("View TLS Settings");
    }

    [Fact]
    public void CreateCertificateExpiryNotification_IncludesMetadata()
    {
        var notification = NotificationFactory.CreateCertificateExpiryNotification(
            subject: "*.example.com", thumbprint: "ABC123",
            expiresAt: DateTime.UtcNow.AddDays(14), daysRemaining: 14);

        notification.Metadata["subject"].Should().Be("*.example.com");
        notification.Metadata["thumbprint"].Should().Be("ABC123");
        notification.Metadata["daysRemaining"].Should().Be("14");
        notification.Metadata["threshold"].Should().Be("ABC123:14");
    }
}
