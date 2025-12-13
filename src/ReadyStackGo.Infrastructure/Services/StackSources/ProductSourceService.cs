using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.StackManagement.Sources;
using ReadyStackGo.Domain.StackManagement.Stacks;
using ReadyStackGo.Infrastructure.Configuration;

namespace ReadyStackGo.Infrastructure.Services.StackSources;

/// <summary>
/// Service for managing product sources and syncing products from various sources.
/// This is an infrastructure service - it only manages the cache and providers.
/// Event publishing is handled by the Application layer (use cases).
/// </summary>
public class ProductSourceService : IProductSourceService
{
    private readonly ILogger<ProductSourceService> _logger;
    private readonly IProductCache _cache;
    private readonly IEnumerable<IProductSourceProvider> _providers;
    private readonly string _configPath;
    private List<StackSourceEntry> _sourceEntries = new();
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public ProductSourceService(
        ILogger<ProductSourceService> logger,
        IProductCache cache,
        IEnumerable<IProductSourceProvider> providers)
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
            _logger.LogInformation("No product source configuration found at {Path}, creating default", _configPath);
            await CreateDefaultConfigurationAsync(cancellationToken);
        }

        try
        {
            var json = await File.ReadAllTextAsync(_configPath, cancellationToken);
            var config = JsonSerializer.Deserialize<StackSourceConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _sourceEntries = config?.Sources ?? new List<StackSourceEntry>();
            _logger.LogInformation("Loaded {Count} product sources from configuration", _sourceEntries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load product source configuration");
            _sourceEntries = new List<StackSourceEntry>();
        }
    }

    private async Task CreateDefaultConfigurationAsync(CancellationToken cancellationToken)
    {
        var defaultConfig = new StackSourceConfig
        {
            Sources = new List<StackSourceEntry>
            {
                new LocalDirectorySourceEntry
                {
                    Id = "stacks",
                    Name = "Local",
                    Path = "stacks",
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
        _logger.LogInformation("Created default product source configuration");
    }

    private async Task SaveConfigurationAsync(CancellationToken cancellationToken)
    {
        var config = new StackSourceConfig { Sources = _sourceEntries };
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(_configPath, json, cancellationToken);
        _logger.LogDebug("Saved product source configuration");
    }

    public async Task<IEnumerable<StackSource>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _sourceEntries.Select(ConvertToStackSource).ToList();
    }

    private static StackSource ConvertToStackSource(StackSourceEntry entry)
    {
        var id = new StackSourceId(entry.Id);

        return entry switch
        {
            LocalDirectorySourceEntry local => StackSource.CreateLocalDirectory(
                id,
                local.Name,
                local.Path,
                local.FilePattern),

            GitRepositorySourceEntry git => StackSource.CreateGitRepository(
                id,
                git.Name,
                git.GitUrl,
                git.Branch,
                git.Path,
                git.FilePattern),

            _ => throw new NotSupportedException($"Unknown source entry type: {entry.GetType().Name}")
        };
    }

    public async Task<StackSource> AddSourceAsync(StackSource source, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (_sourceEntries.Any(s => s.Id == source.Id.Value))
        {
            throw new InvalidOperationException($"Source with ID '{source.Id}' already exists");
        }

        var entry = ConvertToSourceEntry(source);
        _sourceEntries.Add(entry);
        await SaveConfigurationAsync(cancellationToken);

        // Sync the new source
        await SyncSourceAsync(source.Id.Value, cancellationToken);

        _logger.LogInformation("Added new product source: {SourceId} ({SourceName})", source.Id, source.Name);
        return source;
    }

    private static StackSourceEntry ConvertToSourceEntry(StackSource source)
    {
        return source.Type switch
        {
            StackSourceType.LocalDirectory => new LocalDirectorySourceEntry
            {
                Id = source.Id.Value,
                Name = source.Name,
                Path = source.Path!,
                FilePattern = source.FilePattern ?? "*.yml;*.yaml",
                Enabled = source.Enabled,
                LastSyncedAt = source.LastSyncedAt
            },

            StackSourceType.GitRepository => new GitRepositorySourceEntry
            {
                Id = source.Id.Value,
                Name = source.Name,
                GitUrl = source.GitUrl!,
                Branch = source.GitBranch ?? "main",
                Path = source.Path,
                FilePattern = source.FilePattern ?? "*.yml;*.yaml",
                Enabled = source.Enabled,
                LastSyncedAt = source.LastSyncedAt
            },

            _ => throw new NotSupportedException($"Unknown source type: {source.Type}")
        };
    }

    public async Task RemoveSourceAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var entry = _sourceEntries.FirstOrDefault(s => s.Id == sourceId);
        if (entry == null)
        {
            throw new InvalidOperationException($"Source with ID '{sourceId}' not found");
        }

        _sourceEntries.Remove(entry);
        _cache.RemoveBySource(sourceId);
        await SaveConfigurationAsync(cancellationToken);

        _logger.LogInformation("Removed product source: {SourceId}", sourceId);
    }

    public async Task<SyncResult> SyncAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var totalProductsLoaded = 0;
        var sourcesSynced = 0;
        var errors = new List<string>();
        var warnings = new List<string>();
        var success = true;

        foreach (var entry in _sourceEntries.Where(s => s.Enabled))
        {
            try
            {
                var sourceResult = await SyncSourceInternalAsync(entry, cancellationToken);
                totalProductsLoaded += sourceResult.StacksLoaded; // Using StacksLoaded for products count
                sourcesSynced++;
                warnings.AddRange(sourceResult.Warnings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync source {SourceId}", entry.Id);
                errors.Add($"Failed to sync '{entry.Name}': {ex.Message}");
                success = false;
            }
        }

        _logger.LogInformation("Synced {SourceCount} sources, loaded {ProductCount} products",
            sourcesSynced, totalProductsLoaded);

        return new SyncResult
        {
            Success = success,
            StacksLoaded = totalProductsLoaded, // Using StacksLoaded for products count
            SourcesSynced = sourcesSynced,
            Errors = errors,
            Warnings = warnings
        };
    }

    public async Task<SyncResult> SyncSourceAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var entry = _sourceEntries.FirstOrDefault(s => s.Id == sourceId);
        if (entry == null)
        {
            return SyncResult.Failed($"Source with ID '{sourceId}' not found");
        }

        try
        {
            return await SyncSourceInternalAsync(entry, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync source {SourceId}", sourceId);
            return SyncResult.Failed(ex.Message);
        }
    }

    private async Task<SyncResult> SyncSourceInternalAsync(StackSourceEntry entry, CancellationToken cancellationToken)
    {
        // Convert to domain type for provider
        var source = ConvertToStackSource(entry);

        // Find provider for this source type
        var provider = _providers.FirstOrDefault(p => p.CanHandle(source));
        if (provider == null)
        {
            return SyncResult.Failed($"No provider found for source type: {entry.GetType().Name}");
        }

        // Clear existing products from this source
        _cache.RemoveBySource(entry.Id);

        // Load products from source
        var products = await provider.LoadProductsAsync(source, cancellationToken);
        var productList = products.ToList();

        // Add products to cache
        _cache.SetMany(productList);

        // Update source sync time
        entry.LastSyncedAt = DateTime.UtcNow;

        _logger.LogDebug("Synced {Count} products from source {SourceId}", productList.Count, entry.Id);

        return SyncResult.Successful(productList.Count, 1);
    }

    public async Task<IEnumerable<ProductDefinition>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _cache.GetAllProducts().OrderBy(p => p.DisplayName);
    }

    public async Task<ProductDefinition?> GetProductAsync(string productId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _cache.GetProduct(productId);
    }

    public async Task<IEnumerable<StackDefinition>> GetStacksAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _cache.GetAllStacks();
    }

    public async Task<StackDefinition?> GetStackAsync(string stackId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _cache.GetStack(stackId);
    }
}
