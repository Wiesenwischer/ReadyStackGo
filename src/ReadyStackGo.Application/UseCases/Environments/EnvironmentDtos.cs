namespace ReadyStackGo.Application.UseCases.Environments;

/// <summary>
/// Response DTO for environment information
/// </summary>
public class EnvironmentResponse
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required string ConnectionString { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Request DTO for creating a new environment
/// </summary>
public class CreateEnvironmentRequest
{
    public required string Name { get; set; }
    public required string SocketPath { get; set; }

    // RBAC scope fields
    public string? OrganizationId { get; set; }
}

/// <summary>
/// Response DTO for create environment operation
/// </summary>
public class CreateEnvironmentResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public EnvironmentResponse? Environment { get; set; }
}

/// <summary>
/// Request DTO for updating an environment
/// </summary>
public class UpdateEnvironmentRequest
{
    public required string Name { get; set; }
    public required string SocketPath { get; set; }

    // RBAC scope fields
    public string? OrganizationId { get; set; }
    public string? EnvironmentId { get; set; }
}

/// <summary>
/// Response DTO for update environment operation
/// </summary>
public class UpdateEnvironmentResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public EnvironmentResponse? Environment { get; set; }
}

/// <summary>
/// Response DTO for delete environment operation
/// </summary>
public class DeleteEnvironmentResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Response DTO for list environments operation
/// </summary>
public class ListEnvironmentsResponse
{
    public bool Success { get; set; } = true;
    public List<EnvironmentResponse> Environments { get; set; } = new();
}

/// <summary>
/// Response DTO for set default environment operation
/// </summary>
public class SetDefaultEnvironmentResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}
