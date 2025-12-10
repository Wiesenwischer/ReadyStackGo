using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.StackManagement.Manifests;
using ReadyStackGo.Domain.StackManagement.StackSources;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ReadyStackGo.Infrastructure.Stacks.Sources;

/// <summary>
/// Provider that loads stacks from a local directory.
/// Only supports RSGo Manifest Format (stack.yaml/stack.yml).
/// </summary>
public class LocalDirectoryStackSourceProvider : IStackSourceProvider
{
    private readonly ILogger<LocalDirectoryStackSourceProvider> _logger;
    private readonly IRsgoManifestParser _manifestParser;
    private readonly ISerializer _yamlSerializer;

    public string SourceType => "local-directory";

    public LocalDirectoryStackSourceProvider(
        ILogger<LocalDirectoryStackSourceProvider> logger,
        IRsgoManifestParser manifestParser)
    {
        _logger = logger;
        _manifestParser = manifestParser;
        _yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();
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
        var services = ExtractServicesFromManifest(manifest);
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
                yamlContent: yamlContent,
                description: description,
                variables: variables,
                services: services,
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
            var services = ExtractServicesFromManifest(manifest);
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
                    yamlContent: yamlContent,
                    description: description,
                    variables: variables,
                    services: services,
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
            // Extract services from this sub-stack
            var services = stackEntry.Services?.Keys.ToList() ?? new List<string>();

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

            // Generate YAML for this sub-stack, inheriting maintenance config from parent
            var composeYaml = GenerateYamlForStack(stackEntry, manifest.Maintenance);

            _logger.LogDebug("Created sub-stack '{StackKey}' ({StackName}) with {VarCount} variables, {SvcCount} services, HasMaintenance={HasMaintenance}",
                stackKey, stackName, variables.Count, services.Count, manifest.Maintenance?.Observer != null);

            results.Add(new StackDefinition(
                sourceId: sourceId,
                name: stackName,
                yamlContent: composeYaml,
                description: stackDescription,
                variables: variables,
                services: services,
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
    /// Generates Docker Compose compatible YAML content for a sub-stack.
    /// Converts RSGo format to docker-compose format (snake_case, no metadata section).
    /// </summary>
    private string GenerateYamlForStack(RsgoStackEntry stackEntry, RsgoMaintenance? maintenance = null)
    {
        // Build docker-compose compatible structure
        var composeDict = new Dictionary<string, object>();

        // Convert services to docker-compose format
        if (stackEntry.Services != null && stackEntry.Services.Count > 0)
        {
            var servicesDict = new Dictionary<string, object>();
            foreach (var (serviceName, service) in stackEntry.Services)
            {
                servicesDict[serviceName] = ConvertServiceToComposeFormat(service);
            }
            composeDict["services"] = servicesDict;
        }

        // Volumes (already in correct format)
        if (stackEntry.Volumes != null && stackEntry.Volumes.Count > 0)
        {
            composeDict["volumes"] = stackEntry.Volumes;
        }

        // Networks (already in correct format)
        if (stackEntry.Networks != null && stackEntry.Networks.Count > 0)
        {
            composeDict["networks"] = stackEntry.Networks;
        }

        // Use snake_case serializer for docker-compose format
        var composeSerializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();

        return composeSerializer.Serialize(composeDict);
    }

    /// <summary>
    /// Converts an RSGo service configuration to docker-compose format.
    /// </summary>
    private static Dictionary<string, object> ConvertServiceToComposeFormat(RsgoService service)
    {
        var serviceDict = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(service.Image))
            serviceDict["image"] = service.Image;

        if (!string.IsNullOrEmpty(service.ContainerName))
            serviceDict["container_name"] = service.ContainerName;

        if (service.Ports != null && service.Ports.Count > 0)
            serviceDict["ports"] = service.Ports;

        if (service.Environment != null && service.Environment.Count > 0)
            serviceDict["environment"] = service.Environment;

        if (service.Volumes != null && service.Volumes.Count > 0)
            serviceDict["volumes"] = service.Volumes;

        if (service.Networks != null && service.Networks.Count > 0)
            serviceDict["networks"] = service.Networks;

        if (service.DependsOn != null && service.DependsOn.Count > 0)
            serviceDict["depends_on"] = service.DependsOn;

        if (!string.IsNullOrEmpty(service.Restart))
            serviceDict["restart"] = service.Restart;

        if (!string.IsNullOrEmpty(service.Command))
            serviceDict["command"] = service.Command;

        if (!string.IsNullOrEmpty(service.Entrypoint))
            serviceDict["entrypoint"] = service.Entrypoint;

        if (!string.IsNullOrEmpty(service.WorkingDir))
            serviceDict["working_dir"] = service.WorkingDir;

        if (!string.IsNullOrEmpty(service.User))
            serviceDict["user"] = service.User;

        if (service.Labels != null && service.Labels.Count > 0)
            serviceDict["labels"] = service.Labels;

        if (service.HealthCheck != null)
        {
            var healthCheckDict = ConvertHealthCheckToComposeFormat(service.HealthCheck);
            if (healthCheckDict != null)
                serviceDict["healthcheck"] = healthCheckDict;
        }

        return serviceDict;
    }

    /// <summary>
    /// Converts an RSGo health check to docker-compose format.
    /// Only Docker HEALTHCHECK properties are included (not RSGO-specific HTTP checks).
    /// </summary>
    private static Dictionary<string, object>? ConvertHealthCheckToComposeFormat(RsgoHealthCheck healthCheck)
    {
        if (healthCheck.IsHttpHealthCheck || healthCheck.IsTcpHealthCheck || healthCheck.IsDisabled)
            return null;

        var hcDict = new Dictionary<string, object>();

        if (healthCheck.Test != null && healthCheck.Test.Count > 0)
            hcDict["test"] = healthCheck.Test;

        if (!string.IsNullOrEmpty(healthCheck.Interval))
            hcDict["interval"] = healthCheck.Interval;

        if (!string.IsNullOrEmpty(healthCheck.Timeout))
            hcDict["timeout"] = healthCheck.Timeout;

        if (healthCheck.Retries.HasValue)
            hcDict["retries"] = healthCheck.Retries.Value;

        if (!string.IsNullOrEmpty(healthCheck.StartPeriod))
            hcDict["start_period"] = healthCheck.StartPeriod;

        return hcDict.Count > 0 ? hcDict : null;
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

    /// <summary>
    /// Extract all service names from a manifest, handling both single-stack and multi-stack formats.
    /// For multi-stack manifests, collects services from all stacks (after include resolution).
    /// </summary>
    private static List<string> ExtractServicesFromManifest(RsgoManifest manifest)
    {
        var services = new List<string>();

        // Single-stack: direct services
        if (manifest.Services != null)
        {
            services.AddRange(manifest.Services.Keys);
        }

        // Multi-stack: collect services from all stacks (includes are already resolved)
        if (manifest.Stacks != null)
        {
            foreach (var (_, stackEntry) in manifest.Stacks)
            {
                if (stackEntry.Services != null)
                {
                    services.AddRange(stackEntry.Services.Keys);
                }
            }
        }

        return services.Distinct().ToList();
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
