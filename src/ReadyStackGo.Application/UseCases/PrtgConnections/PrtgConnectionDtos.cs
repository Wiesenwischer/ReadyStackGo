namespace ReadyStackGo.Application.UseCases.PrtgConnections;

public sealed record PrtgConnectionDto(
    string Id,
    string Name,
    string Url,
    bool HasApiToken,          // never echo the token; just say whether one is set
    int? TemplateDeviceId,
    bool VerifyTls,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? LastUsedAt);

public sealed record CreatePrtgConnectionRequest(
    string Name,
    string Url,
    string ApiToken,           // plaintext on the wire, encrypted at rest
    int? TemplateDeviceId,
    bool VerifyTls);

public sealed record UpdatePrtgConnectionRequest(
    string Name,
    string Url,
    string? ApiToken,          // null/empty = keep existing
    int? TemplateDeviceId,
    bool VerifyTls);

public sealed record PrtgConnectionResponse(bool Success, string? Error = null, PrtgConnectionDto? Connection = null);
