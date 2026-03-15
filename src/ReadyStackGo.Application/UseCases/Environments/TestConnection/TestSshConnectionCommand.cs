using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Environments.TestConnection;

public record TestSshConnectionCommand(
    string Host,
    int Port,
    string Username,
    string AuthMethod,
    string Secret,
    string RemoteSocketPath
) : IRequest<TestConnectionResult>;
