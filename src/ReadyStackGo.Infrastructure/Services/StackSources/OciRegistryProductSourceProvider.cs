using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.StackManagement.Sources;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.Infrastructure.Services.StackSources;

/// <summary>
/// Provider that loads products from an OCI container registry.
/// Syncs tags from a Docker Registry v2 API, downloads manifest layers,
/// and delegates YAML parsing to LocalDirectoryProductSourceProvider.
/// </summary>
public class OciRegistryProductSourceProvider : IProductSourceProvider
{
    private readonly OciRegistryClient _registryClient;
    private readonly LocalDirectoryProductSourceProvider _localDirectoryProvider;
    private readonly ILogger<OciRegistryProductSourceProvider> _logger;

    public string SourceType => "oci-registry";

    public OciRegistryProductSourceProvider(
        OciRegistryClient registryClient,
        LocalDirectoryProductSourceProvider localDirectoryProvider,
        ILogger<OciRegistryProductSourceProvider> logger)
    {
        _registryClient = registryClient;
        _localDirectoryProvider = localDirectoryProvider;
        _logger = logger;
    }

    public bool CanHandle(StackSource source) => source.Type == StackSourceType.OciRegistry;

    public async Task<IEnumerable<ProductDefinition>> LoadProductsAsync(
        StackSource source,
        CancellationToken cancellationToken = default)
    {
        if (source.Type != StackSourceType.OciRegistry)
            throw new ArgumentException($"Expected OciRegistry source but got {source.Type}");

        if (!source.Enabled)
        {
            _logger.LogDebug("Product source {SourceId} is disabled, skipping", source.Id);
            return Enumerable.Empty<ProductDefinition>();
        }

        var registryHost = source.RegistryUrl!;
        var repository = source.Repository!;
        var tagPattern = source.TagPattern ?? "*";

        _logger.LogInformation("Syncing OCI registry source {SourceId}: {Host}/{Repo} (pattern: {Pattern})",
            source.Id, registryHost, repository, tagPattern);

        // 1. List tags from registry
        var allTags = await _registryClient.ListTagsAsync(
            registryHost, repository, source.RegistryUsername, source.RegistryPassword, cancellationToken);

        if (allTags.Count == 0)
        {
            _logger.LogWarning("No tags found for {Host}/{Repo}", registryHost, repository);
            return Enumerable.Empty<ProductDefinition>();
        }

        // 2. Filter tags by glob pattern
        var matchingTags = FilterTagsByGlob(allTags, tagPattern);
        _logger.LogDebug("Matched {Count}/{Total} tags with pattern '{Pattern}'",
            matchingTags.Count, allTags.Count, tagPattern);

        if (matchingTags.Count == 0)
            return Enumerable.Empty<ProductDefinition>();

        // 3. Download and cache manifests for each tag
        var cacheRoot = GetCacheDirectory(source.Id.Value);
        var products = new List<ProductDefinition>();

        foreach (var tag in matchingTags)
        {
            try
            {
                var product = await LoadProductFromTagAsync(
                    source, registryHost, repository, tag, cacheRoot, cancellationToken);

                if (product != null)
                    products.Add(product);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load product from tag {Tag} in {Host}/{Repo}",
                    tag, registryHost, repository);
            }
        }

        _logger.LogInformation("Loaded {Count} products from OCI registry {Host}/{Repo}",
            products.Count, registryHost, repository);

