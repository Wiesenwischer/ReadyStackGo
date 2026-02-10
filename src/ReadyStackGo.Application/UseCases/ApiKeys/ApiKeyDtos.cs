namespace ReadyStackGo.Application.UseCases.ApiKeys;

/// <summary>
/// DTO for returning API key data (without the full key).
/// </summary>
public record ApiKeyDto(
    string Id,
    string Name,
    string KeyPrefix,
    string OrganizationId,
    string? EnvironmentId,
    IReadOnlyList<string> Permissions,
    DateTime CreatedAt,
    DateTime? LastUsedAt,
    DateTime? ExpiresAt,
    bool IsRevoked);

/// <summary>
/// DTO returned only once after API key creation (includes the full key).
/// </summary>
public record ApiKeyCreatedDto(
    string Id,
    string Name,
    string KeyPrefix,
    string FullKey);

/// <summary>
/// Request to create a new API key.
/// </summary>
public record CreateApiKeyRequest
{
    public string Name { get; init; } = null!;
    public string? EnvironmentId { get; init; }
    public List<string> Permissions { get; init; } = new();
    public DateTime? ExpiresAt { get; init; }
}

/// <summary>
/// Response for API key creation.
/// </summary>
public record CreateApiKeyResponse(
    bool Success,
    string? Message = null,
    ApiKeyCreatedDto? ApiKey = null);

/// <summary>
/// Response for listing API keys.
/// </summary>
public record ListApiKeysResponse(
    IReadOnlyList<ApiKeyDto> ApiKeys);

/// <summary>
/// Request to revoke an API key.
/// </summary>
public record RevokeApiKeyRequest
{
    public string ApiKeyId { get; init; } = string.Empty;
    public string? Reason { get; init; }
}

/// <summary>
/// Response for revoking an API key.
/// </summary>
public record RevokeApiKeyResponse(
    bool Success,
    string? Message = null);
