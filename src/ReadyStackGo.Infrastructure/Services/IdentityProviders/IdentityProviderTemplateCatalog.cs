using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.IdentityProviders;
using ReadyStackGo.Application.Services.Oidc;

namespace ReadyStackGo.Infrastructure.Services.IdentityProviders;

/// <summary>
/// Reads identity provider templates from the embedded built-in set and the optional operator
/// directory (<see cref="IdentityProviderTemplateOptions.Path"/>). A template in the directory
/// replaces a built-in template with the same id; invalid templates are skipped with a warning.
/// The result is cached for <see cref="CacheDuration"/> like the theme catalog.
/// </summary>
public class IdentityProviderTemplateCatalog : IIdentityProviderTemplateCatalog
{
    public const string MetadataFileName = "template.json";
    public const string IconFileName = "icon.svg";
    public const int MaxIconBytes = 64 * 1024;
    public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private const string ResourcePrefix = "IdentityProviderTemplates/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly IOptionsMonitor<IdentityProviderTemplateOptions> _options;
    private readonly ILogger<IdentityProviderTemplateCatalog> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly HashSet<string> _knownKinds;
    private readonly Assembly _builtInAssembly;
    private readonly object _cacheLock = new();
    private CachedScan? _cache;

    public IdentityProviderTemplateCatalog(
        IOptionsMonitor<IdentityProviderTemplateOptions> options,
        ILogger<IdentityProviderTemplateCatalog> logger,
        IEnumerable<IClientRegistrationMethod> registrationMethods,
        TimeProvider? timeProvider = null,
        Assembly? builtInAssembly = null)
    {
        _options = options;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _knownKinds = registrationMethods.Select(m => m.Kind).ToHashSet(StringComparer.Ordinal);
        _builtInAssembly = builtInAssembly ?? typeof(IdentityProviderTemplateCatalog).Assembly;
    }

    public IReadOnlyList<IdentityProviderTemplate> GetTemplates() => GetCachedScan().Templates;

    public IdentityProviderTemplate? GetTemplate(string? id)
    {
        if (!ThemeId.IsValid(id))
        {
            return null;
        }
        return GetCachedScan().Templates.FirstOrDefault(t => t.Id == id);
    }

