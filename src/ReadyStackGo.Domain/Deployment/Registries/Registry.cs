namespace ReadyStackGo.Domain.Deployment.Registries;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Aggregate root representing a Docker Registry configuration.
/// Used for authenticating image pulls during deployment.
/// </summary>
public class Registry : AggregateRoot<RegistryId>
{
    public OrganizationId OrganizationId { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Url { get; private set; } = null!;
    public string? Username { get; private set; }
    public string? Password { get; private set; }
    public bool IsDefault { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Image patterns for matching (e.g., "library/*", "ghcr.io/*", "myregistry.com/myorg/*")
    private readonly List<string> _imagePatterns = new();
    public IReadOnlyList<string> ImagePatterns => _imagePatterns.AsReadOnly();

    // For EF Core
    protected Registry() { }

    private Registry(
        RegistryId id,
        OrganizationId organizationId,
        string name,
        string url,
        string? username,
        string? password)
    {
        SelfAssertArgumentNotNull(id, "RegistryId is required.");
        SelfAssertArgumentNotNull(organizationId, "OrganizationId is required.");
        SelfAssertArgumentNotEmpty(name, "Registry name is required.");
        SelfAssertArgumentLength(name, 1, 100, "Registry name must be 100 characters or less.");
        SelfAssertArgumentNotEmpty(url, "Registry URL is required.");

        Id = id;
        OrganizationId = organizationId;
        Name = name;
        Url = NormalizeUrl(url);
        Username = username;
        Password = password;
        IsDefault = false;
        CreatedAt = SystemClock.UtcNow;

        AddDomainEvent(new RegistryCreated(Id, Name, Url));
    }

    /// <summary>
    /// Creates a new Docker Registry configuration.
    /// </summary>
    public static Registry Create(
        RegistryId id,
        OrganizationId organizationId,
        string name,
        string url,
        string? username = null,
        string? password = null)
    {
        return new Registry(id, organizationId, name, url, username, password);
    }

    /// <summary>
    /// Updates the registry credentials.
    /// </summary>
    public void UpdateCredentials(string? username, string? password)
    {
        Username = username;
        Password = password;
        UpdatedAt = SystemClock.UtcNow;

        AddDomainEvent(new RegistryUpdated(Id, Name));
    }

    /// <summary>
    /// Updates the registry name.
    /// </summary>
    public void UpdateName(string name)
    {
        SelfAssertArgumentNotEmpty(name, "Registry name is required.");
        SelfAssertArgumentLength(name, 1, 100, "Registry name must be 100 characters or less.");

        Name = name;
        UpdatedAt = SystemClock.UtcNow;

        AddDomainEvent(new RegistryUpdated(Id, Name));
    }

    /// <summary>
    /// Updates the registry URL.
    /// </summary>
    public void UpdateUrl(string url)
    {
        SelfAssertArgumentNotEmpty(url, "Registry URL is required.");

        Url = NormalizeUrl(url);
        UpdatedAt = SystemClock.UtcNow;

        AddDomainEvent(new RegistryUpdated(Id, Name));
    }

    /// <summary>
    /// Sets the image patterns for this registry.
    /// Patterns use glob-style matching (e.g., "library/*", "ghcr.io/*", "myregistry.com/myorg/*").
    /// </summary>
    public void SetImagePatterns(IEnumerable<string> patterns)
    {
        _imagePatterns.Clear();
        foreach (var pattern in patterns ?? Enumerable.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(pattern))
            {
                _imagePatterns.Add(pattern.Trim());
            }
        }
        UpdatedAt = SystemClock.UtcNow;
        AddDomainEvent(new RegistryUpdated(Id, Name));
    }

    /// <summary>
    /// Adds an image pattern to this registry.
    /// </summary>
    public void AddImagePattern(string pattern)
    {
        SelfAssertArgumentNotEmpty(pattern, "Image pattern is required.");
        var trimmed = pattern.Trim();
        if (!_imagePatterns.Contains(trimmed))
        {
            _imagePatterns.Add(trimmed);
            UpdatedAt = SystemClock.UtcNow;
        }
    }

    /// <summary>
    /// Removes an image pattern from this registry.
    /// </summary>
    public void RemoveImagePattern(string pattern)
    {
        if (_imagePatterns.Remove(pattern.Trim()))
        {
            UpdatedAt = SystemClock.UtcNow;
        }
    }

    /// <summary>
    /// Sets this registry as the default for the organization.
    /// </summary>
    public void SetAsDefault()
    {
        IsDefault = true;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Unsets this registry as the default.
    /// </summary>
    public void UnsetAsDefault()
    {
        IsDefault = false;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Checks if this registry has credentials configured.
    /// </summary>
    public bool HasCredentials => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);

    /// <summary>
    /// Checks if this registry matches the given image reference.
    /// First checks image patterns if configured, then falls back to URL-based matching.
    /// </summary>
    public bool MatchesImage(string imageReference)
    {
        if (string.IsNullOrEmpty(imageReference))
            return false;

        // If image patterns are configured, use pattern matching
        if (_imagePatterns.Count > 0)
        {
            return _imagePatterns.Any(pattern => MatchesPattern(imageReference, pattern));
        }

        // Fallback: URL-based matching
        var registryHost = GetRegistryHost();
        var imageRegistry = ExtractRegistryFromImage(imageReference);

        // Docker Hub matching
        if (IsDockerHub(registryHost))
        {
            return IsDockerHubImage(imageReference);
        }

        // Direct host matching (case-insensitive)
        return string.Equals(registryHost, imageRegistry, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if an image reference matches a glob-style pattern.
    /// Supports * for matching any characters within a segment, and ** for matching across segments.
    /// </summary>
    private static bool MatchesPattern(string imageReference, string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return false;

        // Normalize: remove tag/digest for matching
        var normalizedImage = NormalizeImageForMatching(imageReference);
        var normalizedPattern = pattern.ToLowerInvariant().Trim();

        // Convert glob pattern to regex
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(normalizedPattern)
            .Replace("\\*\\*", ".*")  // ** matches anything including /
            .Replace("\\*", "[^/]*")  // * matches anything except /
            + "$";

        return System.Text.RegularExpressions.Regex.IsMatch(
            normalizedImage,
            regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Normalizes an image reference for pattern matching (removes tag/digest).
    /// </summary>
    private static string NormalizeImageForMatching(string imageReference)
    {
        var normalized = imageReference.ToLowerInvariant().Trim();

        // Remove digest (@sha256:...)
        var atIndex = normalized.IndexOf('@');
        if (atIndex > 0)
            normalized = normalized[..atIndex];

        // Remove tag (:latest, :v1.0, etc.) but not port numbers
        var lastColon = normalized.LastIndexOf(':');
        if (lastColon > 0)
        {
            var afterColon = normalized[(lastColon + 1)..];
            // If it's all digits, it's likely a port number in the registry (e.g., localhost:5000/image)
            // We need to check if there's a / after it
            var slashAfterColon = normalized.IndexOf('/', lastColon);
            if (slashAfterColon < 0 && !afterColon.All(char.IsDigit))
            {
                // No slash after colon and not all digits = it's a tag
                normalized = normalized[..lastColon];
            }
        }

        return normalized;
    }

    /// <summary>
    /// Gets the registry host from the URL.
    /// </summary>
    public string GetRegistryHost()
    {
        var url = Url.TrimEnd('/');

        // Remove protocol if present
        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            url = url[8..];
        else if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            url = url[7..];

        // Return host (possibly with port)
        var slashIndex = url.IndexOf('/');
        return slashIndex > 0 ? url[..slashIndex] : url;
    }

    private static string NormalizeUrl(string url)
    {
        url = url.Trim().TrimEnd('/');

        // Add https:// if no protocol specified
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        return url;
    }

    private static string? ExtractRegistryFromImage(string imageReference)
    {
        if (string.IsNullOrEmpty(imageReference))
            return null;

        // Remove tag/digest
        var atIndex = imageReference.IndexOf('@');
        if (atIndex > 0)
            imageReference = imageReference[..atIndex];

        var colonIndex = imageReference.LastIndexOf(':');
        if (colonIndex > 0 && !imageReference[..colonIndex].Contains('/'))
        {
            // This is just a tag on a simple image name (e.g., nginx:latest)
        }
        else if (colonIndex > 0 && imageReference[(colonIndex + 1)..].All(c => char.IsDigit(c)))
        {
            // Port number, not a tag (e.g., localhost:5000/image)
        }
        else if (colonIndex > 0)
        {
            imageReference = imageReference[..colonIndex];
        }

        // Check if first segment looks like a registry host
        var firstSlash = imageReference.IndexOf('/');
        if (firstSlash < 0)
            return null; // Simple image name like "nginx" - Docker Hub

        var firstSegment = imageReference[..firstSlash];

        // If contains dot or colon, it's a registry host
        if (firstSegment.Contains('.') || firstSegment.Contains(':'))
            return firstSegment;

        // Otherwise it's a Docker Hub namespace (e.g., library/nginx, myuser/myimage)
        return null;
    }

    private static bool IsDockerHub(string host)
    {
        return host.Equals("docker.io", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("index.docker.io", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("registry-1.docker.io", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("registry.hub.docker.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDockerHubImage(string imageReference)
    {
        var registry = ExtractRegistryFromImage(imageReference);

        // No explicit registry = Docker Hub
        if (registry == null)
            return true;

        // Explicit Docker Hub registry
        return IsDockerHub(registry);
    }

    public override string ToString() =>
        $"Registry [id={Id}, name={Name}, url={Url}, isDefault={IsDefault}]";
}
