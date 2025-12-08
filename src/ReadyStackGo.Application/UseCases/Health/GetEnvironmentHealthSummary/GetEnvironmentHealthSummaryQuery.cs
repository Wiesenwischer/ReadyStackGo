using MediatR;

namespace ReadyStackGo.Application.UseCases.Health.GetEnvironmentHealthSummary;

/// <summary>
/// Query to get health summary for all stacks in an environment.
/// </summary>
public record GetEnvironmentHealthSummaryQuery(
    string EnvironmentId) : IRequest<GetEnvironmentHealthSummaryResponse>;
