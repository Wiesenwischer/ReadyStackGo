using MediatR;
using ReadyStackGo.Application.Services;
using YamlDotNet.Serialization;

namespace ReadyStackGo.Application.UseCases.Stacks.GetStack;

public class GetStackHandler : IRequestHandler<GetStackQuery, GetStackResult?>
{
    private readonly IStackSourceService _stackSourceService;

    public GetStackHandler(IStackSourceService stackSourceService)
    {
        _stackSourceService = stackSourceService;
    }

    public async Task<GetStackResult?> Handle(GetStackQuery request, CancellationToken cancellationToken)
    {
        var stack = await _stackSourceService.GetStackAsync(request.StackId, cancellationToken);

        if (stack == null)
            return null;

        // Merge override files into the main YAML content
        var mergedYamlContent = stack.YamlContent;
        if (stack.AdditionalFileContents.Count > 0)
        {
            mergedYamlContent = MergeComposeFiles(stack.YamlContent, stack.AdditionalFileContents.Values);
        }

        var sources = await _stackSourceService.GetSourcesAsync(cancellationToken);
        var sourceName = sources.FirstOrDefault(s => s.Id.Value == stack.SourceId)?.Name ?? stack.SourceId;

        return new GetStackResult(
            stack.Id,
            stack.SourceId,
            sourceName,
            stack.Name,
            stack.Description,
            mergedYamlContent,
            stack.Services.ToList(),
            stack.Variables.Select(v => new StackVariableItem(v.Name, v.DefaultValue, v.IsRequired)).ToList(),
            stack.FilePath,
            stack.AdditionalFiles.ToList(),
            stack.LastSyncedAt,
            stack.Version
        );
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
