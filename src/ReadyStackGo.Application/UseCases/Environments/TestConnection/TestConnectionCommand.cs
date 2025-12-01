using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Environments.TestConnection;

public record TestConnectionCommand(string DockerHost) : IRequest<TestConnectionResult>;
