using FastEndpoints;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Application.Stacks;
using YamlDotNet.Serialization;

namespace ReadyStackGo.API.Endpoints.Stacks;

/// <summary>
/// GET /api/stacks/{id} - Get a specific stack by ID
/// </summary>
public class GetStackEndpoint : Endpoint<GetStackRequest, StackDetailDto>
{
    public IStackSourceService StackSourceService { get; set; } = null!;

    public override void Configure()
    {
        Get("/api/stacks/{Id}");
        Roles("admin", "operator");
    }

    public override async Task HandleAsync(GetStackRequest req, CancellationToken ct)
    {
        var stack = await StackSourceService.GetStackAsync(req.Id, ct);

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

        var sources = await StackSourceService.GetSourcesAsync(ct);
        var sourceName = sources.FirstOrDefault(s => s.Id == stack.SourceId)?.Name ?? stack.SourceId;

        Response = new StackDetailDto
        {
            Id = stack.Id,
            SourceId = stack.SourceId,
            SourceName = sourceName,
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
    /// </summary>
    private static void MergeDictionaries(Dictionary<string, object> baseDict, Dictionary<string, object> overrideDict, string? parentKey = null)
    {
        foreach (var (key, overrideValue) in overrideDict)
        {
            if (baseDict.TryGetValue(key, out var baseValue))
            {
                if (baseValue is Dictionary<object, object> baseSubDict &&
                    overrideValue is Dictionary<object, object> overrideSubDict)
                {
                    var baseConverted = baseSubDict.ToDictionary(k => k.Key.ToString()!, v => v.Value);
                    var overrideConverted = overrideSubDict.ToDictionary(k => k.Key.ToString()!, v => v.Value);
                    MergeDictionaries(baseConverted, overrideConverted, key);
                    baseDict[key] = baseConverted;
                }
                else if (baseValue is IList<object> baseList && overrideValue is IList<object> overrideList)
                {
                    if (ConcatenatedLists.Contains(key))
                    {
                        baseDict[key] = ConcatenateLists(baseList, overrideList);
                    }
                    else if (MergedByKeyLists.Contains(key))
                    {
                        baseDict[key] = MergeKeyValueLists(baseList, overrideList);
                    }
                    else
                    {
                        baseDict[key] = overrideValue;
                    }
                }
                else
                {
                    baseDict[key] = overrideValue;
                }
            }
            else
            {
                baseDict[key] = overrideValue;
            }
        }
    }

    private static List<object> ConcatenateLists(IList<object> baseList, IList<object> overrideList)
    {
        var result = new List<object>(baseList);
        foreach (var item in overrideList)
        {
            if (!result.Any(r => r.ToString() == item.ToString()))
            {
                result.Add(item);
            }
        }
        return result;
    }

    private static List<object> MergeKeyValueLists(IList<object> baseList, IList<object> overrideList)
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var item in baseList)
        {
            var (key, value) = ParseKeyValue(item.ToString() ?? string.Empty);
            merged[key] = value;
        }

        foreach (var item in overrideList)
        {
            var (key, value) = ParseKeyValue(item.ToString() ?? string.Empty);
            merged[key] = value;
        }

        return merged.Select(kv => string.IsNullOrEmpty(kv.Value)
            ? (object)kv.Key
            : (object)$"{kv.Key}={kv.Value}").ToList();
    }

    private static (string Key, string Value) ParseKeyValue(string item)
    {
        if (item.Contains(':') && item.StartsWith('/'))
        {
            var colonIndex = item.IndexOf(':');
            if (colonIndex > 0 && colonIndex < item.Length - 1)
            {
                var rest = item[(colonIndex + 1)..];
                var nextColon = rest.IndexOf(':');
                var containerPath = nextColon > 0 ? rest[..nextColon] : rest;
                return (containerPath, item);
            }
        }

        var parts = item.Split('=', 2);
        var key = parts[0];
        var value = parts.Length > 1 ? parts[1] : string.Empty;
        return (key, value);
    }
}

public class GetStackRequest
{
    public string Id { get; set; } = string.Empty;
}

/// <summary>
/// Detailed stack DTO including YAML content
/// </summary>
public class StackDetailDto
{
    public required string Id { get; init; }
    public required string SourceId { get; init; }
    public required string SourceName { get; init; }
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
