namespace ReadyStackGo.Domain.IdentityAccess.ApiKeys;

using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Aggregate root representing an API key for CI/CD pipeline authentication.
/// API keys authenticate webhook requests via X-Api-Key header.
/// </summary>
public class ApiKey : AggregateRoot<ApiKeyId>
{
    public OrganizationId OrganizationId { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string KeyHash { get; private set; } = null!;
    public string KeyPrefix { get; private set; } = null!;
    public Guid? EnvironmentId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastUsedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevokedReason { get; private set; }

    private readonly List<string> _permissions = new();
    public IReadOnlyList<string> Permissions => _permissions.AsReadOnly();

    // For EF Core
    protected ApiKey() { }

    private ApiKey(
        ApiKeyId id,
        OrganizationId organizationId,
        string name,
        string keyHash,
        string keyPrefix,
        IEnumerable<string> permissions,
        Guid? environmentId,
        DateTime? expiresAt)
    {
        SelfAssertArgumentNotNull(id, "ApiKeyId is required.");
        SelfAssertArgumentNotNull(organizationId, "OrganizationId is required.");
        SelfAssertArgumentNotEmpty(name, "API key name is required.");
        SelfAssertArgumentLength(name, 1, 100, "API key name must be 100 characters or less.");
        SelfAssertArgumentNotEmpty(keyHash, "Key hash is required.");
        SelfAssertArgumentLength(keyHash, 64, 64, "Key hash must be exactly 64 characters (SHA-256 hex).");
        SelfAssertArgumentNotEmpty(keyPrefix, "Key prefix is required.");
        SelfAssertArgumentLength(keyPrefix, 1, 12, "Key prefix must be 12 characters or less.");

        var permissionList = permissions?.ToList() ?? new List<string>();
        SelfAssertArgumentTrue(permissionList.Count > 0, "At least one permission is required.");

        Id = id;
        OrganizationId = organizationId;
        Name = name;
        KeyHash = keyHash;
        KeyPrefix = keyPrefix;
        EnvironmentId = environmentId;
        ExpiresAt = expiresAt;
        IsRevoked = false;
        CreatedAt = SystemClock.UtcNow;

        _permissions.AddRange(permissionList);

        AddDomainEvent(new ApiKeyCreated(Id, Name));
    }

    /// <summary>
    /// Creates a new API key.
    /// </summary>
    public static ApiKey Create(
        ApiKeyId id,
        OrganizationId organizationId,
        string name,
        string keyHash,
        string keyPrefix,
        IEnumerable<string> permissions,
        Guid? environmentId = null,
        DateTime? expiresAt = null)
    {
        return new ApiKey(id, organizationId, name, keyHash, keyPrefix, permissions, environmentId, expiresAt);
    }

    /// <summary>
    /// Revokes this API key, permanently disabling it.
    /// </summary>
    public void Revoke(string? reason = null)
    {
        SelfAssertArgumentTrue(!IsRevoked, "API key is already revoked.");

        IsRevoked = true;
        RevokedAt = SystemClock.UtcNow;
        RevokedReason = reason;

        AddDomainEvent(new ApiKeyRevoked(Id, Name, reason));
    }

    /// <summary>
    /// Records a usage timestamp for this API key.
    /// </summary>
    public void RecordUsage()
    {
        LastUsedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Checks if this API key has expired.
    /// </summary>
    public bool IsExpired()
    {
        return ExpiresAt.HasValue && ExpiresAt.Value <= SystemClock.UtcNow;
    }

    /// <summary>
    /// Checks if this API key is valid (not revoked and not expired).
    /// </summary>
    public bool IsValid()
    {
        return !IsRevoked && !IsExpired();
    }

    /// <summary>
    /// Checks if this API key has the specified permission.
    /// Supports wildcards: "*.*" matches all, "Resource.*" matches all actions on a resource.
    /// </summary>
    public bool HasPermission(string permissionString)
    {
        if (string.IsNullOrEmpty(permissionString))
            return false;

        var required = permissionString.Split('.');
        if (required.Length != 2)
            return false;

        var requiredResource = required[0];
        var requiredAction = required[1];

        return _permissions.Any(p =>
        {
            var parts = p.Split('.');
            if (parts.Length != 2) return false;

            var resource = parts[0];
            var action = parts[1];

            if (resource == "*") return true;
            if (resource != requiredResource) return false;
            if (action == "*") return true;
            return action == requiredAction;
        });
    }

    public override string ToString() =>
        $"ApiKey [id={Id}, name={Name}, prefix={KeyPrefix}, revoked={IsRevoked}]";
}
