namespace ReadyStackGo.Application.UseCases.Registries;

/// <summary>
/// DTO for registry information.
/// </summary>
public record RegistryDto(
    string Id,
    string Name,
    string Url,
    string? Username,
    bool HasCredentials,
    bool IsDefault,
    IReadOnlyList<string> ImagePatterns,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// Request for creating a new registry.
/// </summary>
public record CreateRegistryRequest(
    string Name,
    string Url,
    string? Username,
    string? Password,
    IReadOnlyList<string>? ImagePatterns = null);

/// <summary>
/// Request for updating a registry.
/// </summary>
public record UpdateRegistryRequest(
    string? Name,
    string? Url,
    string? Username,
    string? Password,
    bool? ClearCredentials,
    IReadOnlyList<string>? ImagePatterns = null);

/// <summary>
/// Response for registry operations.
/// </summary>
public record RegistryResponse(
    bool Success,
    string? Message = null,
    RegistryDto? Registry = null);

/// <summary>
/// Response for listing registries.
/// </summary>
public record ListRegistriesResponse(
    IReadOnlyList<RegistryDto> Registries);

/// <summary>
/// Request for testing registry connection.
/// </summary>
public record TestRegistryConnectionRequest(
    string Url,
    string? Username,
    string? Password);

/// <summary>
/// Response for testing registry connection.
/// </summary>
public record TestRegistryConnectionResponse(
    bool Success,
    string Message);
