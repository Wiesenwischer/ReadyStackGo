using System.Text.RegularExpressions;

namespace ReadyStackGo.Infrastructure.Manifests;

/// <summary>
/// Utility class for Docker naming conventions and sanitization.
/// Docker container names must match: [a-zA-Z0-9][a-zA-Z0-9_.-]*
/// </summary>
public static partial class DockerNamingUtility
{
    // Docker container name rules:
    // - Must start with alphanumeric character
    // - Can contain alphanumeric, underscore, period, hyphen
    // - No spaces, special characters, or unicode
    private static readonly Regex InvalidCharsPattern = InvalidCharsRegex();
    private static readonly Regex LeadingInvalidPattern = LeadingInvalidRegex();
    private static readonly Regex ConsecutiveUnderscoresPattern = ConsecutiveUnderscoresRegex();

    /// <summary>
    /// Sanitizes a name for use in Docker container, network, or volume names.
    /// Replaces spaces and invalid characters with underscores.
    /// </summary>
    /// <param name="name">The name to sanitize</param>
    /// <returns>A Docker-compatible name</returns>
    public static string SanitizeForDocker(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "unnamed";

        // Replace spaces with underscores
        var sanitized = name.Replace(' ', '_');

        // Replace any remaining invalid characters with underscores
        // Valid: [a-zA-Z0-9_.-]
        sanitized = InvalidCharsPattern.Replace(sanitized, "_");

        // Ensure it starts with alphanumeric (not underscore, period, or hyphen)
        sanitized = LeadingInvalidPattern.Replace(sanitized, "");

        // Collapse consecutive underscores
        sanitized = ConsecutiveUnderscoresPattern.Replace(sanitized, "_");

        // Trim trailing underscores
        sanitized = sanitized.TrimEnd('_');

        // If the result is empty after sanitization, provide a default
        if (string.IsNullOrEmpty(sanitized))
            return "unnamed";

        return sanitized;
    }

    /// <summary>
    /// Creates a Docker-compatible container name from stack name and service name.
    /// </summary>
    public static string CreateContainerName(string stackName, string serviceName)
    {
        var sanitizedStack = SanitizeForDocker(stackName);
        var sanitizedService = SanitizeForDocker(serviceName);
        return $"{sanitizedStack}_{sanitizedService}";
    }

    /// <summary>
    /// Creates a Docker-compatible network name from stack name and network name.
    /// </summary>
    public static string CreateNetworkName(string stackName, string networkName)
    {
        var sanitizedStack = SanitizeForDocker(stackName);
        var sanitizedNetwork = SanitizeForDocker(networkName);
        return $"{sanitizedStack}_{sanitizedNetwork}";
    }

    /// <summary>
    /// Creates a Docker-compatible volume name from stack name and volume name.
    /// </summary>
    public static string CreateVolumeName(string stackName, string volumeName)
    {
        var sanitizedStack = SanitizeForDocker(stackName);
        var sanitizedVolume = SanitizeForDocker(volumeName);
        return $"{sanitizedStack}_{sanitizedVolume}";
    }

    [GeneratedRegex(@"[^a-zA-Z0-9_.-]", RegexOptions.Compiled)]
    private static partial Regex InvalidCharsRegex();

    [GeneratedRegex(@"^[^a-zA-Z0-9]+", RegexOptions.Compiled)]
    private static partial Regex LeadingInvalidRegex();

    [GeneratedRegex(@"_+", RegexOptions.Compiled)]
    private static partial Regex ConsecutiveUnderscoresRegex();
}
