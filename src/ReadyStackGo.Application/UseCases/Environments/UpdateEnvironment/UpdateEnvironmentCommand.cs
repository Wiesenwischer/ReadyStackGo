using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Environments.UpdateEnvironment;

public record UpdateEnvironmentCommand(string EnvironmentId, string Name, string SocketPath) : IRequest<UpdateEnvironmentResponse>;
