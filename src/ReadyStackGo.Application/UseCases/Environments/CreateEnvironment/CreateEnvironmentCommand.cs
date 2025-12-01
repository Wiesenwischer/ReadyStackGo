using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Environments.CreateEnvironment;

public record CreateEnvironmentCommand(string Name, string SocketPath) : IRequest<CreateEnvironmentResponse>;
