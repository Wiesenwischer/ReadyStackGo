using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Manifests;

namespace ReadyStackGo.Infrastructure.Manifests;

/// <summary>
/// File-based manifest provider that loads release manifests from the filesystem
/// </summary>
public class ManifestProvider : IManifestProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ManifestProvider> _logger;
    private readonly string _manifestsPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public ManifestProvider(
        IConfiguration configuration,
        ILogger<ManifestProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _manifestsPath = configuration.GetValue<string>("ManifestsPath") ?? "/app/manifests";

        // Ensure manifests directory exists
        if (!Directory.Exists(_manifestsPath))
        {
            Directory.CreateDirectory(_manifestsPath);
        }

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<ReleaseManifest> LoadManifestAsync(string manifestPath)
    {
        try
        {
            _logger.LogInformation("Loading manifest from {Path}", manifestPath);

            if (!File.Exists(manifestPath))
            {
                throw new FileNotFoundException($"Manifest file not found: {manifestPath}");
            }

            var json = await File.ReadAllTextAsync(manifestPath);
            return await LoadManifestFromJsonAsync(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load manifest from {Path}", manifestPath);
            throw;
        }
    }

    public async Task<ReleaseManifest> LoadManifestFromJsonAsync(string manifestJson)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<ReleaseManifest>(manifestJson, _jsonOptions);

            if (manifest == null)
            {
                throw new InvalidOperationException("Failed to deserialize manifest");
            }

            // Validate the manifest
            var validationResult = await ValidateManifestAsync(manifest);
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException(
                    $"Manifest validation failed: {string.Join(", ", validationResult.Errors)}");
            }

            _logger.LogInformation("Successfully loaded manifest version {Version}", manifest.StackVersion);
            return manifest;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse manifest JSON");
            throw new InvalidOperationException("Invalid manifest JSON format", ex);
        }
    }

    public async Task<ManifestValidationResult> ValidateManifestAsync(ReleaseManifest manifest)
    {
        var result = new ManifestValidationResult { IsValid = true };

        // Validate required fields
        if (string.IsNullOrWhiteSpace(manifest.StackVersion))
        {
            result.Errors.Add("StackVersion is required");
            result.IsValid = false;
        }

        if (string.IsNullOrWhiteSpace(manifest.ManifestVersion))
        {
            result.Errors.Add("ManifestVersion is required");
            result.IsValid = false;
        }

        // Validate SemVer format for StackVersion
        if (!string.IsNullOrWhiteSpace(manifest.StackVersion) && !IsValidSemVer(manifest.StackVersion))
        {
            result.Errors.Add($"StackVersion '{manifest.StackVersion}' is not a valid SemVer format");
            result.IsValid = false;
        }

        // Validate contexts
        if (manifest.Contexts == null || manifest.Contexts.Count == 0)
        {
            result.Warnings.Add("No contexts defined in manifest");
        }
        else
        {
            foreach (var (contextName, context) in manifest.Contexts)
            {
                if (string.IsNullOrWhiteSpace(context.Image))
                {
                    result.Errors.Add($"Context '{contextName}': Image is required");
                    result.IsValid = false;
                }

                if (string.IsNullOrWhiteSpace(context.Version))
                {
                    result.Errors.Add($"Context '{contextName}': Version is required");
                    result.IsValid = false;
                }

                if (string.IsNullOrWhiteSpace(context.ContainerName))
                {
                    result.Errors.Add($"Context '{contextName}': ContainerName is required");
                    result.IsValid = false;
                }

                // Validate dependencies exist
                if (context.DependsOn != null)
                {
                    foreach (var dependency in context.DependsOn)
                    {
                        if (!manifest.Contexts.ContainsKey(dependency))
                        {
                            result.Errors.Add($"Context '{contextName}': Dependency '{dependency}' not found in manifest");
                            result.IsValid = false;
                        }
                    }
                }
            }
        }

        // Validate gateway configuration if present
        if (manifest.Gateway != null)
        {
            if (string.IsNullOrWhiteSpace(manifest.Gateway.Context))
            {
                result.Errors.Add("Gateway: Context is required");
                result.IsValid = false;
            }
            else if (!manifest.Contexts.ContainsKey(manifest.Gateway.Context))
            {
                result.Errors.Add($"Gateway: Context '{manifest.Gateway.Context}' not found in manifest");
                result.IsValid = false;
            }
        }

        return await Task.FromResult(result);
    }

    public async Task<List<ManifestInfo>> ListAvailableManifestsAsync()
    {
        try
        {
            var manifestFiles = Directory.GetFiles(_manifestsPath, "*.json", SearchOption.TopDirectoryOnly);
            var manifests = new List<ManifestInfo>();

            foreach (var file in manifestFiles)
            {
                try
                {
                    var manifest = await LoadManifestAsync(file);
                    manifests.Add(new ManifestInfo
                    {
                        FilePath = file,
                        StackVersion = manifest.StackVersion,
                        ReleaseName = manifest.Metadata?.ReleaseName,
                        ReleaseDate = manifest.Metadata?.ReleaseDate
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load manifest from {File}", file);
                }
            }

            // Sort by semantic version (newest first)
            return manifests
                .OrderByDescending(m => ParseSemVer(m.StackVersion))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list available manifests");
            return new List<ManifestInfo>();
        }
    }

    public async Task<ReleaseManifest?> GetLatestManifestAsync()
    {
        var manifests = await ListAvailableManifestsAsync();

        if (manifests.Count == 0)
        {
            _logger.LogWarning("No manifests available");
            return null;
        }

        var latest = manifests.First();
        return await LoadManifestAsync(latest.FilePath);
    }

    private static bool IsValidSemVer(string version)
    {
        // Simple SemVer validation (supports x.y.z and x.y.z-prerelease)
        var semVerPattern = @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$";
        return Regex.IsMatch(version, semVerPattern);
    }

    private static Version ParseSemVer(string version)
    {
        // Extract major.minor.patch from SemVer (ignore prerelease/metadata)
        var match = Regex.Match(version, @"^(\d+)\.(\d+)\.(\d+)");
        if (match.Success)
        {
            return new Version(
                int.Parse(match.Groups[1].Value),
                int.Parse(match.Groups[2].Value),
                int.Parse(match.Groups[3].Value));
        }

        return new Version(0, 0, 0);
    }
}
