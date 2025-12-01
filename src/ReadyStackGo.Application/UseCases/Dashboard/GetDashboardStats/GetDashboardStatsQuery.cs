using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Dashboard.GetDashboardStats;

public record GetDashboardStatsQuery(string? EnvironmentId) : IRequest<GetDashboardStatsResult>;

public record GetDashboardStatsResult(bool Success, DashboardStatsDto Stats, string? ErrorMessage = null);
