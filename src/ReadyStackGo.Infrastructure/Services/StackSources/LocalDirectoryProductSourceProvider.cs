using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.StackManagement.Manifests;
using ReadyStackGo.Domain.StackManagement.Sources;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.Infrastructure.Services.StackSources;

/// <summary>
/// Provider that loads products from a local directory.
/// Only supports RSGo Manifest Format (stack.yaml/stack.yml).
/// Each manifest file produces one ProductDefinition containing its stacks.
/// </summary>
public class LocalDirectoryProductSourceProvider : IProductSourceProvider
{
    private readonly ILogger<LocalDirectoryProductSourceProvider> _logger;
    private readonly IRsgoManifestParser _manifestParser;

    public string SourceType => "local-directory";

    public LocalDirectoryProductSourceProvider(
        ILogger<LocalDirectoryProductSourceProvider> logger,
        IRsgoManifestParser manifestParser)
    {
        _logger = logger;
        _manifestParser = manifestParser;
    }

    public bool CanHandle(StackSource source)
    {
        return source.Type == StackSourceType.LocalDirectory;
    }

    public async Task<IEnumerable<ProductDefinition>> LoadProductsAsync(StackSource source, CancellationToken cancellationToken = default)
    {
        if (source.Type != StackSourceType.LocalDirectory)
        {
            throw new ArgumentException($"Expected LocalDirectory source but got {source.Type}");
        }

        var products = new List<ProductDefinition>();

        // Skip disabled sources
        if (!source.Enabled)
        {
            _logger.LogDebug("Product source {SourceId} is disabled, skipping", source.Id);
            return products;
        }

        var path = source.Path ?? throw new ArgumentException("Source path is required for LocalDirectory sources");

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
            return products;
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
                    var product = await LoadProductFromFolderAsync(directory, composeFile, source.Id.Value, path, cancellationToken);
                    if (product != null)
                    {
                        products.Add(product);
                        _logger.LogDebug("Loaded folder-based product {ProductId} from {Directory}", product.Id, directory);
                    }
                    processedPaths.Add(directory);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load product from folder {Directory}", directory);
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
                var product = await LoadProductFromFileAsync(file, source.Id.Value, path, cancellationToken);
                if (product != null)
                {
                    products.Add(product);
                    _logger.LogDebug("Loaded file-based product {ProductId} from {File}", product.Id, file);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load product from {File}", file);
            }
        }

