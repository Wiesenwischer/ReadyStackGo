using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.StackManagement.Sources;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.Infrastructure.Services.StackSources;

/// <summary>
/// Database-backed implementation of IProductSourceService.
/// Uses IStackSourceRepository for persistence and IProductCache for caching.
/// </summary>
public class DatabaseProductSourceService : IProductSourceService
{
    private readonly ILogger<DatabaseProductSourceService> _logger;
    private readonly IStackSourceRepository _repository;
    private readonly IProductCache _cache;
    private readonly IEnumerable<IProductSourceProvider> _providers;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public DatabaseProductSourceService(
        ILogger<DatabaseProductSourceService> logger,
        IStackSourceRepository repository,
        IProductCache cache,
        IEnumerable<IProductSourceProvider> providers)
    {
        _logger = logger;
        _repository = repository;
        _cache = cache;
        _providers = providers;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;

            _initialized = true;

            // Auto-sync on first load
            await SyncAllAsync(cancellationToken);
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<IEnumerable<StackSource>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return await _repository.GetAllAsync(cancellationToken);
    }

    public async Task<StackSource> AddSourceAsync(StackSource source, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (await _repository.ExistsAsync(source.Id, cancellationToken))
        {
            throw new InvalidOperationException($"Source with ID '{source.Id}' already exists");
        }

        await _repository.AddAsync(source, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        // Sync the new source
        await SyncSourceAsync(source.Id.Value, cancellationToken);

        _logger.LogInformation("Added new product source: {SourceId} ({SourceName})", source.Id, source.Name);
        return source;
    }

    public async Task RemoveSourceAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var id = new StackSourceId(sourceId);
        if (!await _repository.ExistsAsync(id, cancellationToken))
        {
            throw new InvalidOperationException($"Source with ID '{sourceId}' not found");
        }

        await _repository.RemoveAsync(id, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        // Clear products from cache for this source
        _cache.RemoveBySource(sourceId);

        _logger.LogInformation("Removed product source: {SourceId}", sourceId);
    }

    public async Task<SyncResult> SyncAllAsync(CancellationToken cancellationToken = default)
    {
        // Don't call EnsureInitializedAsync here to avoid circular sync
        var sources = await _repository.GetEnabledAsync(cancellationToken);

        var totalProductsLoaded = 0;
        var sourcesSynced = 0;
        var errors = new List<string>();
        var warnings = new List<string>();
        var success = true;

        foreach (var source in sources)
        {
            try
            {
                var sourceResult = await SyncSourceInternalAsync(source, cancellationToken);
                totalProductsLoaded += sourceResult.StacksLoaded;
                sourcesSynced++;
                warnings.AddRange(sourceResult.Warnings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync source {SourceId}", source.Id);
                errors.Add($"Failed to sync '{source.Name}': {ex.Message}");
                success = false;
            }
        }

        // Save any updates to sync times
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Synced {SourceCount} sources, loaded {ProductCount} products",
            sourcesSynced, totalProductsLoaded);

        return new SyncResult
        {
            Success = success,
            StacksLoaded = totalProductsLoaded,
            SourcesSynced = sourcesSynced,
            Errors = errors,
            Warnings = warnings
        };
    }

    public async Task<SyncResult> SyncSourceAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var id = new StackSourceId(sourceId);
        var source = await _repository.GetByIdAsync(id, cancellationToken);

        if (source == null)
        {
            return SyncResult.Failed($"Source with ID '{sourceId}' not found");
        }

        try
        {
            var result = await SyncSourceInternalAsync(source, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync source {SourceId}", sourceId);
            return SyncResult.Failed(ex.Message);
        }
    }

    private async Task<SyncResult> SyncSourceInternalAsync(StackSource source, CancellationToken cancellationToken)
    {
        // Find provider for this source type
        var provider = _providers.FirstOrDefault(p => p.CanHandle(source));
        if (provider == null)
        {
            return SyncResult.Failed($"No provider found for source type: {source.Type}");
        }

        // Clear existing products from this source
        _cache.RemoveBySource(source.Id.Value);

        // Load products from source
        var products = await provider.LoadProductsAsync(source, cancellationToken);
        var productList = products.ToList();

        // Add products to cache
        _cache.SetMany(productList);

        // Update source sync time
        source.MarkSynced();
        await _repository.UpdateAsync(source, cancellationToken);

        _logger.LogDebug("Synced {Count} products from source {SourceId}", productList.Count, source.Id);

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

    public async Task<IEnumerable<ProductDefinition>> GetProductVersionsAsync(string groupId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _cache.GetProductVersions(groupId);
    }

    public async Task<IEnumerable<ProductDefinition>> GetAvailableUpgradesAsync(string groupId, string currentVersion, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _cache.GetAvailableUpgrades(groupId, currentVersion);
    }
}
