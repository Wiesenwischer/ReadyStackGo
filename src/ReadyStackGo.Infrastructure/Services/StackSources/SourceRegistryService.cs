using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Infrastructure.Services.StackSources;

/// <summary>
/// Loads the curated source registry from the embedded JSON resource.
/// </summary>
public class SourceRegistryService : ISourceRegistryService
{
    private const string ResourceName = "ReadyStackGo.Infrastructure.Services.StackSources.source-registry.json";

    private readonly Lazy<IReadOnlyList<SourceRegistryEntry>> _entries;
    private readonly ILogger<SourceRegistryService> _logger;

    public SourceRegistryService(ILogger<SourceRegistryService> logger)
    {
        _logger = logger;
        _entries = new Lazy<IReadOnlyList<SourceRegistryEntry>>(LoadRegistry);
    }

    public IReadOnlyList<SourceRegistryEntry> GetAll() => _entries.Value;

    public SourceRegistryEntry? GetById(string id)
        => _entries.Value.FirstOrDefault(e => e.Id == id);

    private IReadOnlyList<SourceRegistryEntry> LoadRegistry()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(ResourceName);

            if (stream is null)
            {
                _logger.LogWarning("Embedded source registry resource not found: {ResourceName}", ResourceName);
                return [];
            }

            var jsonEntries = JsonSerializer.Deserialize<List<RegistryJsonEntry>>(stream);

            if (jsonEntries is null)
            {
                _logger.LogWarning("Failed to deserialize source registry — result was null");
                return [];
            }

            var entries = jsonEntries
                .Select(e => new SourceRegistryEntry(
                    Id: e.Id,
                    Name: e.Name,
                    Description: e.Description,
                    GitUrl: e.GitUrl,
                    GitBranch: e.GitBranch ?? "main",
                    Category: e.Category ?? "community",
                    Tags: (IReadOnlyList<string>)(e.Tags ?? []),
                    Featured: e.Featured,
                    StackCount: e.StackCount))
                .ToList()
                .AsReadOnly();

            _logger.LogInformation("Loaded {Count} entries from source registry", entries.Count);
            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load source registry");
            return [];
        }
    }

    private sealed class RegistryJsonEntry
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("gitUrl")]
        public string GitUrl { get; set; } = string.Empty;

        [JsonPropertyName("gitBranch")]
        public string? GitBranch { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("featured")]
        public bool Featured { get; set; }

        [JsonPropertyName("stackCount")]
        public int StackCount { get; set; }
    }
}
