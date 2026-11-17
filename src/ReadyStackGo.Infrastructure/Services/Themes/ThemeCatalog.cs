using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Infrastructure.Services.Themes;

/// <summary>
/// Reads theme packages from the built-in directory and the optional operator directory.
/// The scan result (metadata and file paths, not the CSS content) is cached for
/// <see cref="CacheDuration"/>: there are only a few small files, every page load requests
/// the list and each stylesheet, and changes in the operator directory still take effect
/// without a restart. The CSS content is read from disk on each request.
/// </summary>
public class ThemeCatalog : IThemeCatalog
{
    public const string MetadataFileName = "theme.json";
    public const string CssFileName = "theme.css";
    public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly IOptionsMonitor<ThemeOptions> _options;
    private readonly ILogger<ThemeCatalog> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly object _cacheLock = new();
    private CachedScan? _cache;

    public ThemeCatalog(
        IOptionsMonitor<ThemeOptions> options,
        ILogger<ThemeCatalog> logger,
        TimeProvider? timeProvider = null)
    {
        _options = options;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public ThemeCatalogSnapshot GetThemes(string? installationDefault = null)
    {
        var themes = GetCachedScan().Themes;
        return new ThemeCatalogSnapshot(ResolveDefault(_options.CurrentValue, installationDefault, themes), themes);
    }

    private CachedScan GetCachedScan()
    {
        var now = _timeProvider.GetUtcNow();
        lock (_cacheLock)
        {
            if (_cache is not null && now - _cache.CreatedAt < CacheDuration)
            {
                return _cache;
            }

            var options = _options.CurrentValue;
            var offered = LoadOfferedPackages(options);
            var themes = offered.Select(p => p.Info).ToList();

            _cache = new CachedScan(now, themes, offered);
            return _cache;
        }
    }

    public string? GetThemeCss(string? id)
    {
        // Validate before any file access: the id becomes part of a path.
        if (!ThemeId.IsValid(id))
        {
            return null;
        }

        var package = GetCachedScan().Packages.FirstOrDefault(p => p.Info.Id == id);
        if (package is null)
        {
            return null;
        }

        try
        {
            return File.ReadAllText(package.CssPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Theme package {ThemeId}: could not read {File}", id, package.CssPath);
            return null;
        }
    }

    private List<ThemePackage> LoadOfferedPackages(ThemeOptions options)
    {
        var packages = new Dictionary<string, ThemePackage>(StringComparer.Ordinal);

        foreach (var package in ScanDirectory(ResolveBuiltInPath(options)))
        {
            packages[package.Info.Id] = package;
        }

        // Operator packages replace built-in packages with the same id.
        if (!string.IsNullOrWhiteSpace(options.Path))
        {
            foreach (var package in ScanDirectory(options.Path))
            {
                packages[package.Info.Id] = package;
            }
        }

        var enabled = ParseEnabled(options.Enabled);
        foreach (var unknown in enabled.Where(id => !packages.ContainsKey(id)))
        {
            _logger.LogWarning("Themes:Enabled lists unknown theme {ThemeId}; it is ignored", unknown);
        }

        return packages.Values
            .Where(p => enabled.Count == 0 || enabled.Contains(p.Info.Id))
            .OrderBy(p => p.Info.Order)
            .ThenBy(p => p.Info.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static string ResolveBuiltInPath(ThemeOptions options) =>
        string.IsNullOrWhiteSpace(options.BuiltInPath)
            ? System.IO.Path.Combine(AppContext.BaseDirectory, "wwwroot", "themes")
            : options.BuiltInPath;

    private static HashSet<string> ParseEnabled(string? enabled) =>
        string.IsNullOrWhiteSpace(enabled)
            ? new HashSet<string>(StringComparer.Ordinal)
            : enabled
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.Ordinal);

    private static string? ResolveDefault(
        ThemeOptions options,
        string? installationDefault,
        IReadOnlyList<ThemeInfo> themes)
    {
        foreach (var candidate in new[] { options.Default, installationDefault, ThemeDefaults.BuiltIn })
        {
            var id = candidate?.Trim();
            if (!string.IsNullOrEmpty(id) && themes.Any(t => t.Id == id))
            {
                return id;
            }
        }

        // themes is already sorted by order, then id.
        return themes.Count > 0 ? themes[0].Id : null;
    }

    private IEnumerable<ThemePackage> ScanDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            _logger.LogDebug("Theme directory {Directory} does not exist; skipped", directory);
            return [];
        }

        string[] folders;
        try
        {
            folders = Directory.GetDirectories(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Theme directory {Directory} could not be read; skipped", directory);
            return [];
        }

        var result = new List<ThemePackage>();
        foreach (var folder in folders)
        {
            var package = TryReadPackage(folder);
            if (package is not null)
            {
                result.Add(package);
            }
        }

        return result;
    }

    private ThemePackage? TryReadPackage(string folder)
    {
        var folderName = System.IO.Path.GetFileName(folder);
        if (!ThemeId.IsValid(folderName))
        {
            _logger.LogWarning("Theme package {Folder} skipped: folder name is not a valid theme id", folder);
            return null;
        }

        var metadataPath = System.IO.Path.Combine(folder, MetadataFileName);
        var cssPath = System.IO.Path.Combine(folder, CssFileName);

        if (!File.Exists(metadataPath))
        {
            _logger.LogWarning("Theme package {Folder} skipped: {File} is missing", folder, MetadataFileName);
            return null;
        }

        if (!File.Exists(cssPath))
        {
            _logger.LogWarning("Theme package {Folder} skipped: {File} is missing", folder, CssFileName);
            return null;
        }

        ThemeMetadata? metadata;
        try
        {
            metadata = JsonSerializer.Deserialize<ThemeMetadata>(File.ReadAllText(metadataPath), JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Theme package {Folder} skipped: {File} could not be read", folder, MetadataFileName);
            return null;
        }

        if (metadata is null || metadata.Id != folderName)
        {
            _logger.LogWarning(
                "Theme package {Folder} skipped: id {ThemeId} in {File} does not match the folder name",
                folder, metadata?.Id, MetadataFileName);
            return null;
        }

        var name = string.IsNullOrWhiteSpace(metadata.Name) ? metadata.Id : metadata.Name.Trim();
        var info = new ThemeInfo(metadata.Id, name, metadata.Description?.Trim() ?? "", metadata.Order ?? int.MaxValue);
        return new ThemePackage(info, cssPath);
    }

    private sealed record ThemePackage(ThemeInfo Info, string CssPath);

    private sealed record CachedScan(
        DateTimeOffset CreatedAt,
        IReadOnlyList<ThemeInfo> Themes,
        IReadOnlyList<ThemePackage> Packages);

    private sealed class ThemeMetadata
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public int? Order { get; set; }
    }
}
