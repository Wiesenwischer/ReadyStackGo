using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Environments.ListEnvironments;

public record ListEnvironmentsQuery : IRequest<ListEnvironmentsResponse>;
