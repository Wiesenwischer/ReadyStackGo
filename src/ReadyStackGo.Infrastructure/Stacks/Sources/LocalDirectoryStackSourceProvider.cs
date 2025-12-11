using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Catalog.Manifests;
using ReadyStackGo.Domain.Catalog.Sources;
using ReadyStackGo.Domain.Catalog.Stacks;

namespace ReadyStackGo.Infrastructure.Stacks.Sources;

/// <summary>
/// Provider that loads stacks from a local directory.
/// Only supports RSGo Manifest Format (stack.yaml/stack.yml).
/// </summary>
public class LocalDirectoryStackSourceProvider : IStackSourceProvider
{
    private readonly ILogger<LocalDirectoryStackSourceProvider> _logger;
    private readonly IRsgoManifestParser _manifestParser;

    public string SourceType => "local-directory";

    public LocalDirectoryStackSourceProvider(
        ILogger<LocalDirectoryStackSourceProvider> logger,
        IRsgoManifestParser manifestParser)
    {
        _logger = logger;
        _manifestParser = manifestParser;
    }

    public bool CanHandle(StackSource source)
    {
        return source.Type == StackSourceType.LocalDirectory;
    }

    public async Task<IEnumerable<StackDefinition>> LoadStacksAsync(StackSource source, CancellationToken cancellationToken = default)
    {
        if (source.Type != StackSourceType.LocalDirectory)
        {
            throw new ArgumentException($"Expected LocalDirectory source but got {source.Type}");
        }

        var stacks = new List<StackDefinition>();

        // Skip disabled sources
        if (!source.Enabled)
        {
            _logger.LogDebug("Stack source {SourceId} is disabled, skipping", source.Id);
            return stacks;
        }

        var path = source.Path;

        // Resolve relative paths
        if (!Path.IsPathRooted(path))
        {
            // Try from base directory first
            var basePath = Path.Combine(AppContext.BaseDirectory, path);
            if (Directory.Exists(basePath))
            {
                path = basePath;
            }
            else
            {
                // Try to find the path by walking up to solution root (for development)
                var solutionRoot = FindSolutionRoot(AppContext.BaseDirectory);
                if (solutionRoot != null)
                {
                    var solutionPath = Path.Combine(solutionRoot, path);
                    if (Directory.Exists(solutionPath))
                    {
                        path = solutionPath;
                    }
                }
            }
        }

        if (!Directory.Exists(path))
        {
            _logger.LogWarning("Directory not found: {Path}", path);
            return stacks;
        }

        // First, look for folder-based stacks (directories containing docker-compose.yml or stack.yaml)
        // Search recursively to support nested folder structures like stacks/ams.project/identityaccess/
        var processedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in Directory.GetDirectories(path, "*", SearchOption.AllDirectories))
        {
            var composeFile = Path.Combine(directory, "docker-compose.yml");
            if (!File.Exists(composeFile))
            {
                composeFile = Path.Combine(directory, "docker-compose.yaml");
            }
            // Also support stack.yaml (RSGo Manifest Format)
            if (!File.Exists(composeFile))
            {
                composeFile = Path.Combine(directory, "stack.yaml");
            }
            if (!File.Exists(composeFile))
            {
                composeFile = Path.Combine(directory, "stack.yml");
            }

            if (File.Exists(composeFile))
            {
                try
                {
                    var loadedStacks = await LoadStacksFromFolderAsync(directory, composeFile, source.Id.Value, path, cancellationToken);
                    foreach (var stack in loadedStacks)
                    {
                        stacks.Add(stack);
                        _logger.LogDebug("Loaded folder-based stack {StackId} from {Directory}", stack.Id, directory);
                    }
                    processedPaths.Add(directory);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load stack from folder {Directory}", directory);
                }
            }
        }

        // Then, look for standalone YAML files (not in processed folders)
        // Search recursively to find files in subdirectories like stacks/examples/simple-nginx.yml
        var filePattern = source.FilePattern ?? "*.yml;*.yaml";
        var patterns = filePattern.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var files = new List<string>();

