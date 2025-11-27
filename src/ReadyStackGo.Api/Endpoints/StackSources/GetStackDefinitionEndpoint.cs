using FastEndpoints;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Application.Stacks;
using YamlDotNet.Serialization;

namespace ReadyStackGo.API.Endpoints.StackSources;

/// <summary>
/// Get a specific stack definition by ID
/// </summary>
public class GetStackDefinitionEndpoint : Endpoint<GetStackDefinitionRequest, StackDefinitionDetailDto>
{
    public IStackSourceService StackSourceService { get; set; } = null!;

    public override void Configure()
    {
        Get("/api/stack-sources/stacks/{StackId}");
        Roles("admin", "operator");
    }

    public override async Task HandleAsync(GetStackDefinitionRequest req, CancellationToken ct)
    {
        var stack = await StackSourceService.GetStackAsync(req.StackId, ct);

        if (stack == null)
        {
            ThrowError("Stack not found", StatusCodes.Status404NotFound);
            return;
        }

        // Merge override files into the main YAML content
        var mergedYamlContent = stack.YamlContent;
        if (stack.AdditionalFileContents.Count > 0)
        {
            mergedYamlContent = MergeComposeFiles(stack.YamlContent, stack.AdditionalFileContents.Values);
        }

        Response = new StackDefinitionDetailDto
        {
            Id = stack.Id,
            SourceId = stack.SourceId,
            Name = stack.Name,
            Description = stack.Description,
            YamlContent = mergedYamlContent,
            Services = stack.Services,
            Variables = stack.Variables.Select(v => new StackVariableDto
            {
                Name = v.Name,
                DefaultValue = v.DefaultValue,
                IsRequired = v.IsRequired
            }).ToList(),
            FilePath = stack.FilePath,
            AdditionalFiles = stack.AdditionalFiles,
            LastSyncedAt = stack.LastSyncedAt,
            Version = stack.Version
        };
    }

    /// <summary>
    /// Merge multiple compose files using Docker Compose merge semantics.
    /// Override files are merged on top of the base file.
    /// </summary>
    private static string MergeComposeFiles(string baseYaml, IEnumerable<string> overrideYamls)
    {
        var deserializer = new DeserializerBuilder().Build();
        var serializer = new SerializerBuilder()
            .DisableAliases()
            .Build();

        var baseDict = deserializer.Deserialize<Dictionary<string, object>>(baseYaml) ?? new();

        foreach (var overrideYaml in overrideYamls)
        {
            if (string.IsNullOrWhiteSpace(overrideYaml))
                continue;

            var overrideDict = deserializer.Deserialize<Dictionary<string, object>>(overrideYaml);
            if (overrideDict != null)
            {
                MergeDictionaries(baseDict, overrideDict);
            }
        }

        return serializer.Serialize(baseDict);
    }

    // Lists that are concatenated per Docker Compose semantics
    private static readonly HashSet<string> ConcatenatedLists = new(StringComparer.OrdinalIgnoreCase)
    {
        "ports", "expose", "dns", "dns_search", "tmpfs"
    };

    // Lists that are merged by key per Docker Compose semantics
    private static readonly HashSet<string> MergedByKeyLists = new(StringComparer.OrdinalIgnoreCase)
    {
        "environment", "labels", "volumes", "devices"
    };

