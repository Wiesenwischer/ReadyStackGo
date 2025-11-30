using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Environments.GetEnvironment;

public record GetEnvironmentQuery(string EnvironmentId) : IRequest<EnvironmentResponse?>;
