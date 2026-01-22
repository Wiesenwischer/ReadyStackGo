using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.System.GetVersion;

/// <summary>
/// Handler for getting system version information.
/// </summary>
public class GetVersionHandler : IRequestHandler<GetVersionQuery, GetVersionResponse>
{
    private readonly IVersionCheckService _versionCheckService;
    private readonly ILogger<GetVersionHandler> _logger;

    public GetVersionHandler(
        IVersionCheckService versionCheckService,
        ILogger<GetVersionHandler> logger)
    {
        _versionCheckService = versionCheckService;
        _logger = logger;
    }

    public async Task<GetVersionResponse> Handle(GetVersionQuery request, CancellationToken cancellationToken)
    {
        var currentVersion = _versionCheckService.GetCurrentVersion();
        var latestInfo = await _versionCheckService.GetLatestVersionAsync(cancellationToken);

        var updateAvailable = false;
        if (latestInfo != null)
        {
            updateAvailable = IsNewerVersion(currentVersion, latestInfo.Version);
        }

        return new GetVersionResponse
        {
            ServerVersion = currentVersion,
            UpdateAvailable = updateAvailable,
            LatestVersion = latestInfo?.Version,
            LatestReleaseUrl = latestInfo?.ReleaseUrl,
            Build = new BuildInfo
            {
                GitCommit = GetGitCommit(),
                BuildDate = GetBuildDate(),
                RuntimeVersion = global::System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
            }
        };
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
