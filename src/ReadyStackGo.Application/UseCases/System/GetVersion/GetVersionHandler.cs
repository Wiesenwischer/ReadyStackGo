using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Notifications;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.System.GetVersion;

/// <summary>
/// Handler for getting system version information.
/// </summary>
public class GetVersionHandler : IRequestHandler<GetVersionQuery, GetVersionResponse>
{
    private readonly IVersionCheckService _versionCheckService;
    private readonly INotificationService? _notificationService;
    private readonly ILogger<GetVersionHandler> _logger;

    public GetVersionHandler(
        IVersionCheckService versionCheckService,
        ILogger<GetVersionHandler> logger,
        INotificationService? notificationService = null)
    {
        _versionCheckService = versionCheckService;
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task<GetVersionResponse> Handle(GetVersionQuery request, CancellationToken cancellationToken)
    {
        var currentVersion = _versionCheckService.GetCurrentVersion();
        var latestInfo = await _versionCheckService.GetLatestVersionAsync(request.ForceCheck, cancellationToken);

        var updateAvailable = false;
        if (latestInfo != null)
        {
            updateAvailable = IsNewerVersion(currentVersion, latestInfo.Version);
        }

        if (updateAvailable && latestInfo != null)
        {
            await CreateUpdateNotificationAsync(latestInfo.Version, latestInfo.ReleaseUrl, cancellationToken);
        }

        return new GetVersionResponse
        {
            ServerVersion = currentVersion,
            UpdateAvailable = updateAvailable,
            LatestVersion = latestInfo?.Version,
            LatestReleaseUrl = latestInfo?.ReleaseUrl,
            CheckedAt = latestInfo?.CheckedAt,
            Build = new BuildInfo
            {
                GitCommit = GetGitCommit(),
                BuildDate = GetBuildDate(),
                RuntimeVersion = global::System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
            }
        };
    }

    private async Task CreateUpdateNotificationAsync(string latestVersion, string? releaseUrl, CancellationToken ct)
    {
        if (_notificationService == null) return;

        try
        {
            var alreadyExists = await _notificationService.ExistsAsync(
                NotificationType.UpdateAvailable, "latestVersion", latestVersion, ct);

            if (alreadyExists) return;

            var notification = new Notification
            {
                Type = NotificationType.UpdateAvailable,
                Title = "Update Available",
                Message = $"Version {latestVersion} is available. Go to Settings > System to update.",
                Severity = NotificationSeverity.Info,
                ActionUrl = "/settings/system",
                ActionLabel = "View Update",
                Metadata = new Dictionary<string, string> { ["latestVersion"] = latestVersion }
            };

            if (!string.IsNullOrEmpty(releaseUrl))
            {
                notification.Metadata["releaseUrl"] = releaseUrl;
            }

            await _notificationService.AddAsync(notification, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to create update notification for version {Version}", latestVersion);
        }
    }

    private static bool IsNewerVersion(string currentVersion, string latestVersion)
    {
        // Remove 'v' prefix if present
        var current = currentVersion.TrimStart('v', 'V');
        var latest = latestVersion.TrimStart('v', 'V');

        if (Version.TryParse(current, out var currentVer) &&
            Version.TryParse(latest, out var latestVer))
        {
            return latestVer > currentVer;
        }

        // Fallback to string comparison
        return string.Compare(latest, current, StringComparison.OrdinalIgnoreCase) > 0;
    }

    private static string? GetGitCommit()
    {
        var assembly = typeof(GetVersionHandler).Assembly;
        var attribute = assembly.GetCustomAttributes(typeof(global::System.Reflection.AssemblyInformationalVersionAttribute), false)
            .FirstOrDefault() as global::System.Reflection.AssemblyInformationalVersionAttribute;

        // InformationalVersion often contains commit hash after '+'
        var version = attribute?.InformationalVersion;
        if (version != null && version.Contains('+'))
        {
            return version.Split('+').LastOrDefault();
        }
        return null;
    }

    private static string? GetBuildDate()
    {
        // Try to get build date from assembly metadata
        var assembly = typeof(GetVersionHandler).Assembly;
        var attribute = assembly.GetCustomAttributes(typeof(global::System.Reflection.AssemblyMetadataAttribute), false)
            .Cast<global::System.Reflection.AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildDate");

        return attribute?.Value;
    }
}
