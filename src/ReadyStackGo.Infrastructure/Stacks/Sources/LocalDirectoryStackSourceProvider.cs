using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Stacks;
using ReadyStackGo.Domain.Stacks;
using YamlDotNet.Serialization;

namespace ReadyStackGo.Infrastructure.Stacks.Sources;

/// <summary>
/// Provider that loads stacks from a local directory
/// </summary>
public partial class LocalDirectoryStackSourceProvider : IStackSourceProvider
{
    private readonly ILogger<LocalDirectoryStackSourceProvider> _logger;
    private readonly IDeserializer _yamlDeserializer;

    public string SourceType => "local-directory";

    public LocalDirectoryStackSourceProvider(ILogger<LocalDirectoryStackSourceProvider> logger)
    {
        _logger = logger;
        _yamlDeserializer = new DeserializerBuilder().Build();
    }

    public bool CanHandle(StackSource source)
    {
        return source is LocalDirectoryStackSource;
    }

    public async Task<IEnumerable<StackDefinition>> LoadStacksAsync(StackSource source, CancellationToken cancellationToken = default)
    {
        if (source is not LocalDirectoryStackSource localSource)
        {
            throw new ArgumentException($"Expected LocalDirectoryStackSource but got {source.GetType().Name}");
        }

        var stacks = new List<StackDefinition>();

        // Skip disabled sources
        if (!localSource.Enabled)
        {
            _logger.LogDebug("Stack source {SourceId} is disabled, skipping", localSource.Id);
            return stacks;
        }

        var path = localSource.Path;

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

        // First, look for folder-based stacks (directories containing docker-compose.yml)
        var processedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in Directory.GetDirectories(path))
        {
            var composeFile = Path.Combine(directory, "docker-compose.yml");
            if (!File.Exists(composeFile))
            {
                composeFile = Path.Combine(directory, "docker-compose.yaml");
            }

            if (File.Exists(composeFile))
            {
                try
                {
                    var stack = await LoadStackFromFolderAsync(directory, composeFile, localSource.Id, cancellationToken);
                    if (stack != null)
                    {
                        stacks.Add(stack);
                        processedPaths.Add(directory);
                        _logger.LogDebug("Loaded folder-based stack {StackId} from {Directory}", stack.Id, directory);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load stack from folder {Directory}", directory);
                }
            }
        }

        // Then, look for standalone YAML files (not in processed folders)
        var patterns = localSource.FilePattern.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var files = new List<string>();

        foreach (var pattern in patterns)
        {
            files.AddRange(Directory.GetFiles(path, pattern.Trim(), SearchOption.TopDirectoryOnly));
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
                var stack = await LoadStackFromFileAsync(file, localSource.Id, cancellationToken);
                if (stack != null)
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
    /// Load a stack from a folder containing docker-compose.yml and optional .env/override files
    /// </summary>
    private async Task<StackDefinition?> LoadStackFromFolderAsync(string folderPath, string composeFile, string sourceId, CancellationToken cancellationToken)
    {
        var yamlContent = await File.ReadAllTextAsync(composeFile, cancellationToken);

        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            return null;
        }

        // Stack name is the folder name
        var stackName = Path.GetFileName(folderPath);

        // Look for additional files
        var additionalFiles = new List<string>();
        var additionalFileContents = new Dictionary<string, string>();

        // Check for docker-compose.override.yml
        var overrideFile = Path.Combine(folderPath, "docker-compose.override.yml");
        if (!File.Exists(overrideFile))
        {
            overrideFile = Path.Combine(folderPath, "docker-compose.override.yaml");
        }
        if (File.Exists(overrideFile))
        {
            var overrideContent = await File.ReadAllTextAsync(overrideFile, cancellationToken);
            additionalFiles.Add(Path.GetFileName(overrideFile));
            additionalFileContents[Path.GetFileName(overrideFile)] = overrideContent;
            _logger.LogDebug("Found override file for stack {StackName}", stackName);
        }

        // Check for .env file and parse default values
        var envFile = Path.Combine(folderPath, ".env");
        var envDefaults = new Dictionary<string, string>();
        if (File.Exists(envFile))
        {
            envDefaults = await ParseEnvFileAsync(envFile, cancellationToken);
            _logger.LogDebug("Found .env file for stack {StackName} with {Count} variables", stackName, envDefaults.Count);
        }

        // Parse YAML to extract services and metadata
        var services = new List<string>();
        string? description = null;

        try
        {
            var yaml = _yamlDeserializer.Deserialize<Dictionary<string, object>>(yamlContent);

            if (yaml != null && yaml.TryGetValue("services", out var servicesObj) && servicesObj is Dictionary<object, object> servicesDict)
            {
                services = servicesDict.Keys.Select(k => k.ToString()!).ToList();
            }

            description = ExtractDescription(yamlContent);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse YAML structure for {Folder}", folderPath);
        }

        // Extract variables from main YAML and override files
        var allYamlContent = yamlContent + string.Join("\n", additionalFileContents.Values);
        var variables = ExtractVariables(allYamlContent);

        // Apply .env defaults to variables
        // .env values have higher priority than YAML inline defaults (Docker Compose semantics)
        for (var i = 0; i < variables.Count; i++)
        {
            var variable = variables[i];
            if (envDefaults.TryGetValue(variable.Name, out var envDefault))
            {
                // .env value overrides YAML default (higher priority per Docker Compose)
                variables[i] = variable with { DefaultValue = envDefault, IsRequired = false };
            }
        }

        // Add any .env variables that weren't found in YAML (informational)
        foreach (var envVar in envDefaults)
        {
            if (!variables.Any(v => v.Name == envVar.Key))
            {
                variables.Add(new StackVariable
                {
                    Name = envVar.Key,
                    DefaultValue = envVar.Value,
                    IsRequired = false,
                    Description = "From .env file"
                });
            }
        }

        // Calculate version hash including all files
        var allContent = yamlContent + string.Join("", additionalFileContents.Values) + string.Join("", envDefaults.Select(kv => $"{kv.Key}={kv.Value}"));
        var version = ComputeHash(allContent);

        return new StackDefinition
        {
            Id = $"{sourceId}:{stackName}",
            SourceId = sourceId,
            Name = stackName,
            Description = description,
            YamlContent = yamlContent,
            Variables = variables.OrderBy(v => v.Name).ToList(),
            Services = services,
            FilePath = composeFile,
            AdditionalFiles = additionalFiles,
            AdditionalFileContents = additionalFileContents,
            LastSyncedAt = DateTime.UtcNow,
            Version = version
        };
    }

    /// <summary>
    /// Parse a .env file and return key-value pairs
    /// </summary>
    private async Task<Dictionary<string, string>> ParseEnvFileAsync(string envFilePath, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>();
        var lines = await File.ReadAllLinesAsync(envFilePath, cancellationToken);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Skip empty lines and comments
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
            {
                continue;
            }

            var equalsIndex = trimmed.IndexOf('=');
            if (equalsIndex > 0)
            {
                var key = trimmed[..equalsIndex].Trim();
                var value = trimmed[(equalsIndex + 1)..].Trim();

                // Remove quotes if present
                if ((value.StartsWith('"') && value.EndsWith('"')) ||
                    (value.StartsWith('\'') && value.EndsWith('\'')))
                {
                    value = value[1..^1];
                }

                result[key] = value;
            }
        }

        return result;
    }

