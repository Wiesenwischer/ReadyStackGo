namespace ReadyStackGo.Application.UseCases.System.GetVersion;

/// <summary>
/// Response containing system version information.
/// </summary>
public record GetVersionResponse
{
    /// <summary>
    /// Current server version.
    /// </summary>
    public required string ServerVersion { get; init; }

    /// <summary>
    /// Whether an update is available.
    /// </summary>
    public bool UpdateAvailable { get; init; }

    /// <summary>
    /// Latest available version (if update check succeeded).
    /// </summary>
    public string? LatestVersion { get; init; }

    /// <summary>
    /// URL to the latest release page.
    /// </summary>
    public string? LatestReleaseUrl { get; init; }

    /// <summary>
    /// Build information.
    /// </summary>
    public required BuildInfo Build { get; init; }
}

/// <summary>
/// Build metadata.
/// </summary>
public record BuildInfo
{
    /// <summary>
    /// Git commit hash.
    /// </summary>
    public string? GitCommit { get; init; }

    /// <summary>
    /// Build date/time.
    /// </summary>
    public string? BuildDate { get; init; }

    /// <summary>
    /// .NET runtime version.
    /// </summary>
    public string? RuntimeVersion { get; init; }
}