        _logger.LogInformation("Loaded {Count} products from {Path}", products.Count, path);
        return products;
    }

    /// <summary>
    /// Load a product from a folder containing stack.yaml (RSGo Manifest Format).
    /// Each manifest file produces one ProductDefinition containing its stacks.
    /// </summary>
    private async Task<ProductDefinition?> LoadProductFromFolderAsync(string folderPath, string manifestFile, string sourceId, string basePath, CancellationToken cancellationToken)
    {
        var yamlContent = await File.ReadAllTextAsync(manifestFile, cancellationToken);

        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            return null;
        }

        // Calculate relative path from base (e.g., "examples" or "ams.project")
        var relativePath = GetRelativePath(folderPath, basePath);

        // Stack name is the folder name
        var folderName = Path.GetFileName(folderPath);

        // Parse RSGo Manifest from file to resolve includes for multi-stack manifests
        var manifest = await _manifestParser.ParseFromFileAsync(manifestFile, cancellationToken);

        return CreateProductFromManifest(manifest, sourceId, yamlContent, manifestFile, relativePath, folderName);
    }

    /// <summary>
    /// Load a product from a YAML file (RSGo Manifest Format).
    /// Each manifest file produces one ProductDefinition containing its stacks.
    /// </summary>
    private async Task<ProductDefinition?> LoadProductFromFileAsync(string filePath, string sourceId, string basePath, CancellationToken cancellationToken)
    {
        var yamlContent = await File.ReadAllTextAsync(filePath, cancellationToken);

        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            return null;
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

            // Skip fragments (manifests without productVersion) - they are only loaded via include
            if (!manifest.IsProduct)
            {
                _logger.LogDebug("Skipping fragment {File} - no productVersion set", filePath);
                return null;
            }

            return CreateProductFromManifest(manifest, sourceId, yamlContent, filePath, relativePath, fileName);
        }
        catch (Exception ex)
        {
            // Invalid RSGo manifest - skip this file
            _logger.LogWarning(ex, "Invalid RSGo manifest in file {File}, skipping", filePath);
            return null;
        }
    }

    /// <summary>
    /// Creates a ProductDefinition from a manifest.
    /// Single-stack manifests produce a product with one stack.
    /// Multi-stack manifests produce a product with multiple stacks.
    /// </summary>
    private ProductDefinition CreateProductFromManifest(
        RsgoManifest manifest,
        string sourceId,
        string yamlContent,
        string filePath,
        string? relativePath,
        string fallbackName)
    {
        // Ensure manifest has a name (use fallback if not set in metadata)
        manifest.Metadata.Name ??= fallbackName;

        // Product-level metadata
        var productName = manifest.Metadata.Name;
        var productDisplayName = manifest.Metadata.Name;
        var productDescription = manifest.Metadata?.Description;
        var productVersion = manifest.Metadata?.ProductVersion;
        var productCategory = manifest.Metadata?.Category;
        var productTags = manifest.Metadata?.Tags;
        var maintenanceObserver = manifest.Maintenance?.Observer;
        var version = ComputeHash(yamlContent);
        var stacks = new List<StackDefinition>();

        // Check if this is a multi-stack manifest
        if (manifest.IsMultiStack && manifest.Stacks != null && manifest.Stacks.Count > 0)
        {
            foreach (var (stackKey, stackEntry) in manifest.Stacks)
            {
                var stack = CreateStackFromEntry(stackKey, stackEntry, manifest, sourceId, filePath, relativePath, productVersion, version);
                stacks.Add(stack);
                _logger.LogDebug("Created sub-stack '{StackKey}' ({StackName}) with {VarCount} variables, {SvcCount} services",
                    stackKey, stack.Name, stack.Variables.Count, stack.Services.Count);
            }

            _logger.LogInformation("Loaded multi-stack product '{ProductName}' with {StackCount} sub-stacks",
                productName, stacks.Count);
        }
        else
        {
            // Single-stack manifest - create one stack from the manifest
            var stack = CreateStackFromSingleStackManifest(manifest, sourceId, filePath, relativePath, productVersion, version);
            stacks.Add(stack);
            _logger.LogDebug("Loaded single-stack product {ProductName} with {VarCount} variables, {SvcCount} services",
                productName, stack.Variables.Count, stack.Services.Count);
        }

        return new ProductDefinition(
            sourceId: sourceId,
            name: productName,
            displayName: productDisplayName,
            stacks: stacks,
            description: productDescription,
            productVersion: productVersion,
            category: productCategory,
            tags: productTags,
            maintenanceObserver: maintenanceObserver,
            filePath: filePath,
            relativePath: relativePath,
            productId: manifest.Metadata?.ProductId);
    }

    /// <summary>
    /// Creates a StackDefinition from a multi-stack manifest entry.
    /// </summary>
    private StackDefinition CreateStackFromEntry(
        string stackKey,
        RsgoStackEntry stackEntry,
        RsgoManifest manifest,
        string sourceId,
        string filePath,
        string? relativePath,
        string? productVersion,
        string version)
    {
        // Extract services from this sub-stack as ServiceTemplates
        var services = ExtractServiceTemplatesFromStackEntry(stackEntry);
        var volumes = ExtractVolumeDefinitionsFromStackEntry(stackEntry);
        var networks = ExtractNetworkDefinitionsFromStackEntry(stackEntry);

        // Include shared networks from manifest (similar to sharedVariables)
        if (manifest.Networks != null)
        {
            foreach (var (networkName, networkDef) in manifest.Networks)
            {
                // Only add if not already defined in stack (stack-specific takes precedence)
                if (!networks.Any(n => n.Name == networkName))
                {
                    networks.Add(ConvertToNetworkDefinition(networkName, networkDef));
                }
            }
        }

        // Extract variables: shared + stack-specific
        var variables = new List<Variable>();
        if (manifest.SharedVariables != null)
        {
            foreach (var (name, def) in manifest.SharedVariables)
            {
                variables.Add(ConvertToVariable(name, def));
            }
        }
        if (stackEntry.Variables != null)
        {
            foreach (var (name, def) in stackEntry.Variables)
            {
                // Stack-specific overrides shared
                variables.RemoveAll(v => v.Name == name);
                variables.Add(ConvertToVariable(name, def));
            }
        }

        // Stack-level metadata
        var stackName = stackEntry.Metadata?.Name ?? stackKey;
        var productId = ProductId.FromManifest(manifest);

        return new StackDefinition(
            sourceId: sourceId,
            name: stackName,
            productId: productId,
            services: services,
            description: Description.From(stackEntry.Metadata?.Description),
            variables: variables,
            volumes: volumes,
            networks: networks,
            filePath: filePath,
            relativePath: relativePath,
            lastSyncedAt: DateTime.UtcNow,
            version: productVersion ?? version,
            productName: manifest.Metadata.Name,
            productDisplayName: manifest.Metadata.Name,
            productDescription: Description.From(manifest.Metadata.Description),
            productVersion: productVersion,
            category: manifest.Metadata.Category,
            tags: manifest.Metadata.Tags);
    }

    /// <summary>
    /// Creates a StackDefinition from a single-stack manifest.
    /// The manifest's Metadata.Name must already be set.
    /// </summary>
    private StackDefinition CreateStackFromSingleStackManifest(
        RsgoManifest manifest,
        string sourceId,
        string filePath,
        string? relativePath,
        string? productVersion,
        string version)
    {
        var variables = _manifestParser.ExtractVariablesAsync(manifest).GetAwaiter().GetResult();
        var services = ExtractServiceTemplatesFromManifest(manifest);
        var volumes = ExtractVolumeDefinitionsFromManifest(manifest);
        var networks = ExtractNetworkDefinitionsFromManifest(manifest);
        var productId = ProductId.FromManifest(manifest);

        var description = Description.From(manifest.Metadata.Description);

        return new StackDefinition(
            sourceId: sourceId,
            name: manifest.Metadata.Name!,
            productId: productId,
            services: services,
            description: description,
            variables: variables,
            volumes: volumes,
            networks: networks,
            filePath: filePath,
            relativePath: relativePath,
            lastSyncedAt: DateTime.UtcNow,
            version: productVersion ?? version,
            productName: manifest.Metadata.Name,
            productDisplayName: manifest.Metadata.Name,
            productDescription: description,
            productVersion: productVersion,
            category: manifest.Metadata.Category,
            tags: manifest.Metadata.Tags);
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
    /// Supports both Docker HEALTHCHECK and RSGO HTTP/TCP health checks.
    /// </summary>
    private static ServiceHealthCheck ConvertToServiceHealthCheck(RsgoHealthCheck healthCheck)
    {
        return new ServiceHealthCheck
        {
            // Docker HEALTHCHECK fields
            Test = healthCheck.Test ?? new List<string>(),
            Interval = ParseTimeSpan(healthCheck.Interval),
            Timeout = ParseTimeSpan(healthCheck.Timeout),
            Retries = healthCheck.Retries,
            StartPeriod = ParseTimeSpan(healthCheck.StartPeriod),
            // RSGO HTTP/TCP health check fields
            Type = healthCheck.Type,
            Path = healthCheck.Path,
            Port = healthCheck.Port,
            ExpectedStatusCodes = healthCheck.ExpectedStatusCodes,
            Https = healthCheck.Https
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

    private static Variable ConvertToVariable(string name, RsgoVariable def)
    {
        var options = def.Options?.Select(o => new SelectOption(o.Value, o.Label, o.Description));

        return new Variable(
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