    private async Task<StackDefinition?> LoadStackFromFileAsync(string filePath, string sourceId, CancellationToken cancellationToken)
    {
        var yamlContent = await File.ReadAllTextAsync(filePath, cancellationToken);

        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            return null;
        }

        // Extract stack name from filename
        var stackName = Path.GetFileNameWithoutExtension(filePath);

        // Try to parse YAML to extract services and metadata
        var services = new List<string>();
        string? description = null;

        try
        {
            var yaml = _yamlDeserializer.Deserialize<Dictionary<string, object>>(yamlContent);

            if (yaml != null && yaml.TryGetValue("services", out var servicesObj) && servicesObj is Dictionary<object, object> servicesDict)
            {
                services = servicesDict.Keys.Select(k => k.ToString()!).ToList();
            }

            // Extract description from top-level comment
            description = ExtractDescription(yamlContent);
        }
        catch (Exception ex)
        {
            // Invalid YAML - skip this file entirely
            _logger.LogWarning(ex, "Invalid YAML in file {File}, skipping", filePath);
            return null;
        }

        // Extract variables
        var variables = ExtractVariables(yamlContent);

        // Calculate version hash
        var version = ComputeHash(yamlContent);

        return new StackDefinition
        {
            Id = $"{sourceId}:{stackName}",
            SourceId = sourceId,
            Name = stackName,
            Description = description,
            YamlContent = yamlContent,
            Variables = variables,
            Services = services,
            FilePath = filePath,
            LastSyncedAt = DateTime.UtcNow,
            Version = version
        };
    }

    private static string? ExtractDescription(string yamlContent)
    {
        // Look for description in first comment block
        var lines = yamlContent.Split('\n');
        var descriptions = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#'))
            {
                var comment = trimmed.TrimStart('#').Trim();
                if (!string.IsNullOrEmpty(comment) && !comment.StartsWith("vim:") && !comment.Contains("yaml"))
                {
                    descriptions.Add(comment);
                }
            }
            else if (!string.IsNullOrWhiteSpace(trimmed))
            {
                break;
            }
        }

        return descriptions.Count > 0 ? string.Join(" ", descriptions) : null;
    }

    private static List<StackVariable> ExtractVariables(string yamlContent)
    {
        var variables = new Dictionary<string, StackVariable>();

        // Match ${VAR} and ${VAR:-default} patterns
        var regex = VariableRegex();
        var matches = regex.Matches(yamlContent);

        foreach (Match match in matches)
        {
            var varName = match.Groups[1].Value;
            var defaultValue = match.Groups[3].Success ? match.Groups[3].Value : null;

            if (!variables.ContainsKey(varName))
            {
                variables[varName] = new StackVariable
                {
                    Name = varName,
                    DefaultValue = defaultValue,
                    IsRequired = defaultValue == null
                };
            }
            else if (defaultValue != null && variables[varName].DefaultValue == null)
            {
                // Update with default if we find one
                variables[varName] = variables[varName] with { DefaultValue = defaultValue, IsRequired = false };
            }
        }

        return variables.Values.OrderBy(v => v.Name).ToList();
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    [GeneratedRegex(@"\$\{([A-Z_][A-Z0-9_]*)(:-([^}]*))?\}", RegexOptions.IgnoreCase)]
    private static partial Regex VariableRegex();

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
}
