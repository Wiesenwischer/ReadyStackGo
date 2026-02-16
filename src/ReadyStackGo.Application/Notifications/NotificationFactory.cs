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
            ActionUrl = "/stack-sources",
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
            actionUrl = $"/deployments/{deploymentId}";
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
