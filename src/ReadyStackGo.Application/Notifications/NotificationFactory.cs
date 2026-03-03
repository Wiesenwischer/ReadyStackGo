namespace ReadyStackGo.Application.Notifications;

/// <summary>
/// Static factory for creating well-formatted notifications with consistent
/// severity, message format, and metadata.
/// </summary>
public static class NotificationFactory
{
    public static Notification CreateSyncResult(
        bool success, int stacksLoaded, int sourcesSynced,
        IReadOnlyList<string> errors, IReadOnlyList<string> warnings,
        string? sourceName = null)
    {
        var (severity, title, message) = ResolveSyncSeverity(
            success, stacksLoaded, sourcesSynced, errors, warnings, sourceName);

        return new Notification
        {
            Type = NotificationType.SourceSyncResult,
            Title = title,
            Message = message,
            Severity = severity,
            ActionUrl = "/settings/stack-sources",
            ActionLabel = "View Sources",
            Metadata = new Dictionary<string, string>
            {
                ["stacksLoaded"] = stacksLoaded.ToString(),
                ["sourcesSynced"] = sourcesSynced.ToString()
            }
        };
    }

    public static Notification CreateDeploymentResult(
        bool success, string operation, string stackName,
        string? message = null, string? deploymentId = null)
    {
        var severity = success ? NotificationSeverity.Success : NotificationSeverity.Error;
        var title = FormatDeploymentTitle(operation, success);
        var body = message ?? FormatDeploymentMessage(operation, stackName, success);

        var metadata = new Dictionary<string, string>
        {
            ["operation"] = operation,
            ["stackName"] = stackName
        };

        string? actionUrl = null;
        if (!string.IsNullOrEmpty(deploymentId))
        {
            metadata["deploymentId"] = deploymentId;
            actionUrl = $"/deployments/{Uri.EscapeDataString(stackName)}";
        }

        return new Notification
        {
            Type = NotificationType.DeploymentResult,
            Title = title,
            Message = body,
            Severity = severity,
            ActionUrl = actionUrl,
            ActionLabel = actionUrl != null ? "View Deployment" : null,
            Metadata = metadata
        };
    }

    public static Notification CreateProductDeploymentResult(
        bool success, string operation, string productName, string productVersion,
        int totalStacks, int completedStacks, int failedStacks,
        string? message = null, string? productDeploymentId = null)
    {
        var severity = success
            ? NotificationSeverity.Success
            : failedStacks > 0 && completedStacks > 0
                ? NotificationSeverity.Warning
                : NotificationSeverity.Error;

        var op = char.ToUpper(operation[0]) + operation[1..];
        var title = success ? $"Product {op} Successful" : $"Product {op} Failed";

        var body = message ?? (success
            ? $"Product '{productName}' v{productVersion} was successfully {GetPastTense(operation)} ({totalStacks} stacks)."
            : $"Failed to {operation} product '{productName}' v{productVersion}. {completedStacks}/{totalStacks} stacks succeeded, {failedStacks} failed.");

        var metadata = new Dictionary<string, string>
        {
            ["operation"] = operation,
            ["productName"] = productName,
            ["productVersion"] = productVersion,
            ["totalStacks"] = totalStacks.ToString(),
            ["completedStacks"] = completedStacks.ToString(),
            ["failedStacks"] = failedStacks.ToString()
        };

        string? actionUrl = null;
        if (!string.IsNullOrEmpty(productDeploymentId))
        {
            metadata["productDeploymentId"] = productDeploymentId;
            actionUrl = $"/product-deployments/{productDeploymentId}";
        }

        return new Notification
        {
            Type = NotificationType.ProductDeploymentResult,
            Title = title,
            Message = body,
            Severity = severity,
            ActionUrl = actionUrl,
            ActionLabel = actionUrl != null ? "View Product Deployment" : null,
            Metadata = metadata
        };
    }