        foreach (var pattern in patterns)
        {
            files.AddRange(Directory.GetFiles(path, pattern.Trim(), SearchOption.AllDirectories));
        }

        foreach (var file in files.Distinct())
        {
            // Skip if this file is in a folder we already processed as a folder-based stack
            var fileDir = Path.GetDirectoryName(file);
            if (fileDir != null && processedPaths.Contains(fileDir))
            {
                continue;
            }

            try
            {
                var loadedStacks = await LoadStacksFromFileAsync(file, source.Id.Value, path, cancellationToken);
                foreach (var stack in loadedStacks)
                {
                    stacks.Add(stack);
                    _logger.LogDebug("Loaded file-based stack {StackId} from {File}", stack.Id, file);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load stack from {File}", file);
            }
        }

        _logger.LogInformation("Loaded {Count} stacks from {Path}", stacks.Count, path);
        return stacks;
    }

    /// <summary>
    /// Load stacks from a folder containing stack.yaml (RSGo Manifest Format).
    /// For multi-stack manifests, returns one StackDefinition per sub-stack.
    /// </summary>
    private async Task<IEnumerable<StackDefinition>> LoadStacksFromFolderAsync(string folderPath, string manifestFile, string sourceId, string basePath, CancellationToken cancellationToken)
    {
        var yamlContent = await File.ReadAllTextAsync(manifestFile, cancellationToken);

        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            return Enumerable.Empty<StackDefinition>();
        }

        // Calculate relative path from base (e.g., "examples" or "ams.project")
        var relativePath = GetRelativePath(folderPath, basePath);

        // Stack name is the folder name
        var folderName = Path.GetFileName(folderPath);

        // Parse RSGo Manifest from file to resolve includes for multi-stack manifests
        var manifest = await _manifestParser.ParseFromFileAsync(manifestFile, cancellationToken);

        // Check if this is a multi-stack manifest
        if (manifest.IsMultiStack && manifest.Stacks != null && manifest.Stacks.Count > 0)
        {
            return CreateStackDefinitionsFromMultiStack(manifest, sourceId, yamlContent, manifestFile, relativePath);
        }

        // Single-stack manifest - product = stack
        var variables = await _manifestParser.ExtractVariablesAsync(manifest);
        var services = ExtractServiceTemplatesFromManifest(manifest);
        var volumes = ExtractVolumeDefinitionsFromManifest(manifest);
        var networks = ExtractNetworkDefinitionsFromManifest(manifest);
        var stackName = manifest.Metadata?.Name ?? folderName;
        var description = manifest.Metadata?.Description;
        var productVersion = manifest.Metadata?.ProductVersion;
        var version = ComputeHash(yamlContent);
        var category = manifest.Metadata?.Category;
        var tags = manifest.Metadata?.Tags;

        _logger.LogDebug("Loaded RSGo manifest {StackName} with {VarCount} variables, {SvcCount} services",
            folderName, variables.Count, services.Count);