    /// <summary>
    /// Deep merge two dictionaries using Docker Compose merge semantics.
    /// - Scalars: override replaces
    /// - ports, expose, dns, dns_search, tmpfs: concatenated
    /// - environment, labels, volumes, devices: merged by key
    /// - Nested maps: recursive deep merge
    /// </summary>
    private static void MergeDictionaries(Dictionary<string, object> baseDict, Dictionary<string, object> overrideDict, string? parentKey = null)
    {
        foreach (var (key, overrideValue) in overrideDict)
        {
            if (baseDict.TryGetValue(key, out var baseValue))
            {
                // Both have the key - check if we need to deep merge
                if (baseValue is Dictionary<object, object> baseSubDict &&
                    overrideValue is Dictionary<object, object> overrideSubDict)
                {
                    // Convert to string-keyed dictionaries and merge recursively
                    var baseConverted = baseSubDict.ToDictionary(k => k.Key.ToString()!, v => v.Value);
                    var overrideConverted = overrideSubDict.ToDictionary(k => k.Key.ToString()!, v => v.Value);
                    MergeDictionaries(baseConverted, overrideConverted, key);
                    baseDict[key] = baseConverted;
                }
                else if (baseValue is IList<object> baseList && overrideValue is IList<object> overrideList)
                {
                    // Handle list merging based on Docker Compose semantics
                    if (ConcatenatedLists.Contains(key))
                    {
                        // Concatenate: combine both lists
                        baseDict[key] = ConcatenateLists(baseList, overrideList);
                    }
                    else if (MergedByKeyLists.Contains(key))
                    {
                        // Merge by key: override values take precedence for same key
                        baseDict[key] = MergeKeyValueLists(baseList, overrideList);
                    }
                    else
                    {
                        // Default: override replaces
                        baseDict[key] = overrideValue;
                    }
                }
                else
                {
                    // Override replaces (for scalars)
                    baseDict[key] = overrideValue;
                }
            }
            else
            {
                // Key doesn't exist in base - add it
                baseDict[key] = overrideValue;
            }
        }
    }

    /// <summary>
    /// Concatenate two lists, combining all items from both.
    /// Used for ports, expose, dns, dns_search, tmpfs.
    /// </summary>
    private static List<object> ConcatenateLists(IList<object> baseList, IList<object> overrideList)
    {
        var result = new List<object>(baseList);
        foreach (var item in overrideList)
        {
            // Avoid duplicates for simple values
            if (!result.Any(r => r.ToString() == item.ToString()))
            {
                result.Add(item);
            }
        }
        return result;
    }

    /// <summary>
    /// Merge two key-value lists per Docker Compose semantics.
    /// Override values take precedence for duplicate keys.
    /// Used for environment, labels, volumes, devices.
    /// </summary>
    private static List<object> MergeKeyValueLists(IList<object> baseList, IList<object> overrideList)
    {
        // Use ordered dictionary to maintain insertion order
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);

        // Add base values first
        foreach (var item in baseList)
        {
            var (key, value) = ParseKeyValue(item.ToString() ?? string.Empty);
            merged[key] = value;
        }

        // Override values take precedence
        foreach (var item in overrideList)
        {
            var (key, value) = ParseKeyValue(item.ToString() ?? string.Empty);
            merged[key] = value;
        }

        // Convert back to list format
        return merged.Select(kv => string.IsNullOrEmpty(kv.Value)
            ? (object)kv.Key
            : (object)$"{kv.Key}={kv.Value}").ToList();
    }

    /// <summary>
    /// Parse a key-value string (e.g., "KEY=value" or "KEY" or "/path:/container/path")
    /// </summary>
    private static (string Key, string Value) ParseKeyValue(string item)
    {
        // Handle volume format: /host/path:/container/path:ro
        // The key is the container path (second part)
        if (item.Contains(':') && item.StartsWith('/'))
        {
            // Volume mount - use container path as key for deduplication
            var colonIndex = item.IndexOf(':');
            if (colonIndex > 0 && colonIndex < item.Length - 1)
            {
                var rest = item[(colonIndex + 1)..];
                var nextColon = rest.IndexOf(':');
                var containerPath = nextColon > 0 ? rest[..nextColon] : rest;
                return (containerPath, item); // Use full item as value for reconstruction
            }
        }

        // Standard KEY=value format
        var parts = item.Split('=', 2);
        var key = parts[0];
        var value = parts.Length > 1 ? parts[1] : string.Empty;
        return (key, value);
    }
}

public class GetStackDefinitionRequest
{
    public string StackId { get; set; } = string.Empty;
}

public class StackDefinitionDetailDto
{
    public required string Id { get; init; }
    public required string SourceId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string YamlContent { get; init; }
    public List<string> Services { get; init; } = new();
    public List<StackVariableDto> Variables { get; init; } = new();
    public string? FilePath { get; init; }
    public List<string> AdditionalFiles { get; init; } = new();
    public DateTime LastSyncedAt { get; init; }
    public string? Version { get; init; }
}
