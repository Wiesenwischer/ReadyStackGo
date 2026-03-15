using ReadyStackGo.Domain.Deployment.Environments;
using DomainEnvironment = ReadyStackGo.Domain.Deployment.Environments.Environment;

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

    // SSH-specific fields (only set for SshTunnel type)
    public string? SshHost { get; set; }
    public int? SshPort { get; set; }
    public string? SshUsername { get; set; }
    public string? SshAuthMethod { get; set; }
    public string? RemoteSocketPath { get; set; }
}

/// <summary>
/// Maps Domain Environment to DTOs.
/// </summary>
public static class EnvironmentMapper
{
    public static EnvironmentResponse ToResponse(DomainEnvironment environment)
    {
        var response = new EnvironmentResponse
        {
            Id = environment.Id.ToString(),
            Name = environment.Name,
            Type = environment.Type.ToString(),
            ConnectionString = environment.ConnectionConfig.GetDockerHost(),
            IsDefault = environment.IsDefault,
            CreatedAt = environment.CreatedAt
        };

        if (environment.ConnectionConfig is SshTunnelConfig sshConfig)
        {
            response.SshHost = sshConfig.Host;
            response.SshPort = sshConfig.Port;
            response.SshUsername = sshConfig.Username;
            response.SshAuthMethod = sshConfig.AuthMethod.ToString();
            response.RemoteSocketPath = sshConfig.RemoteSocketPath;
        }

        return response;
    }
}

/// <summary>
/// Request DTO for creating a new environment
/// </summary>
public class CreateEnvironmentRequest
{
    public required string Name { get; set; }

    /// <summary>
    /// Environment type: "DockerSocket" (default) or "SshTunnel"
    /// </summary>
    public string Type { get; set; } = "DockerSocket";

    // DockerSocket fields
    public string? SocketPath { get; set; }

    // SSH Tunnel fields
    public string? SshHost { get; set; }
    public int? SshPort { get; set; }
    public string? SshUsername { get; set; }
    public string? SshAuthMethod { get; set; }
    public string? SshSecret { get; set; }
    public string? RemoteSocketPath { get; set; }

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

    /// <summary>
    /// Environment type: "DockerSocket" or "SshTunnel"
    /// </summary>
    public string Type { get; set; } = "DockerSocket";

    // DockerSocket fields
    public string? SocketPath { get; set; }

    // SSH Tunnel fields
    public string? SshHost { get; set; }
    public int? SshPort { get; set; }
    public string? SshUsername { get; set; }
    public string? SshAuthMethod { get; set; }
    public string? SshSecret { get; set; }
    public string? RemoteSocketPath { get; set; }

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