        return new[]
        {
            new StackDefinition(
                sourceId: sourceId,
                name: stackName,
                services: services,
                description: description,
                variables: variables,
                volumes: volumes,
                networks: networks,
                filePath: manifestFile,
                relativePath: relativePath,
                lastSyncedAt: DateTime.UtcNow,
                version: productVersion ?? version,
                // Product properties - for single-stack, product = stack
                productName: stackName,
                productDisplayName: stackName,
                productDescription: description,
                productVersion: productVersion,
                category: category,
                tags: tags,
                // Maintenance configuration
                maintenanceObserver: manifest.Maintenance?.Observer)
        };
    }

    /// <summary>
    /// Load stacks from a YAML file (RSGo Manifest Format).
    /// For multi-stack manifests, returns one StackDefinition per sub-stack.
    /// </summary>
    private async Task<IEnumerable<StackDefinition>> LoadStacksFromFileAsync(string filePath, string sourceId, string basePath, CancellationToken cancellationToken)
    {
        var yamlContent = await File.ReadAllTextAsync(filePath, cancellationToken);

        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            return Enumerable.Empty<StackDefinition>();
        }

        // Extract stack name from filename
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        // Calculate relative path from base (e.g., "examples" for stacks/examples/simple-nginx.yml)
        var fileDir = Path.GetDirectoryName(filePath);
        var relativePath = fileDir != null ? GetRelativePathForFile(fileDir, basePath) : null;

        try
        {
            // Parse RSGo Manifest from file to resolve includes for multi-stack manifests
            var manifest = await _manifestParser.ParseFromFileAsync(filePath, cancellationToken);

            // Check if this is a multi-stack manifest
            if (manifest.IsMultiStack && manifest.Stacks != null && manifest.Stacks.Count > 0)
            {
                return CreateStackDefinitionsFromMultiStack(manifest, sourceId, yamlContent, filePath, relativePath);
            }

            // Single-stack manifest - product = stack
            var variables = await _manifestParser.ExtractVariablesAsync(manifest);
            var services = ExtractServiceTemplatesFromManifest(manifest);
            var volumes = ExtractVolumeDefinitionsFromManifest(manifest);
            var networks = ExtractNetworkDefinitionsFromManifest(manifest);
            var stackName = manifest.Metadata?.Name ?? fileName;
            var description = manifest.Metadata?.Description;
            var productVersion = manifest.Metadata?.ProductVersion;
            var version = ComputeHash(yamlContent);
            var category = manifest.Metadata?.Category;
            var tags = manifest.Metadata?.Tags;

            _logger.LogDebug("Loaded RSGo manifest file {StackName} with {VarCount} variables, {SvcCount} services",
                fileName, variables.Count, services.Count);

            return new[]
            {
                new StackDefinition(
                    sourceId: sourceId,
                    name: stackName,
                    services: services,
                    description: description,
                    variables: variables,
                    volumes: volumes,
                    networks: networks,
                    filePath: filePath,
                    relativePath: relativePath,
                    lastSyncedAt: DateTime.UtcNow,
                    version: productVersion ?? version,
                    // Product properties - for single-stack, product = stack
                    productName: stackName,
                    productDisplayName: stackName,
                    productDescription: description,
                    productVersion: productVersion,
                    category: category,
                    tags: tags,
                    // Maintenance configuration
                    maintenanceObserver: manifest.Maintenance?.Observer)
            };
        }
        catch (Exception ex)
        {
            // Invalid RSGo manifest - skip this file
            _logger.LogWarning(ex, "Invalid RSGo manifest in file {File}, skipping", filePath);
            return Enumerable.Empty<StackDefinition>();
        }
    }

    /// <summary>
    /// Creates StackDefinition objects for each sub-stack in a multi-stack manifest.
    /// All stacks share the same ProductName for proper grouping.
    /// </summary>
    private IEnumerable<StackDefinition> CreateStackDefinitionsFromMultiStack(
        RsgoManifest manifest,
        string sourceId,
        string yamlContent,
        string filePath,
        string? relativePath)
    {
        // Product-level metadata (shared across all stacks)
        var productName = manifest.Metadata?.Name ?? "Unknown";
        var productDisplayName = manifest.Metadata?.Name ?? "Unknown";
        var productDescription = manifest.Metadata?.Description;
        var productVersion = manifest.Metadata?.ProductVersion;
        var productCategory = manifest.Metadata?.Category;
        var productTags = manifest.Metadata?.Tags;
        var version = ComputeHash(yamlContent);
        var results = new List<StackDefinition>();

        foreach (var (stackKey, stackEntry) in manifest.Stacks!)
        {
            // Extract services from this sub-stack as ServiceTemplates
            var services = ExtractServiceTemplatesFromStackEntry(stackEntry);
            var volumes = ExtractVolumeDefinitionsFromStackEntry(stackEntry);
            var networks = ExtractNetworkDefinitionsFromStackEntry(stackEntry);

            // Extract variables: shared + stack-specific
            var variables = new List<StackVariable>();
            if (manifest.SharedVariables != null)
            {
                foreach (var (name, def) in manifest.SharedVariables)
                {
                    variables.Add(ConvertToStackVariable(name, def));
                }
            }
            if (stackEntry.Variables != null)
            {
                foreach (var (name, def) in stackEntry.Variables)
                {
                    // Stack-specific overrides shared
                    variables.RemoveAll(v => v.Name == name);
                    variables.Add(ConvertToStackVariable(name, def));
                }
            }

            // Stack-level metadata
            var stackName = stackEntry.Metadata?.Name ?? stackKey;
            var stackDescription = stackEntry.Metadata?.Description ?? productDescription;

            _logger.LogDebug("Created sub-stack '{StackKey}' ({StackName}) with {VarCount} variables, {SvcCount} services, HasMaintenance={HasMaintenance}",
                stackKey, stackName, variables.Count, services.Count, manifest.Maintenance?.Observer != null);

            results.Add(new StackDefinition(
                sourceId: sourceId,
                name: stackName,
                services: services,
                description: stackDescription,
                variables: variables,
                volumes: volumes,
                networks: networks,
                filePath: filePath,
                relativePath: relativePath,
                lastSyncedAt: DateTime.UtcNow,
                version: productVersion ?? version,
                // Product properties - all stacks share the same product
                productName: productName,
                productDisplayName: productDisplayName,
                productDescription: productDescription,
                productVersion: productVersion,
                category: productCategory,
                tags: productTags,
                // Maintenance configuration - inherited from product level
                maintenanceObserver: manifest.Maintenance?.Observer));
        }

        _logger.LogInformation("Loaded multi-stack manifest '{ProductName}' with {StackCount} sub-stacks",
            productName, results.Count);

        return results;
    }

    /// <summary>
    /// Extracts ServiceTemplate objects from a manifest.
    /// </summary>
    private static List<ServiceTemplate> ExtractServiceTemplatesFromManifest(RsgoManifest manifest)
    {
        var services = new List<ServiceTemplate>();

        if (manifest.Services != null)
        {
            foreach (var (serviceName, service) in manifest.Services)
            {
                services.Add(ConvertToServiceTemplate(serviceName, service));
            }
        }

        // Multi-stack: collect services from all stacks (includes are already resolved)
        if (manifest.Stacks != null)
        {
            foreach (var (_, stackEntry) in manifest.Stacks)
            {
                if (stackEntry.Services != null)
                {
                    foreach (var (serviceName, service) in stackEntry.Services)
                    {
                        // Only add if not already present
                        if (!services.Any(s => s.Name == serviceName))
                        {
                            services.Add(ConvertToServiceTemplate(serviceName, service));
                        }
                    }
                }
            }
        }

        return services;
    }

    /// <summary>
    /// Extracts ServiceTemplate objects from a stack entry.
    /// </summary>
    private static List<ServiceTemplate> ExtractServiceTemplatesFromStackEntry(RsgoStackEntry stackEntry)
    {
        var services = new List<ServiceTemplate>();

        if (stackEntry.Services != null)
        {
            foreach (var (serviceName, service) in stackEntry.Services)
            {
                services.Add(ConvertToServiceTemplate(serviceName, service));
            }
        }

        return services;
    }

    /// <summary>
    /// Converts an RsgoService to a ServiceTemplate.
    /// </summary>
    private static ServiceTemplate ConvertToServiceTemplate(string serviceName, RsgoService service)
    {
        return new ServiceTemplate
        {
            Name = serviceName,
            Image = service.Image ?? "unknown",
            ContainerName = service.ContainerName,
            Ports = service.Ports?.Select(p => PortMapping.Parse(p)).ToList() ?? new List<PortMapping>(),
            Volumes = service.Volumes?.Select(v => VolumeMapping.Parse(v)).ToList() ?? new List<VolumeMapping>(),
            Environment = service.Environment?.ToDictionary(e => e.Key, e => e.Value) ?? new Dictionary<string, string>(),
            Labels = service.Labels ?? new Dictionary<string, string>(),
            Networks = service.Networks ?? new List<string>(),
            DependsOn = service.DependsOn ?? new List<string>(),
            RestartPolicy = service.Restart,
            Command = service.Command,
            Entrypoint = service.Entrypoint,
            WorkingDir = service.WorkingDir,
            User = service.User,
            HealthCheck = service.HealthCheck != null ? ConvertToServiceHealthCheck(service.HealthCheck) : null
        };
    }

    /// <summary>
    /// Converts an RsgoHealthCheck to a ServiceHealthCheck.
    /// </summary>
    private static ServiceHealthCheck? ConvertToServiceHealthCheck(RsgoHealthCheck healthCheck)
    {
        // Skip if this is an RSGO-specific health check (HTTP/TCP), not a Docker HEALTHCHECK
        if (healthCheck.IsHttpHealthCheck || healthCheck.IsTcpHealthCheck || healthCheck.IsDisabled)
            return null;

        return new ServiceHealthCheck
        {
            Test = healthCheck.Test ?? new List<string>(),
            Interval = ParseTimeSpan(healthCheck.Interval),
            Timeout = ParseTimeSpan(healthCheck.Timeout),
            Retries = healthCheck.Retries,
            StartPeriod = ParseTimeSpan(healthCheck.StartPeriod)
        };
    }

    /// <summary>
    /// Parses a Docker-style duration string (e.g., "30s", "1m", "1h") to TimeSpan.
    /// </summary>
    private static TimeSpan? ParseTimeSpan(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
            return null;

        // Try to parse common formats: 30s, 1m, 1h, etc.
        var value = duration.Trim().ToLowerInvariant();

        if (value.EndsWith('s') && double.TryParse(value[..^1], out var seconds))
            return TimeSpan.FromSeconds(seconds);

        if (value.EndsWith('m') && double.TryParse(value[..^1], out var minutes))
            return TimeSpan.FromMinutes(minutes);

        if (value.EndsWith('h') && double.TryParse(value[..^1], out var hours))
            return TimeSpan.FromHours(hours);

        if (value.EndsWith("ms") && double.TryParse(value[..^2], out var ms))
            return TimeSpan.FromMilliseconds(ms);

        // Fallback: try parsing as TimeSpan
        if (TimeSpan.TryParse(duration, out var ts))
            return ts;

        return null;
    }

    /// <summary>
    /// Extracts VolumeDefinition objects from a manifest.
    /// </summary>
    private static List<VolumeDefinition> ExtractVolumeDefinitionsFromManifest(RsgoManifest manifest)
    {
        var volumes = new List<VolumeDefinition>();

        if (manifest.Volumes != null)
        {
            foreach (var (volumeName, volumeDef) in manifest.Volumes)
            {
                volumes.Add(ConvertToVolumeDefinition(volumeName, volumeDef));
            }
        }

        return volumes;
    }

    /// <summary>
    /// Extracts VolumeDefinition objects from a stack entry.
    /// </summary>
    private static List<VolumeDefinition> ExtractVolumeDefinitionsFromStackEntry(RsgoStackEntry stackEntry)
    {
        var volumes = new List<VolumeDefinition>();

        if (stackEntry.Volumes != null)
        {
            foreach (var (volumeName, volumeDef) in stackEntry.Volumes)
            {
                volumes.Add(ConvertToVolumeDefinition(volumeName, volumeDef));
            }
        }

        return volumes;
    }

    /// <summary>
    /// Converts an RSGo volume definition to VolumeDefinition.
    /// </summary>
    private static VolumeDefinition ConvertToVolumeDefinition(string volumeName, RsgoVolume? volumeDef)
    {
        return new VolumeDefinition
        {
            Name = volumeName,
            Driver = volumeDef?.Driver,
            External = volumeDef?.External ?? false,
            DriverOpts = volumeDef?.DriverOpts ?? new Dictionary<string, string>()
        };
    }

    /// <summary>
    /// Extracts NetworkDefinition objects from a manifest.
    /// </summary>
    private static List<NetworkDefinition> ExtractNetworkDefinitionsFromManifest(RsgoManifest manifest)
    {
        var networks = new List<NetworkDefinition>();

        if (manifest.Networks != null)
        {
            foreach (var (networkName, networkDef) in manifest.Networks)
            {
                networks.Add(ConvertToNetworkDefinition(networkName, networkDef));
            }
        }

        return networks;
    }

    /// <summary>
    /// Extracts NetworkDefinition objects from a stack entry.
    /// </summary>
    private static List<NetworkDefinition> ExtractNetworkDefinitionsFromStackEntry(RsgoStackEntry stackEntry)
    {
        var networks = new List<NetworkDefinition>();

        if (stackEntry.Networks != null)
        {
            foreach (var (networkName, networkDef) in stackEntry.Networks)
            {
                networks.Add(ConvertToNetworkDefinition(networkName, networkDef));
            }
        }

        return networks;
    }

    /// <summary>
    /// Converts an RSGo network definition to NetworkDefinition.
    /// </summary>
    private static NetworkDefinition ConvertToNetworkDefinition(string networkName, RsgoNetwork? networkDef)
    {
        return new NetworkDefinition
        {
            Name = networkName,
            Driver = networkDef?.Driver ?? "bridge",
            External = networkDef?.External ?? false,
            DriverOpts = networkDef?.DriverOpts ?? new Dictionary<string, string>()
        };
    }

    private static StackVariable ConvertToStackVariable(string name, RsgoVariable def)
    {
        var options = def.Options?.Select(o => new SelectOption(o.Value, o.Label, o.Description));

        return new StackVariable(
            name: name,
            defaultValue: def.Default,
            description: def.Description,
            type: def.Type,
            label: def.Label,
            pattern: def.Pattern,
            patternError: def.PatternError,
            options: options,
            min: def.Min,
            max: def.Max,
            placeholder: def.Placeholder,
            group: def.Group,
            order: def.Order,
            isRequired: def.Required
        );
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    private static string? FindSolutionRoot(string startPath)
    {
        var directory = new DirectoryInfo(startPath);
        while (directory != null)
        {
            // Look for solution file or .git directory as markers of project root
            if (directory.GetFiles("*.sln").Length > 0 ||
                directory.GetDirectories(".git").Length > 0)
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        return null;
    }

    /// <summary>
    /// Get the relative path from basePath to targetPath, excluding the final folder name.
    /// Used for folder-based stacks where the stack folder itself should not be included.
    /// For example: basePath="/app/stacks", targetPath="/app/stacks/examples/wordpress" returns "examples"
    /// </summary>
    private static string? GetRelativePath(string targetPath, string basePath)
    {
        try
        {
            var baseDir = new DirectoryInfo(basePath);
            var targetDir = new DirectoryInfo(targetPath);

            // Get the relative path from base to target's parent (excluding the stack folder itself)
            var targetParent = targetDir.Parent;
            if (targetParent == null || string.Equals(targetParent.FullName, baseDir.FullName, StringComparison.OrdinalIgnoreCase))
            {
                // Target is directly in base path
                return null;
            }

            // Calculate relative path
            var relativePath = Path.GetRelativePath(baseDir.FullName, targetParent.FullName);

            // Don't return "." for same directory
            if (relativePath == ".")
            {
                return null;
            }

            return relativePath;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get the relative path from basePath to the file's directory.
    /// Used for file-based stacks where we want the folder containing the file.
    /// For example: basePath="/app/stacks", fileDir="/app/stacks/examples" returns "examples"
    /// </summary>
    private static string? GetRelativePathForFile(string fileDir, string basePath)
    {
        try
        {
            var baseDir = new DirectoryInfo(basePath);
            var targetDir = new DirectoryInfo(fileDir);

            // If file is directly in base path, no relative path
            if (string.Equals(targetDir.FullName, baseDir.FullName, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Calculate relative path directly to the file's directory
            var relativePath = Path.GetRelativePath(baseDir.FullName, targetDir.FullName);

            // Don't return "." for same directory
            if (relativePath == ".")
            {
                return null;
            }

            return relativePath;
        }
        catch
        {
            return null;
        }
    }
}
