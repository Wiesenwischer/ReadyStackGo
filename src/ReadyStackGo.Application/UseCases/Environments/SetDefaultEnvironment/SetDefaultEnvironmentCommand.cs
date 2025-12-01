using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Environments.SetDefaultEnvironment;

public record SetDefaultEnvironmentCommand(string EnvironmentId) : IRequest<SetDefaultEnvironmentResponse>;
