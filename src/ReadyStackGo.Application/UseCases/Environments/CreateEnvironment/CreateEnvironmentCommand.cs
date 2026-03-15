using MediatR;
using ReadyStackGo.Application.UseCases.Environments;

namespace ReadyStackGo.Application.UseCases.Environments.CreateEnvironment;

public record CreateEnvironmentCommand(
    string Name,
    string Type,
    // DockerSocket fields
    string? SocketPath,
    // SSH Tunnel fields
    string? SshHost,
    int? SshPort,
    string? SshUsername,
    string? SshAuthMethod,
    string? SshSecret,
    string? RemoteSocketPath
) : IRequest<CreateEnvironmentResponse>;
