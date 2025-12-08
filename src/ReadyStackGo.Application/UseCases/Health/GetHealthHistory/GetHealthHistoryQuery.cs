using MediatR;

namespace ReadyStackGo.Application.UseCases.Health.GetHealthHistory;

/// <summary>
/// Query to get health history for a specific deployment.
/// </summary>
public record GetHealthHistoryQuery(
    string DeploymentId,
    int Limit = 10) : IRequest<GetHealthHistoryResponse>;
