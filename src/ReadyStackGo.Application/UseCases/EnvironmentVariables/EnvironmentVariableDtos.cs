namespace ReadyStackGo.Application.UseCases.EnvironmentVariables;

/// <summary>
/// DTO for environment variable.
/// </summary>
public record EnvironmentVariableDto(
    string Key,
    string Value,
    bool IsEncrypted);

/// <summary>
/// Response containing environment variables.
/// </summary>
public record GetEnvironmentVariablesResponse(
    Dictionary<string, string> Variables);

/// <summary>
/// Response for save operation.
/// </summary>
public record SaveEnvironmentVariablesResponse(
    bool Success,
    string? Message = null);