    public byte[]? GetIcon(string? id)
    {
        if (!ThemeId.IsValid(id))
        {
            return null;
        }
        return GetCachedScan().Icons.TryGetValue(id!, out var icon) ? icon : null;
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

            _cache = Scan(now);
            return _cache;
        }
    }

    private CachedScan Scan(DateTimeOffset now)
    {
        var options = _options.CurrentValue;
        var packages = new Dictionary<string, TemplatePackage>(StringComparer.Ordinal);

        foreach (var package in ReadBuiltIn())
        {
            packages[package.Template.Id] = package;
        }

        if (!string.IsNullOrWhiteSpace(options.Path))
        {
            foreach (var package in ScanDirectory(options.Path))
            {
                packages[package.Template.Id] = package;
            }
        }

        var enabled = ParseEnabled(options.Enabled);
        foreach (var unknown in enabled.Where(id => !packages.ContainsKey(id)))
        {
            _logger.LogWarning("IdentityProviderTemplates:Enabled lists unknown template {TemplateId}; it is ignored", unknown);
        }

        var offered = packages.Values
            .Where(p => enabled.Count == 0 || enabled.Contains(p.Template.Id))
            .OrderBy(p => p.Template.Order)
            .ThenBy(p => p.Template.Id, StringComparer.Ordinal)
            .ToList();

        return new CachedScan(
            now,
            offered.Select(p => p.Template).ToList(),
            offered.Where(p => p.Icon != null).ToDictionary(p => p.Template.Id, p => p.Icon!, StringComparer.Ordinal));
    }

    private static HashSet<string> ParseEnabled(string? enabled) =>
        string.IsNullOrWhiteSpace(enabled)
            ? new HashSet<string>(StringComparer.Ordinal)
            : enabled
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.Ordinal);

    private IEnumerable<TemplatePackage> ReadBuiltIn()
    {
        var resources = _builtInAssembly.GetManifestResourceNames()
            .Select(name => (Resource: name, Path: name.Replace('\\', '/')))
            .Where(r => r.Path.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .ToList();

        var folders = resources
            .Select(r => r.Path[ResourcePrefix.Length..].Split('/'))
            .Where(parts => parts.Length == 2)
            .Select(parts => parts[0])
            .Distinct(StringComparer.Ordinal);

        foreach (var folder in folders)
        {
            byte[]? Read(string file)
            {
                var resource = resources.FirstOrDefault(r => r.Path == $"{ResourcePrefix}{folder}/{file}").Resource;
                if (resource == null)
                {
                    return null;
                }
                using var stream = _builtInAssembly.GetManifestResourceStream(resource);
                if (stream == null)
                {
                    return null;
                }
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                return memory.ToArray();
            }

            var package = TryCreatePackage($"built-in:{folder}", folder, Read(MetadataFileName), Read(IconFileName));
            if (package != null)
            {
                yield return package;
            }
        }
    }

    private IEnumerable<TemplatePackage> ScanDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            _logger.LogDebug("Identity provider template directory {Directory} does not exist; skipped", directory);
            return [];
        }

        string[] folders;
        try
        {
            folders = Directory.GetDirectories(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Identity provider template directory {Directory} could not be read; skipped", directory);
            return [];
        }

        var result = new List<TemplatePackage>();
        foreach (var folder in folders)
        {
            var folderName = System.IO.Path.GetFileName(folder);
            byte[]? metadata = null;
            byte[]? icon = null;
            try
            {
                var metadataPath = System.IO.Path.Combine(folder, MetadataFileName);
                if (File.Exists(metadataPath))
                {
                    metadata = File.ReadAllBytes(metadataPath);
                }

                var iconPath = System.IO.Path.Combine(folder, IconFileName);
                if (File.Exists(iconPath))
                {
                    icon = new FileInfo(iconPath).Length <= MaxIconBytes ? File.ReadAllBytes(iconPath) : TooLarge;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Identity provider template {Folder} could not be read; skipped", folder);
                continue;
            }

            var package = TryCreatePackage(folder, folderName, metadata, icon);
            if (package != null)
            {
                result.Add(package);
            }
        }

        return result;
    }

    // Marker for an icon file that exceeds MaxIconBytes (not read into memory).
    private static readonly byte[] TooLarge = new byte[MaxIconBytes + 1];

    private TemplatePackage? TryCreatePackage(string source, string folderName, byte[]? metadataBytes, byte[]? iconBytes)
    {
        if (!ThemeId.IsValid(folderName))
        {
            _logger.LogWarning("Identity provider template {Source} skipped: folder name is not a valid id", source);
            return null;
        }

        if (metadataBytes == null)
        {
            _logger.LogWarning("Identity provider template {Source} skipped: {File} is missing", source, MetadataFileName);
            return null;
        }

        TemplateMetadata? metadata;
        try
        {
            metadata = JsonSerializer.Deserialize<TemplateMetadata>(metadataBytes, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Identity provider template {Source} skipped: {File} is not valid JSON", source, MetadataFileName);
            return null;
        }

        var error = Validate(metadata, folderName);
        if (error != null)
        {
            _logger.LogWarning("Identity provider template {Source} skipped: {Reason}", source, error);
            return null;
        }

        var icon = ValidateIcon(source, iconBytes);
        var template = ToTemplate(metadata!, icon != null);
        return new TemplatePackage(template, icon);
    }

    private string? Validate(TemplateMetadata? metadata, string folderName)
    {
        if (metadata == null)
        {
            return "template.json is empty";
        }
        if (metadata.Id != folderName)
        {
            return $"id '{metadata.Id}' does not match the folder name";
        }
        if (string.IsNullOrWhiteSpace(metadata.Name))
        {
            return "name is missing";
        }

        var hasUrl = !string.IsNullOrWhiteSpace(metadata.Authority?.Url);
        var hasInput = metadata.Authority?.Input != null;
        if (hasUrl == hasInput)
        {
            return "authority needs exactly one of url and input";
        }
        if (hasUrl && !IsAbsoluteHttpUrl(metadata.Authority!.Url!))
        {
            return "authority.url is not an absolute http(s) address";
        }

        var kind = metadata.Registration?.Kind;
        if (string.IsNullOrWhiteSpace(kind))
        {
            return "registration.kind is missing";
        }
        if (!_knownKinds.Contains(kind))
        {
            return $"registration kind '{kind}' is unknown";
        }

        if (metadata.Scopes != null &&
            !metadata.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("openid", StringComparer.Ordinal))
        {
            return "scopes must contain openid";
        }

        if (metadata.Provider?.Name != null && !ThemeId.IsValid(metadata.Provider.Name))
        {
            return "provider.name is not a valid name";
        }

        return null;
    }

    private byte[]? ValidateIcon(string source, byte[]? iconBytes)
    {
        if (iconBytes == null)
        {
            return null;
        }
        if (iconBytes.Length > MaxIconBytes)
        {
            _logger.LogWarning("Identity provider template {Source}: {File} is larger than 64 KB; shown without icon", source, IconFileName);
            return null;
        }

        var text = Encoding.UTF8.GetString(iconBytes).TrimStart('﻿').TrimStart();
        var isSvg = (text.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) ||
                     text.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)) &&
                    text.Contains("<svg", StringComparison.OrdinalIgnoreCase);
        if (!isSvg)
        {
            _logger.LogWarning("Identity provider template {Source}: {File} is not an SVG; shown without icon", source, IconFileName);
            return null;
        }

        return iconBytes;
    }

    private static bool IsAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static IdentityProviderTemplate ToTemplate(TemplateMetadata m, bool hasIcon)
    {
        var defaults = new OidcClaimNames();
        var description = m.Description?.Trim() ?? string.Empty;
        return new IdentityProviderTemplate
        {
            Id = m.Id!,
            Name = m.Name!.Trim(),
            Description = description,
            SetupDescription = string.IsNullOrWhiteSpace(m.SetupDescription) ? description : m.SetupDescription.Trim(),
            Order = m.Order ?? int.MaxValue,
            ProviderName = string.IsNullOrWhiteSpace(m.Provider?.Name) ? m.Id! : m.Provider.Name,
            ProviderDisplayName = m.Provider?.DisplayName?.Trim() is { Length: > 0 } displayName
                ? displayName
                : m.Authority?.Url != null ? m.Name!.Trim() : null,
            AuthorityUrl = string.IsNullOrWhiteSpace(m.Authority?.Url) ? null : m.Authority.Url.Trim(),
            AuthorityInput = m.Authority?.Input == null
                ? null
                : new TemplateAuthorityInput(m.Authority.Input.Hint, m.Authority.Input.Example),
            RegistrationKind = m.Registration!.Kind!,
            RequirePar = m.RequirePar ?? false,
            RequireHttps = m.RequireHttps ?? false,
            Scopes = string.IsNullOrWhiteSpace(m.Scopes) ? OidcProviderSettings.DefaultScopes : m.Scopes.Trim(),
            Claims = new OidcClaimNames
            {
                Username = string.IsNullOrWhiteSpace(m.Claims?.Username) ? defaults.Username : m.Claims.Username,
                DisplayName = string.IsNullOrWhiteSpace(m.Claims?.DisplayName) ? defaults.DisplayName : m.Claims.DisplayName,
                Email = string.IsNullOrWhiteSpace(m.Claims?.Email) ? defaults.Email : m.Claims.Email
            },
            HelpUrl = string.IsNullOrWhiteSpace(m.HelpUrl) ? null : m.HelpUrl.Trim(),
            OfferInSetup = m.OfferInSetup ?? false,
            HasIcon = hasIcon
        };
    }

    private sealed record TemplatePackage(IdentityProviderTemplate Template, byte[]? Icon);

    private sealed record CachedScan(
        DateTimeOffset CreatedAt,
        IReadOnlyList<IdentityProviderTemplate> Templates,
        IReadOnlyDictionary<string, byte[]> Icons);

    private sealed class TemplateMetadata
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? SetupDescription { get; set; }
        public int? Order { get; set; }
        public ProviderMetadata? Provider { get; set; }
        public AuthorityMetadata? Authority { get; set; }
        public RegistrationMetadata? Registration { get; set; }
        public bool? RequirePar { get; set; }
        public bool? RequireHttps { get; set; }
        public string? Scopes { get; set; }
        public ClaimsMetadata? Claims { get; set; }
        public string? HelpUrl { get; set; }
        public bool? OfferInSetup { get; set; }
    }

    private sealed class ProviderMetadata
    {
        public string? Name { get; set; }
        public string? DisplayName { get; set; }
    }

    private sealed class AuthorityMetadata
    {
        public string? Url { get; set; }
        public AuthorityInputMetadata? Input { get; set; }
    }

    private sealed class AuthorityInputMetadata
    {
        public string? Hint { get; set; }
        public string? Example { get; set; }
    }

    private sealed class RegistrationMetadata
    {
        public string? Kind { get; set; }
    }

    private sealed class ClaimsMetadata
    {
        public string? Username { get; set; }
        public string? DisplayName { get; set; }
        public string? Email { get; set; }
    }
}
