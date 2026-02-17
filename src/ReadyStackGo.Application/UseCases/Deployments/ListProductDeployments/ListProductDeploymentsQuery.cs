using MediatR;

namespace ReadyStackGo.Application.UseCases.Deployments.ListProductDeployments;

/// <summary>
/// Query to list product deployments in an environment.
/// </summary>
public record ListProductDeploymentsQuery(
    string EnvironmentId
) : IRequest<ListProductDeploymentsResponse>;
