using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Environments.DeleteEnvironment;

public record DeleteEnvironmentCommand(string EnvironmentId) : IRequest<DeleteEnvironmentResponse>;
