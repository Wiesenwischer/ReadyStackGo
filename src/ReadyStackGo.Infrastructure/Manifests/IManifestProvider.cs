using ReadyStackGo.Domain.Manifests;

namespace ReadyStackGo.Infrastructure.Manifests;

/// <summary>
/// Service for loading and managing release manifests
/// </summary>
public interface IManifestProvider
{
    /// <summary>
    /// Load a manifest from a file path
    /// </summary>
    Task<ReleaseManifest> LoadManifestAsync(string manifestPath);

    /// <summary>
    /// Load a manifest from a JSON string
    /// </summary>
    Task<ReleaseManifest> LoadManifestFromJsonAsync(string manifestJson);

    /// <summary>
    /// Validate a manifest structure
    /// </summary>
    Task<ManifestValidationResult> ValidateManifestAsync(ReleaseManifest manifest);

    /// <summary>
    /// List all available manifests in the manifests directory
    /// </summary>
    Task<List<ManifestInfo>> ListAvailableManifestsAsync();

    /// <summary>
    /// Get the latest available manifest
    /// </summary>
    Task<ReleaseManifest?> GetLatestManifestAsync();
}

public class ManifestInfo
{
    public required string FilePath { get; set; }
    public required string StackVersion { get; set; }
    public string? ReleaseName { get; set; }
    public DateTime? ReleaseDate { get; set; }
}

public class ManifestValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
