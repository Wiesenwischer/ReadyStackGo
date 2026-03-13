using MediatR;

namespace ReadyStackGo.Application.UseCases.Health.GetHealthTransitions;

/// <summary>
/// Query to get health status transitions for a specific deployment.
/// </summary>
public record GetHealthTransitionsQuery(
    string DeploymentId) : IRequest<GetHealthTransitionsResponse>;
