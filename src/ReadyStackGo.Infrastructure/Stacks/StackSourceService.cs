using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Stacks;
using ReadyStackGo.Domain.Stacks;

namespace ReadyStackGo.Infrastructure.Stacks;

/// <summary>
/// Service for managing stack sources and syncing stacks from various sources
/// </summary>
public class StackSourceService : IStackSourceService
{
    private readonly ILogger<StackSourceService> _logger;
    private readonly IStackCache _cache;
    private readonly IEnumerable<IStackSourceProvider> _providers;
    private readonly string _configPath;
    private List<StackSource> _sources = new();
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public StackSourceService(
        ILogger<StackSourceService> logger,
        IStackCache cache,
        IEnumerable<IStackSourceProvider> providers)
    {
        _logger = logger;
        _cache = cache;
        _providers = providers;
        _configPath = Path.Combine(AppContext.BaseDirectory, "rsgo.stacks.json");
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;

            await LoadConfigurationAsync(cancellationToken);
            _initialized = true;

            // Auto-sync on first load
            await SyncAllAsync(cancellationToken);
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task LoadConfigurationAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_configPath))
        {
            _logger.LogInformation("No stack source configuration found at {Path}, creating default", _configPath);
            await CreateDefaultConfigurationAsync(cancellationToken);
        }

        try
        {
            var json = await File.ReadAllTextAsync(_configPath, cancellationToken);
            var config = JsonSerializer.Deserialize<StackSourceConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _sources = config?.Sources ?? new List<StackSource>();
            _logger.LogInformation("Loaded {Count} stack sources from configuration", _sources.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load stack source configuration");
            _sources = new List<StackSource>();
        }
    }

    private async Task CreateDefaultConfigurationAsync(CancellationToken cancellationToken)
    {
        var defaultConfig = new StackSourceConfig
        {
            Sources = new List<StackSource>
            {
                new LocalDirectoryStackSource
                {
                    Id = "examples",
                    Name = "Example Stacks",
                    Path = "examples",
                    FilePattern = "*.yml;*.yaml",
                    Enabled = true
                }
            }
        };

        var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(_configPath, json, cancellationToken);
        _logger.LogInformation("Created default stack source configuration");
    }

    private async Task SaveConfigurationAsync(CancellationToken cancellationToken)
    {
        var config = new StackSourceConfig { Sources = _sources };
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(_configPath, json, cancellationToken);
        _logger.LogDebug("Saved stack source configuration");
    }

    public async Task<IEnumerable<StackSource>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _sources.ToList();
    }

    public async Task<StackSource> AddSourceAsync(StackSource source, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (_sources.Any(s => s.Id == source.Id))
        {
            throw new InvalidOperationException($"Source with ID '{source.Id}' already exists");
        }

        _sources.Add(source);
        await SaveConfigurationAsync(cancellationToken);

        // Sync the new source
        await SyncSourceAsync(source.Id, cancellationToken);

        _logger.LogInformation("Added new stack source: {SourceId} ({SourceName})", source.Id, source.Name);
        return source;
    }

    public async Task RemoveSourceAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var source = _sources.FirstOrDefault(s => s.Id == sourceId);
        if (source == null)
        {
            throw new InvalidOperationException($"Source with ID '{sourceId}' not found");
        }

        _sources.Remove(source);
        _cache.RemoveBySource(sourceId);
        await SaveConfigurationAsync(cancellationToken);

        _logger.LogInformation("Removed stack source: {SourceId}", sourceId);
    }

    public async Task<SyncResult> SyncAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var result = new SyncResult
        {
            Success = true,
            StacksLoaded = 0,
            SourcesSynced = 0
        };

        foreach (var source in _sources.Where(s => s.Enabled))
        {
            try
            {
                var sourceResult = await SyncSourceInternalAsync(source, cancellationToken);
                result.StacksLoaded += sourceResult.StacksLoaded;
                result.SourcesSynced++;
                result.Warnings.AddRange(sourceResult.Warnings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync source {SourceId}", source.Id);
                result.Errors.Add($"Failed to sync '{source.Name}': {ex.Message}");
                result.Success = false;
            }
        }

        _logger.LogInformation("Synced {SourceCount} sources, loaded {StackCount} stacks",
            result.SourcesSynced, result.StacksLoaded);

        return result;
    }

    public async Task<SyncResult> SyncSourceAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var source = _sources.FirstOrDefault(s => s.Id == sourceId);
        if (source == null)
        {
            return new SyncResult
            {
                Success = false,
                Errors = { $"Source with ID '{sourceId}' not found" }
            };
        }

        try
        {
            return await SyncSourceInternalAsync(source, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync source {SourceId}", sourceId);
            return new SyncResult
            {
                Success = false,
                Errors = { ex.Message }
            };
        }
    }

    private async Task<SyncResult> SyncSourceInternalAsync(StackSource source, CancellationToken cancellationToken)
    {
        var result = new SyncResult { Success = true, SourcesSynced = 1 };

        // Find provider for this source type
        var provider = _providers.FirstOrDefault(p => p.CanHandle(source));
        if (provider == null)
        {
            result.Success = false;
            result.Errors.Add($"No provider found for source type: {source.GetType().Name}");
            return result;
        }

        // Clear existing stacks from this source
        _cache.RemoveBySource(source.Id);

        // Load stacks from source
        var stacks = await provider.LoadStacksAsync(source, cancellationToken);
        var stackList = stacks.ToList();

        // Add to cache
        _cache.SetMany(stackList);

        // Update source sync time
        source.LastSyncedAt = DateTime.UtcNow;

        result.StacksLoaded = stackList.Count;
        _logger.LogDebug("Synced {Count} stacks from source {SourceId}", stackList.Count, source.Id);

        return result;
    }

    public async Task<IEnumerable<StackDefinition>> GetStacksAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _cache.GetAll();
    }

    public async Task<StackDefinition?> GetStackAsync(string stackId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _cache.Get(stackId);
    }
}

/// <summary>
/// Configuration file structure for stack sources
/// </summary>
internal class StackSourceConfig
{
    public List<StackSource> Sources { get; set; } = new();
}
