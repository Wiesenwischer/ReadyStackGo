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
        var path = localSource.Path;

        // Resolve relative paths
        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, path);
        }

        if (!Directory.Exists(path))
        {
            _logger.LogWarning("Directory not found: {Path}", path);
            return stacks;
        }

        // Parse file patterns
        var patterns = localSource.FilePattern.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var files = new List<string>();

        foreach (var pattern in patterns)
        {
            files.AddRange(Directory.GetFiles(path, pattern.Trim(), SearchOption.AllDirectories));
        }

        foreach (var file in files.Distinct())
        {
            try
            {
                var stack = await LoadStackFromFileAsync(file, localSource.Id, cancellationToken);
                if (stack != null)
                {
                    stacks.Add(stack);
                    _logger.LogDebug("Loaded stack {StackId} from {File}", stack.Id, file);
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
            _logger.LogDebug(ex, "Failed to parse YAML structure for {File}", filePath);
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
}