        return products;
    }

    /// <summary>
    /// Loads a product from a single OCI tag by downloading the manifest and extracting stack files.
    /// </summary>
    private async Task<ProductDefinition?> LoadProductFromTagAsync(
        StackSource source,
        string registryHost,
        string repository,
        string tag,
        string cacheRoot,
        CancellationToken ct)
    {
        var tagCacheDir = Path.Combine(cacheRoot, tag);

        // Check if cached manifest digest matches (skip re-download)
        var manifest = await _registryClient.GetManifestAsync(
            registryHost, repository, tag, source.RegistryUsername, source.RegistryPassword, ct);

        if (manifest == null)
        {
            _logger.LogDebug("No manifest for tag {Tag}", tag);
            return null;
        }

        var digestFile = Path.Combine(tagCacheDir, ".digest");
        if (File.Exists(digestFile) && manifest.Digest != null)
        {
            var cachedDigest = await File.ReadAllTextAsync(digestFile, ct);
            if (cachedDigest.Trim() == manifest.Digest && Directory.GetFiles(tagCacheDir, "*.yml").Length > 0)
            {
                _logger.LogDebug("Tag {Tag} unchanged (digest: {Digest}), using cache", tag, manifest.Digest);
                return await LoadFromCacheAsync(source, tagCacheDir, ct);
            }
        }

        // Parse manifest to find stack layer
        var stackYaml = await ExtractStackYamlFromManifestAsync(
            manifest, registryHost, repository, source.RegistryUsername, source.RegistryPassword, ct);

        if (stackYaml == null)
        {
            _logger.LogDebug("No stack YAML found in manifest for tag {Tag}", tag);
            return null;
        }

        // Write to cache
        Directory.CreateDirectory(tagCacheDir);
        var stackFile = Path.Combine(tagCacheDir, "stack.yml");
        await File.WriteAllTextAsync(stackFile, stackYaml, ct);

        if (manifest.Digest != null)
            await File.WriteAllTextAsync(digestFile, manifest.Digest, ct);

        return await LoadFromCacheAsync(source, tagCacheDir, ct);
    }

    /// <summary>
    /// Loads a product from the cached directory using the local directory provider.
    /// </summary>
    private async Task<ProductDefinition?> LoadFromCacheAsync(
        StackSource source,
        string cacheDir,
        CancellationToken ct)
    {
        var tempSource = StackSource.CreateLocalDirectory(
            source.Id, source.Name, cacheDir, "*.yml;*.yaml");

        var products = await _localDirectoryProvider.LoadProductsAsync(tempSource, ct);
        return products.FirstOrDefault();
    }

    /// <summary>
    /// Extracts stack YAML from an OCI manifest by finding and pulling the appropriate layer.
    /// Supports both custom RSGO media types and standard Docker images with /rsgo/ paths.
    /// </summary>
    private async Task<string?> ExtractStackYamlFromManifestAsync(
        OciManifestResult manifest,
        string registryHost,
        string repository,
        string? username,
        string? password,
        CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(manifest.Content);
            var root = doc.RootElement;

            // Try to find layers with RSGO-specific media types first
            if (root.TryGetProperty("layers", out var layers) && layers.ValueKind == JsonValueKind.Array)
            {
                foreach (var layer in layers.EnumerateArray())
                {
                    var mediaType = layer.TryGetProperty("mediaType", out var mt) ? mt.GetString() : null;
                    var digest = layer.TryGetProperty("digest", out var d) ? d.GetString() : null;

                    if (digest == null) continue;

                    // Check for RSGO-specific media type
                    if (mediaType is "application/vnd.rsgo.stack.manifest.v1+yaml"
                        or "application/vnd.rsgo.stack.v1+yaml")
                    {
                        var blob = await _registryClient.PullBlobAsync(
                            registryHost, repository, digest, username, password, ct);
                        return blob != null ? System.Text.Encoding.UTF8.GetString(blob) : null;
                    }
                }

                // Fallback: pull the config blob and check for /rsgo/ structure,
                // or pull layers and look for stack.yml in tar.gz
                foreach (var layer in layers.EnumerateArray())
                {
                    var digest = layer.TryGetProperty("digest", out var d) ? d.GetString() : null;
                    var mediaType = layer.TryGetProperty("mediaType", out var mt) ? mt.GetString() : null;

                    if (digest == null) continue;

                    // Standard Docker image layer (tar.gz) — extract /rsgo/stack.yml
                    if (mediaType is "application/vnd.docker.image.rootfs.diff.tar.gzip"
                        or "application/vnd.oci.image.layer.v1.tar+gzip")
                    {
                        var yaml = await ExtractYamlFromTarGzLayerAsync(
                            registryHost, repository, digest, username, password, ct);
                        if (yaml != null) return yaml;
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse OCI manifest JSON");
        }

        return null;
    }

    /// <summary>
    /// Extracts stack.yml from a tar.gz layer by looking for /rsgo/stack.yml or stack.yml at root.
    /// </summary>
    private async Task<string?> ExtractYamlFromTarGzLayerAsync(
        string registryHost,
        string repository,
        string digest,
        string? username,
        string? password,
        CancellationToken ct)
    {
        var blob = await _registryClient.PullBlobAsync(
            registryHost, repository, digest, username, password, ct);

        if (blob == null) return null;

        try
        {
            using var ms = new MemoryStream(blob);
            using var gzip = new GZipStream(ms, CompressionMode.Decompress);
            using var tar = new System.Formats.Tar.TarReader(gzip);

            while (await tar.GetNextEntryAsync(cancellationToken: ct) is { } entry)
            {
                var name = entry.Name.TrimStart('.', '/');

                if (name is "rsgo/stack.yml" or "rsgo/stack.yaml" or "stack.yml" or "stack.yaml")
                {
                    if (entry.DataStream == null) continue;
                    using var reader = new StreamReader(entry.DataStream);
                    return await reader.ReadToEndAsync(ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to extract YAML from tar.gz layer {Digest}", digest);
        }

        return null;
    }

    /// <summary>
    /// Filters tags by a glob pattern (e.g., "v*", "ams-*").
    /// Supports * (any chars) and ? (single char) wildcards.
    /// </summary>
    internal static IReadOnlyList<string> FilterTagsByGlob(IReadOnlyList<string> tags, string pattern)
    {
        if (pattern == "*")
            return tags;

        // Convert glob to regex
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        var regex = new System.Text.RegularExpressions.Regex(regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return tags.Where(t => regex.IsMatch(t)).ToList();
    }

    private static string GetCacheDirectory(string sourceId)
    {
        var cacheRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".rsgo", "oci-cache", sourceId);
        Directory.CreateDirectory(cacheRoot);
        return cacheRoot;
    }
}
