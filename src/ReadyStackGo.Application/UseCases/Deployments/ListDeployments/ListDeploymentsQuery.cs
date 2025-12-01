using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Deployments.ListDeployments;

public record ListDeploymentsQuery(string EnvironmentId) : IRequest<ListDeploymentsResponse>;
