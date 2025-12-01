using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.Dashboard;
using ReadyStackGo.Application.UseCases.Dashboard.GetDashboardStats;

namespace ReadyStackGo.API.Endpoints.Dashboard;

public class GetDashboardStatsRequest
{
    [BindFrom("environment")]
    public string Environment { get; set; } = null!;
}

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
        Roles("admin", "operator");
        Description(b => b.WithTags("Dashboard"));
    }

    public override async Task HandleAsync(GetDashboardStatsRequest req, CancellationToken ct)
    {
        var environment = Query<string>("environment", false);

        var result = await _mediator.Send(new GetDashboardStatsQuery(environment), ct);

        if (!result.Success)
        {
            ThrowError(result.ErrorMessage ?? "Failed to get dashboard stats");
        }

        Response = result.Stats;
    }
}
