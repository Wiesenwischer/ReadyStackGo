namespace ReadyStackGo.Domain.IdentityAccess.ApiKeys;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Event raised when a new API key is created.
/// </summary>
public sealed class ApiKeyCreated : DomainEvent
{
    public ApiKeyId ApiKeyId { get; }
    public string Name { get; }

    public ApiKeyCreated(ApiKeyId apiKeyId, string name)
    {
        ApiKeyId = apiKeyId;
        Name = name;
    }
}

/// <summary>
/// Event raised when an API key is revoked.
/// </summary>
public sealed class ApiKeyRevoked : DomainEvent
{
    public ApiKeyId ApiKeyId { get; }
    public string Name { get; }
    public string? Reason { get; }

    public ApiKeyRevoked(ApiKeyId apiKeyId, string name, string? reason)
    {
        ApiKeyId = apiKeyId;
        Name = name;
        Reason = reason;
    }
}
