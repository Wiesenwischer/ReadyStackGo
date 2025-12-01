using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Dashboard;
using ReadyStackGo.Application.UseCases.Dashboard.GetDashboardStats;

namespace ReadyStackGo.API.Endpoints.Dashboard;

public class GetDashboardStatsRequest
{
    [BindFrom("environment")]
    public string? Environment { get; set; }
}

/// <summary>
/// GET /api/dashboard/stats - Get dashboard statistics.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator, Viewer (scoped).
/// </summary>
[RequirePermission("Dashboard", "Read")]
public class GetDashboardStatsEndpoint : Endpoint<GetDashboardStatsRequest, DashboardStatsDto>
{
    private readonly IMediator _mediator;

    public GetDashboardStatsEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/dashboard/stats");
        Description(b => b.WithTags("Dashboard"));
        PreProcessor<RbacPreProcessor<GetDashboardStatsRequest>>();
    }

    public override async Task HandleAsync(GetDashboardStatsRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDashboardStatsQuery(req.Environment), ct);

        if (!result.Success)
        {
            ThrowError(result.ErrorMessage ?? "Failed to get dashboard stats");
        }

        Response = result.Stats;
    }
}