    public static Notification CreateHealthChangeNotification(
        string stackName, string serviceName,
        string previousStatus, string currentStatus,
        string? deploymentId = null)
    {
        var isRecovery = currentStatus.Equals("Healthy", StringComparison.OrdinalIgnoreCase);
        var severity = ResolveHealthSeverity(currentStatus);
        var title = isRecovery ? "Service Recovered" : "Service Health Changed";
        var message = $"Service '{serviceName}' in stack '{stackName}' changed from {previousStatus} to {currentStatus}.";

        var metadata = new Dictionary<string, string>
        {
            ["serviceKey"] = $"{deploymentId}:{serviceName}",
            ["stackName"] = stackName,
            ["serviceName"] = serviceName,
            ["previousStatus"] = previousStatus,
            ["currentStatus"] = currentStatus
        };

        string? actionUrl = null;
        if (!string.IsNullOrEmpty(deploymentId))
        {
            metadata["deploymentId"] = deploymentId;
            actionUrl = $"/deployments/{Uri.EscapeDataString(stackName)}";
        }

        return new Notification
        {
            Type = NotificationType.HealthChange,
            Title = title,
            Message = message,
            Severity = severity,
            ActionUrl = actionUrl ?? "/health",
            ActionLabel = actionUrl != null ? "View Deployment" : "View Health",
            Metadata = metadata
        };
    }

    public static Notification CreateApiKeyFirstUseNotification(
        string keyName, string keyPrefix)
    {
        return new Notification
        {
            Type = NotificationType.ApiKeyFirstUse,
            Title = "API Key First Used",
            Message = $"API key '{keyName}' ({keyPrefix}...) was used for the first time.",
            Severity = NotificationSeverity.Info,
            ActionUrl = "/settings/api-keys",
            ActionLabel = "View API Keys",
            Metadata = new Dictionary<string, string>
            {
                ["keyName"] = keyName,
                ["keyPrefix"] = keyPrefix
            }
        };
    }

    public static Notification CreateCertificateExpiryNotification(
        string subject, string thumbprint, DateTime expiresAt, int daysRemaining)
    {
        var severity = daysRemaining <= 7
            ? NotificationSeverity.Error
            : NotificationSeverity.Warning;

        var title = daysRemaining <= 0 ? "Certificate Expired!" : "Certificate Expiring Soon";
        var message = daysRemaining <= 0
            ? $"Certificate for '{subject}' has expired!"
            : $"Certificate for '{subject}' expires in {daysRemaining} day(s).";

        return new Notification
        {
            Type = NotificationType.CertificateExpiry,
            Title = title,
            Message = message,
            Severity = severity,
            ActionUrl = "/settings/tls",
            ActionLabel = "View TLS Settings",
            Metadata = new Dictionary<string, string>
            {
                ["subject"] = subject,
                ["thumbprint"] = thumbprint,
                ["daysRemaining"] = daysRemaining.ToString(),
                ["threshold"] = $"{thumbprint}:{daysRemaining}"
            }
        };
    }

    private static NotificationSeverity ResolveHealthSeverity(string currentStatus) =>
        currentStatus.ToLowerInvariant() switch
        {
            "unhealthy" or "notfound" => NotificationSeverity.Error,
            "degraded" => NotificationSeverity.Warning,
            _ => NotificationSeverity.Info
        };

    private static (NotificationSeverity Severity, string Title, string Message) ResolveSyncSeverity(
        bool success, int stacksLoaded, int sourcesSynced,
        IReadOnlyList<string> errors, IReadOnlyList<string> warnings,
        string? sourceName)
    {
        var target = sourceName != null ? $"'{sourceName}'" : $"{sourcesSynced} source(s)";

        if (!success)
        {
            return (NotificationSeverity.Error,
                "Source Sync Failed",
                $"Sync of {target} failed: {string.Join("; ", errors)}");
        }

        if (warnings.Count > 0)
        {
            return (NotificationSeverity.Warning,
                "Source Sync Completed with Warnings",
                $"Synced {target}: {stacksLoaded} stack(s) loaded. Warnings: {string.Join("; ", warnings)}");
        }

        if (stacksLoaded == 0)
        {
            return (NotificationSeverity.Info,
                "Source Sync Complete",
                $"Synced {target}: no changes detected.");
        }

        return (NotificationSeverity.Success,
            "Source Sync Complete",
            $"Synced {target}: {stacksLoaded} stack(s) loaded.");
    }

    private static string FormatDeploymentTitle(string operation, bool success)
    {
        var op = char.ToUpper(operation[0]) + operation[1..];
        return success ? $"{op} Successful" : $"{op} Failed";
    }

    private static string FormatDeploymentMessage(string operation, string stackName, bool success)
    {
        return success
            ? $"Stack '{stackName}' was successfully {GetPastTense(operation)}."
            : $"Failed to {operation} stack '{stackName}'.";
    }

    private static string GetPastTense(string operation) => operation.ToLowerInvariant() switch
    {
        "deploy" => "deployed",
        "upgrade" => "upgraded",
        "rollback" => "rolled back",
        "remove" => "removed",
        _ => operation + "ed"
    };
}
